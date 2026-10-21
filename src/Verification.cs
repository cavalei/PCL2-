using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;

using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
internal static partial class PatcherCore {
static string Canonical(MethodDef m) {
if(!m.HasBody) return "NO BODY";
var b=m.Body; var ids=b.Instructions.Select((x,i)=>(x,i)).ToDictionary(x=>x.x,x=>x.i);
int Id(Instruction i)=>i==null?-1:ids[i];
string Operand(object o)=>o switch { null=>"",Instruction i=>"@"+Id(i),IList<Instruction> a=>string.Join(",",a.Select(Id)),Local l=>"local:"+l.Index,Parameter p=>"arg:"+p.Index,IMethod x=>x.FullName,IField x=>x.FullName,IType x=>x.FullName,float x=>BitConverter.ToInt32(BitConverter.GetBytes(x),0).ToString(),double x=>BitConverter.DoubleToInt64Bits(x).ToString(),_=>o.ToString()! };
return $"{b.MaxStack}|{b.InitLocals}\n"+string.Join(";",b.Variables.Select(l=>l.Type.FullName))+"\n"+string.Join("\n",b.Instructions.Select(i=>i.OpCode.Code+"|"+Operand(i.Operand)))+"\n"+string.Join("\n",b.ExceptionHandlers.Select(e=>$"{e.HandlerType}|{Id(e.TryStart)}|{Id(e.TryEnd)}|{Id(e.HandlerStart)}|{Id(e.HandlerEnd)}|{Id(e.FilterStart)}|{e.CatchType?.FullName}"));
}
static void VerifyMetadata(ModuleDefMD a,ModuleDefMD b) {
int cells=0,relocated=0;
for(int n=0;n<45;n++) {
var x=a.Metadata.TablesStream.Get((Table)n); var y=b.Metadata.TablesStream.Get((Table)n);
Require(x.Rows==y.Rows,$"Metadata row count changed: {(Table)n} {x.Rows}->{y.Rows}");
for(uint row=1;row<=x.Rows;row++) for(int c=0;c<x.Columns.Count;c++) {
a.Metadata.TablesStream.TryReadColumn(x,row,c,out uint av); b.Metadata.TablesStream.TryReadColumn(y,row,c,out uint bv);
// PE rebuilding relocates method code and field data; RVAs are physical addresses.
if((n==6 || n==29) && c==0) { if(av!=bv) relocated++; continue; }
bool equal=x.Columns[c].ColumnSize switch { ColumnSize.Strings=>a.Metadata.StringsStream.Read(av)==b.Metadata.StringsStream.Read(bv), ColumnSize.Blob=>a.Metadata.BlobStream.Read(av).SequenceEqual(b.Metadata.BlobStream.Read(bv)), ColumnSize.GUID=>a.Metadata.GuidStream.Read(av)==b.Metadata.GuidStream.Read(bv), _=>av==bv }; Require(equal,$"Metadata changed: {(Table)n} row={row} column={c}: {av}->{bv}"); cells++;
}}
foreach(var stream in new[]{"#US","#Blob","#GUID"}) {
var x=a.Metadata.AllStreams.Single(s=>s.Name==stream).CreateReader().ToArray();
var y=b.Metadata.AllStreams.Single(s=>s.Name==stream).CreateReader().ToArray();
Require(y.Take(x.Length).SequenceEqual(x),"Existing metadata heap bytes changed: "+stream); if(x.Length!=y.Length) Console.WriteLine($"METADATA heap {stream}: {x.Length}->{y.Length}, original prefix preserved");
}
Require(a.Resources.Count==b.Resources.Count,"Resource count changed");
for(int i=0;i<a.Resources.Count;i++) {
Require(a.Resources[i].Name==b.Resources[i].Name && a.Resources[i].Attributes==b.Resources[i].Attributes,"Resource metadata changed");
if(a.Resources[i] is EmbeddedResource x && b.Resources[i] is EmbeddedResource y) Require(x.CreateReader().ToArray().SequenceEqual(y.CreateReader().ToArray()),"Resource content changed");
}
Console.WriteLine($"METADATA unchanged cells={cells}; relocated RVA cells={relocated}; existing US/Blob/GUID heap bytes preserved; string values identical (heap offsets may relocate); managed resources identical={a.Resources.Count}");
}
}
