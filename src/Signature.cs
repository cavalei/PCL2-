using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using dnlib.DotNet;

internal sealed class SignatureEntry {
 public string Label {get;set;}
 public int MethodCount {get;set;}
 public string FullRoot {get;set;}
 public int CoreCount {get;set;}
 public string CoreRoot {get;set;}
}
internal sealed class UnlockedShape {
 public int ModpackInstall {get;set;}
 public int ModpackInstall3 {get;set;}
}
internal sealed class SignatureFile {
 public string Note {get;set;}
 public List<SignatureEntry> Signatures {get;set;}=new();
 public UnlockedShape UnlockedShape {get;set;}
}
internal sealed class FingerprintResult {
 public int Count;
 public int CoreCount;
 public string Full;
 public string Core;
 public SignatureEntry Match;
}

internal static partial class PatcherCore {
 static readonly string[] TargetMethods = {
  "System.Void PCL.PageDownloadInstall::LoaderInit()",
  "System.Void PCL.ModModpack::ModpackInstall()",
  "PCL.ModLoader/LoaderCombo`1<System.String> PCL.ModModpack::ModpackInstall(System.String,System.String,System.String)",
  "System.Void PCL.PageDownloadResourceDetail::InitializeStackResourceVersions(System.Windows.Controls.StackPanel)",
  "System.Void PCL.PageDownloadModify::LoadInstanceFinished(PCL.ModLoader/LoaderTask`2<PCL.ModMinecraft/McInstance,System.Object>)"
 };

 // 逐个方法体算 canonical 哈希，再对 (全名|哈希) 排序求根。
 // 同一份源码在不同时间编译：方法体不变 → 根相同；文件哈希变了也不影响判定。
 internal static FingerprintResult Fingerprint(ModuleDefMD mod) {
  var all=new SortedDictionary<string,string>(StringComparer.Ordinal);
  foreach(var t in mod.GetTypes())foreach(var m in t.Methods) {
   if(!m.HasBody)continue;
   all[m.FullName]=Hash(Encoding.UTF8.GetBytes(Canonical(m)));
  }
  var core=all.Where(kv=>!TargetMethods.Contains(kv.Key)).ToList();
  var fp=new FingerprintResult { Count=all.Count,CoreCount=core.Count };
  fp.Full=Root(all);fp.Core=Root(core);
  return fp;
 }
 static string Root(IEnumerable<KeyValuePair<string,string>> items) {
  var sb=new StringBuilder();
  foreach(var kv in items)sb.Append(kv.Key).Append('|').Append(kv.Value).Append('\n');
  return Hash(Encoding.UTF8.GetBytes(sb.ToString()));
 }
 internal static FingerprintResult MatchFingerprint(byte[] bytes) {
  FingerprintResult fp;int modpack=-1,modpack3=-1;
  using(var mod=ModuleDefMD.Load(bytes)) {
   fp=Fingerprint(mod);
   foreach(var t in mod.GetTypes())foreach(var m in t.Methods) {
    if(!m.HasBody)continue;
    if(m.FullName=="System.Void PCL.ModModpack::ModpackInstall()")modpack=m.Body.Instructions.Count;
    if(m.FullName.StartsWith("PCL.ModLoader/LoaderCombo`1<System.String> PCL.ModModpack::ModpackInstall(",StringComparison.Ordinal))
     modpack3=m.Body.Instructions.Count;
   }
  }
  var file=Resource<SignatureFile>("signatures.json");
  fp.Match=file.Signatures.FirstOrDefault(s=>s.MethodCount==fp.Count && string.Equals(s.FullRoot,fp.Full,StringComparison.OrdinalIgnoreCase));
  if(fp.Match!=null)return fp;
  // 只改了目标方法（其余方法体仍与某个基准一致）→ 就是被别的工具改过的
  if(lx(file,fp)!=null)
   throw new InvalidOperationException("该版本已被解锁下载，无法确认文件安全性，请使用原版重试！");
  var shape=file.UnlockedShape;
  if(shape!=null && modpack==shape.ModpackInstall && modpack3==shape.ModpackInstall3)
   throw new InvalidOperationException("该版本已被解锁下载，无法确认文件安全性，请使用原版重试！");
  throw new InvalidOperationException("无法识别这个 PCL 构建：方法体指纹与已知基准都不一致。\r\n"
   +"方法数 "+fp.Count+"，指纹 "+fp.Full.Substring(0,16)+"…\r\n"
   +"请使用官方原版 PCL 2.99 重试；若确认这是官方构建，请把上面这行反馈给我们，以便收录。");
 }
 static SignatureEntry lx(SignatureFile file,FingerprintResult fp) {
  return file.Signatures.FirstOrDefault(s=>s.CoreCount==fp.CoreCount && string.Equals(s.CoreRoot,fp.Core,StringComparison.OrdinalIgnoreCase));
 }
}
