using System;
using System.Collections.Generic;
using System.Linq;

using dnlib.DotNet;
using dnlib.DotNet.Emit;

internal static partial class PatcherCore {
 // 2.99.0.1 在 PageDownloadLeft 的构造函数里把「安装」入口和「资源」标签设成 Collapsed，
 // 并把默认子页改成「模组」。这里把这段截掉，恢复成 2.99.0.0 的形态。
 internal const string LeftNavCtor="System.Void PCL.PageDownloadLeft::.ctor()";

 internal static bool UnhideLeftNav(ModuleDef mod) {
  var type=mod.GetTypes().FirstOrDefault(t=>t.FullName=="PCL.PageDownloadLeft");
  if(type==null)return false;
  var ctor=type.Methods.FirstOrDefault(m=>m.Name==".ctor"&&!m.IsStatic&&m.HasBody);
  if(ctor==null)return false;
  var body=ctor.Body;
  var ins=body.Instructions;
  int cut=-1;
  for(int i=0;i<ins.Count;i++)
   if(ins[i].OpCode==OpCodes.Call&&ins[i].Operand is IMethod im&&im.Name=="InitializeComponent"){cut=i;break;}
  if(cut<0)return false;
  // 已经是精简形态：InitializeComponent 之后只有 ret
  if(ins.Count==cut+2&&ins[cut+1].OpCode==OpCodes.Ret)return false;
  // 确认后面确实是在隐藏界面元素，避免误伤别的构建
  bool hiding=false;
  for(int i=cut+1;i<ins.Count;i++)
   if(ins[i].OpCode==OpCodes.Callvirt&&ins[i].Operand is IMethod m2&&m2.Name=="set_Visibility")hiding=true;
  if(!hiding)return false;
  while(ins.Count>cut+1)ins.RemoveAt(ins.Count-1);
  ins.Add(Instruction.Create(OpCodes.Ret));
  body.ExceptionHandlers.Clear();
  body.UpdateInstructionOffsets();
  body.MaxStack=Math.Max(body.MaxStack,(ushort)2);
  return true;
 }

 // 校验：构造函数必须是精简形态（8 条指令，最后 ret，且不再调用 set_Visibility）
 internal static void VerifyLeftNav(MethodDef m) {
  var ins=m.Body.Instructions;
  Require(ins.Count==8,"Left nav ctor has "+ins.Count+" instructions, expected 8");
  Require(ins[ins.Count-1].OpCode==OpCodes.Ret,"Left nav ctor does not end with ret");
  foreach(var i in ins)
   Require(!(i.OpCode==OpCodes.Callvirt&&i.Operand is IMethod im&&im.Name=="set_Visibility"),
    "Left nav ctor still hides UI elements");
 }
}
