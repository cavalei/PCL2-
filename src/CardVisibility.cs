using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

internal static partial class PatcherCore {
// Match the property's backing field and Visibility setter, never a byte offset.
static MethodDef ShowMinecraftCard(ModuleDef module) {
 var page=module.Find("PCL.PageDownloadModify",false) ?? throw new InvalidOperationException("Missing modify page");
 var card=module.Find("PCL.PageDownloadInstall",false)?.Fields.Single(f=>f.Name=="_CardMinecraft" && f.FieldSig.Type.FullName=="PCL.MyCard")
   ?? throw new InvalidOperationException("Missing CardMinecraft backing field");
 var matches=new List<(MethodDef Method,Instruction Constant)>();
 foreach(var method in page.Methods.Where(m=>m.HasBody)) {
  var il=method.Body.Instructions;
  for(int i=0;i+2<il.Count;i++) {
   bool fieldLoad=il[i].OpCode==OpCodes.Ldfld && il[i].Operand is IField f && f.FullName==card.FullName;
   if((il[i].OpCode==OpCodes.Call || il[i].OpCode==OpCodes.Callvirt) && il[i].Operand is IMethod getter && getter.Name=="get_CardMinecraft") {
    var def=getter.ResolveMethodDef();
    fieldLoad=def?.Body?.Instructions.Any(x=>x.OpCode==OpCodes.Ldfld && x.Operand is IField backing && backing.FullName==card.FullName)==true;
   }
   if(fieldLoad && il[i+2].OpCode==OpCodes.Callvirt && il[i+2].Operand is IMethod setter && setter.FullName=="System.Void System.Windows.UIElement::set_Visibility(System.Windows.Visibility)")
    matches.Add((method,il[i+1]));
  }
 }
 Require(matches.Count==1,"Expected exactly one CardMinecraft Visibility assignment; found "+matches.Count);
 var match=matches.Single();
 Require(match.Constant.OpCode==OpCodes.Ldc_I4_2 && match.Constant.Operand==null,"Expected Collapsed (ldc.i4.2) at CardMinecraft Visibility assignment");
 match.Constant.OpCode=OpCodes.Ldc_I4_0;
 Console.WriteLine("CARD PATCH "+match.Method.FullName+": ldc.i4.2 -> ldc.i4.0 (one instruction)");
 return match.Method;
}
}

