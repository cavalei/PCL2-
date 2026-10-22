using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Newtonsoft.Json;

internal static partial class PatcherCore {
 internal static string Hash(byte[] data) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","");}
 static void Require(bool ok,string why) {if(!ok)throw new InvalidOperationException(why);}
 static T Resource<T>(string name) {
  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PclFusion."+name))
  using(var reader=new StreamReader(stream ?? throw new InvalidOperationException("Missing resource: "+name)))
   return JsonConvert.DeserializeObject<T>(reader.ReadToEnd()) ?? throw new InvalidOperationException("Invalid resource: "+name);
 }
 internal static string Inspect(string path) {
  using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
   byte[] bytes=ReadAll(file); var version=KnownVersion(bytes);
   return version.Version+"; SHA256="+Hash(bytes);
  }
 }
 static VersionEntry KnownVersion(byte[] input) {
  string hash=Hash(input);
  List<VersionEntry> all=Resource<List<VersionEntry>>("versions.json");
  VersionEntry hit=all.SingleOrDefault(v=>v.InputSha256==hash);
  if(hit!=null)return hit;
  throw new InvalidOperationException(Explain(input,hash,all));
 }
 // 输入不在白名单时，尽量说清楚原因，而不是只丢一串哈希。
 static string Explain(byte[] input,string hash,List<VersionEntry> all) {
  foreach(VersionEntry v in all)
   if(v.OutputSha256==hash)
    return "这个文件已经是解锁版，无需再处理。\r\n若要重新生成，请使用官方原版 PCL。";
  foreach(VersionEntry v in all) {
   if(v.InputSize<=0)continue;
   long diff=Math.Abs(input.LongLength-v.InputSize);
   if(diff>0&&diff<=4096)
    return "该版本已被解锁下载，无法确认文件安全性，请使用原版重试！";
  }
  foreach(VersionEntry v in all)
   if(v.InputSize==input.LongLength)
    return "文件大小与官方构建一致，但内容已被改动，或不是同一版本。\r\nSHA256: "+hash+"\r\n请使用官方原版重试！";
  return "不支持的 PCL 版本（可能不是 2.99.0.0）。\r\nSHA256: "+hash;
 }
 // GUI and CLI enter the exact same input guard and generation path.
 internal static void Process(string input,string output,bool verify,bool force,Action<string> log) {
  Require(!(verify&&force),"--verify and --force are mutually exclusive");
  string inputFull=Path.GetFullPath(input),outputFull=Path.GetFullPath(output);
  Require(!inputFull.Equals(outputFull,StringComparison.OrdinalIgnoreCase),"Input and output must differ; original file is protected");
  // Deny writes and deletion while generating, including aliases to the original file.
  using(var file=new FileStream(inputFull,FileMode.Open,FileAccess.Read,FileShare.Read)) {
   if(File.Exists(outputFull))FileIdentity.RequireDifferent(file,outputFull);
   byte[] original=ReadAll(file); string inputHash=Hash(original); var version=KnownVersion(original);
   log("VERSION="+version.Version);log("INPUT SHA256="+inputHash);
   byte[] result;
   if(verify)result=File.ReadAllBytes(outputFull);
   else {Require(force||!File.Exists(outputFull),"Output exists; use --force to replace it"); result=Build(original);}
   Require(Hash(result)==version.OutputSha256,"Output SHA256 mismatch: "+Hash(result));
   Verify(original,result,log);
   if(!verify)AtomicOutput.Write(result,outputFull,force);
   file.Position=0;Require(Hash(ReadAll(file))==inputHash,"Input changed");
   Require(Hash(File.ReadAllBytes(outputFull))==version.OutputSha256,"On-disk output mismatch");
   log("OUTPUT SHA256="+version.OutputSha256);
   log(verify?"VERIFY PASS (read-only)":"PATCH PASS (input unchanged)");
  }
 }
 static byte[] ReadAll(Stream stream) {using(var memory=new MemoryStream()){stream.CopyTo(memory);return memory.ToArray();}}
 static byte[] Build(byte[] original) {
  var payload=Resource<Payload>("pcl-2.99.json");Require(payload.Schema==1&&payload.Methods.Count==4,"Unsupported payload");
  using(var mod=ModuleDefMD.Load(original)) {
   var resolver=new TargetResolver(mod);
   foreach(var method in mod.GetTypes().SelectMany(t=>t.Methods).Where(m=>m.HasBody))method.Body.KeepOldMaxStack=true;
   foreach(var body in payload.Methods)Restore(body,resolver);
   ShowMinecraftCard(mod);
   var opts=new ModuleWriterOptions(mod){Logger=DummyLogger.NoThrowInstance,MetadataOptions={Flags=MetadataFlags.PreserveAll}};
   using(var memory=new MemoryStream()){mod.Write(memory,opts);return memory.ToArray();}
  }
 }
 static void Verify(byte[] input,byte[] result,Action<string> log) {
  var payload=Resource<Payload>("pcl-2.99.json");var patched=payload.Methods.ToDictionary(x=>x.Method);
  using(var before=ModuleDefMD.Load(input))using(var after=ModuleDefMD.Load(result)) {
   var methods=after.GetTypes().SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);
   var originals=before.GetTypes().SelectMany(t=>t.Methods).ToArray();Require(methods.Count==originals.Length,"Method count changed");
   var card=ShowMinecraftCard(before);int unchanged=0;
   foreach(var m in originals) {
    Require(methods.TryGetValue(m.FullName,out var n),"Missing method: "+m.FullName);
    if(patched.TryGetValue(m.FullName,out var data)) {
     Require(Hash(Encoding.UTF8.GetBytes(Canonical(n)))==data.CanonicalSha256,"Payload mismatch: "+m.FullName);
     log("BODY "+m.FullName+": instructions="+n.Body.Instructions.Count+", EH="+n.Body.ExceptionHandlers.Count);
    } else if(m.FullName==card.FullName)Require(Canonical(card)==Canonical(n),"Card patch differs");
    else {Require(Canonical(m)==Canonical(n),"Unexpected method change: "+m.FullName);unchanged++;}
   }
   log("OTHER METHODS unchanged="+unchanged);VerifyMetadata(before,after);
  }
  Require(VerifyPE(input)==VerifyPE(result),"Native resources changed");
 }
}

