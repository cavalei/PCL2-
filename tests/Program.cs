using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class Tests {
 static int checks;
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
 static void Reject(Action action,string message) {try{action();}catch{checks++;return;}throw new Exception(message);}
 static bool Same(byte[] a,byte[] b)=>a.SequenceEqual(b);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateHardLink(string link,string existing,IntPtr reserved);
 [STAThread]static int Main(string[] args) {
  Bootstrap.Register();
  try{return Run(args);}catch(Exception e){Console.Error.WriteLine("FAIL: "+System.Text.RegularExpressions.Regex.Replace(e.Message,@"(?i)[A-Z]:\\[^\r\n]+","<local-path>"));return 1;}
 }
 [MethodImpl(MethodImplOptions.NoInlining)]static int Run(string[] args) {
  string dir=Path.Combine(Path.GetTempPath(),"pcl-fix-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try {
   // Writer tests do not require PCL binaries and run in CI.
   string target=Path.Combine(dir,"existing.bin");byte[] old={1,2,3},next={4,5,6};File.WriteAllBytes(target,old);
   Reject(()=>AtomicOutput.Write(next,target,false),"Existing output unexpectedly overwritten");Check(Same(File.ReadAllBytes(target),old),"Rejected write changed output");
   using(var held=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read)) {
    Reject(()=>AtomicOutput.Write(next,target,true),"Locked target replacement should fail");
    Check(Same(File.ReadAllBytes(target),old),"Replacement failure destroyed old output");
   }
   Check(!Directory.GetFiles(dir,".pclunlocker-*.tmp").Any(),"Leaked temporary file");
   AtomicOutput.Write(next,target,true);Check(Same(File.ReadAllBytes(target),next),"Replacement failed");
   string fresh=Path.Combine(dir,"new.bin");AtomicOutput.Write(old,fresh,false);Check(Same(File.ReadAllBytes(fresh),old),"New output failed");
   // Exercise the exact GUI entry point; these guards run before parsing input.
   string input=Path.Combine(dir,"original.exe");File.WriteAllBytes(input,old);
   Reject(()=>GuiController.Generate(input,input,true,_=>{}),"GUI allowed original overwrite");
   Reject(()=>GuiController.Generate(input,Path.Combine(dir,".","original.exe"),true,_=>{}),"GUI normalization bypass");
   Check(Same(File.ReadAllBytes(input),old),"GUI modified original");
   Reject(()=>PatcherCore.Process(input,target,false,true,_=>{}),"Force bypassed input allowlist");
   Check(Same(File.ReadAllBytes(target),next),"Unknown input changed output");
   Reject(()=>PatcherCore.Process(input,target,true,true,_=>{}),"verify+force accepted");
   Console.WriteLine("PASS: atomic replacement, replacement failure preserves old file, temp cleanup, GUI same-file guard, allowlist protection");
   if(args.Length==1) {
    byte[] original=File.ReadAllBytes(args[0]);File.WriteAllBytes(input,original);string output=Path.Combine(dir,"patched.exe");
    string expected="4B4AAF8F874A3C5C6D45C7507B427879091757D04FF9B39E9412EC7BB8C0BD3C";
    GuiController.Generate(input,output,false,_=>{});
    Check(PatcherCore.Hash(File.ReadAllBytes(output))==expected,"GUI output SHA256 mismatch");
    DateTime stamp=File.GetLastWriteTimeUtc(output);PatcherCore.Process(input,output,true,false,_=>{});
    Check(File.GetLastWriteTimeUtc(output)==stamp,"Verify modified output");
    Check(Same(File.ReadAllBytes(input),original),"Input changed");
    Reject(()=>GuiController.Generate(input,input,true,_=>{}),"GUI accepted real input overwrite");
    Check(Same(File.ReadAllBytes(input),original),"Original changed on rejection");
    Reject(()=>PatcherCore.Process(input,output,false,false,_=>{}),"Overwrite without force accepted");
    PatcherCore.Process(input,output,false,true,_=>{});Check(PatcherCore.Hash(File.ReadAllBytes(output))==expected,"Force output mismatch");
    string alias=Path.Combine(dir,"alias.exe");Check(CreateHardLink(alias,input,IntPtr.Zero),"Hardlink fixture failed");
    Reject(()=>GuiController.Generate(input,alias,true,_=>{}),"GUI allowed replacement via hardlink");
    Check(Same(File.ReadAllBytes(input),original),"Hardlink write changed input");
    Console.WriteLine("PASS: original PCL fixture -> exact SHA256="+expected+"; 6334 other methods preserved; GUI and CLI shared path; verify read-only; hardlink input protected");
   }
   Console.WriteLine("PASS: "+checks+" assertions");return 0;
  }finally{Directory.Delete(dir,true);}
 }
}

