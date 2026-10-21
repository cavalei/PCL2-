using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
[assembly: InternalsVisibleTo("PclFix.Tests")]
internal static class Bootstrap {
 internal static void Register() {
  AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=> {
   string name=new AssemblyName(e.Name).Name;
   if(name!="dnlib"&&name!="Newtonsoft.Json")return null;
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PclFusion.Embedded."+name+".dll")) {
    if(stream==null)return null;
    using(var memory=new MemoryStream()){stream.CopyTo(memory);return Assembly.Load(memory.ToArray());}
   }
  };
 }
}
internal static class Program {
 [DllImport("kernel32.dll")]static extern bool AttachConsole(uint processId);
 [STAThread]static int Main(string[] args) {
  Bootstrap.Register();
  if(args.Length>0){AttachConsole(unchecked((uint)-1));return RunCli(args);}
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm());return 0;
 }
 [MethodImpl(MethodImplOptions.NoInlining)]static int RunCli(string[] args) {
  try {
   if(args.Length==1&&args[0]=="--help"){Console.WriteLine("PclModpackEnabler --input <original.exe> --output <patched.exe> [--verify | --force]");return 0;}
   string input=null,output=null;bool verify=false,force=false;var seen=new HashSet<string>();
   for(int i=0;i<args.Length;i++) {
    string option=args[i];if(!seen.Add(option))throw new ArgumentException("Duplicate option: "+option);
    switch(option) {
     case "--input":case "--output":
      if(i+1>=args.Length||args[i+1].StartsWith("--"))throw new ArgumentException("Missing value: "+option);
      if(option=="--input")input=args[++i];else output=args[++i];break;
     case "--verify":verify=true;break;case "--force":force=true;break;
     default:throw new ArgumentException("Unknown option: "+option);
    }
   }
   if(input==null||output==null)throw new ArgumentException("--input and --output are required");
   PatcherCore.Process(input,output,verify,force,Console.WriteLine);return 0;
  }catch(Exception e){Console.Error.WriteLine("ERROR: "+e.Message);return 1;}
 }
}
