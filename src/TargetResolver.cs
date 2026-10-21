using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using dnlib.DotNet;

// Only target-owned metadata can enter the reconstructed bodies. Missing entries fail closed,
// including external entries: no assembly probing, guessed signatures or network resolution.
internal sealed class TargetResolver {
 readonly Dictionary<string,IMethod> methods=new();
 readonly Dictionary<string,IField> fields=new();
 readonly Dictionary<string,ITypeDefOrRef> types=new();
 readonly Dictionary<string,TypeSig> signatures=new();
 public TargetResolver(ModuleDefMD module) {
  void AddSig(TypeSig s) { if(s==null || !signatures.TryAdd(s.FullName,s)) return; if(s.Next!=null) AddSig(s.Next); if(s is GenericInstSig g) { AddSig(g.GenericType); foreach(var a in g.GenericArguments) AddSig(a); } }
  void AddMethod(IMethod m) { methods.TryAdd(m.FullName,m); if(m.MethodSig!=null) { AddSig(m.MethodSig.RetType); foreach(var p in m.MethodSig.Params) AddSig(p); } }
  foreach(var t in module.GetTypes()) {
   types.TryAdd(t.FullName,t); AddSig(t.ToTypeSig());
   foreach(var m in t.Methods) { AddMethod(m); if(m.HasBody) foreach(var l in m.Body.Variables) AddSig(l.Type); }
   foreach(var f in t.Fields) {fields.TryAdd(f.FullName,f); AddSig(f.FieldSig.Type);}
  }
  foreach(var t in module.GetTypeRefs()) { types.TryAdd(t.FullName,t); AddSig(t.ToTypeSig()); }
  for(uint row=1;row<=module.Metadata.TablesStream.TypeSpecTable.Rows;row++) { var t=module.ResolveTypeSpec(row); types.TryAdd(t.FullName,t); AddSig(t.TypeSig); }
  foreach(var m in module.GetMemberRefs()) {if(m.IsMethodRef) AddMethod(m); else {fields.TryAdd(m.FullName,m);AddSig(m.FieldSig.Type);}}
  for(uint row=1;row<=module.Metadata.TablesStream.MethodSpecTable.Rows;row++) { var m=module.ResolveMethodSpec(row); AddMethod(m); foreach(var a in m.GenericInstMethodSig.GenericArguments) AddSig(a); }
 }
 static T Find<T>(Dictionary<string,T> map,string name)=>map.TryGetValue(name,out var value)?value:throw new InvalidOperationException("Unresolved target member/signature (no import fallback): "+name);
 public MethodDef MethodDefinition(string name)=>Find(methods,name) as MethodDef ?? throw new InvalidOperationException("Target is not a method definition: "+name);
 public IMethod Method(string name) {
  var m=Find(methods,name);
  // Fresh MethodSpec; generic arguments come from target metadata, preserving ValueTypeSig.
  return m is MethodSpec s ? new MethodSpecUser(s.Method,new GenericInstMethodSig(s.GenericInstMethodSig.GenericArguments.ToArray())) : m;
 }
 public IField Field(string name)=>Find(fields,name);
 public ITypeDefOrRef Type(string name)=>Find(types,name);
 public TypeSig Signature(string name)=>Find(signatures,name);
}

