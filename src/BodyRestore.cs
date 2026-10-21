using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
internal static partial class PatcherCore {
static void Restore(BodyData data,TargetResolver resolver) {
 var target=resolver.MethodDefinition(data.Method);
 var body=new CilBody {MaxStack=data.MaxStack,InitLocals=data.InitLocals,KeepOldMaxStack=true};
 foreach(var name in data.Locals) body.Variables.Add(new Local(resolver.Signature(name)));
 var opcodes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(o=>o.Name);
 foreach(var i in data.Instructions) body.Instructions.Add(new Instruction(opcodes[i.Opcode]));
 Instruction At(int i)=>i==-1?null:body.Instructions[i];
 for(int j=0;j<data.Instructions.Count;j++) {
  var d=data.Instructions[j]; int Number()=>int.Parse(d.Value,CultureInfo.InvariantCulture);
  body.Instructions[j].Operand=d.Kind switch {
   "none"=>null,"branch"=>At(d.Indices.Single()),"switch"=>d.Indices.Select(At).ToArray(),
   "local"=>body.Variables[Number()],"parameter"=>target.Parameters[Number()],
   "method"=>resolver.Method(d.Value),"field"=>resolver.Field(d.Value),"type"=>resolver.Type(d.Value),
   "string"=>d.Value,"Int32"=>Number(),"SByte"=>sbyte.Parse(d.Value,CultureInfo.InvariantCulture),
   "Byte"=>byte.Parse(d.Value,CultureInfo.InvariantCulture),"Int64"=>long.Parse(d.Value,CultureInfo.InvariantCulture),
   "Single"=>float.Parse(d.Value,CultureInfo.InvariantCulture),"Double"=>double.Parse(d.Value,CultureInfo.InvariantCulture),
   _=>throw new InvalidOperationException("Unsupported operand kind: "+d.Kind)
  };
 }
 foreach(var e in data.Handlers) body.ExceptionHandlers.Add(new ExceptionHandler(e.Kind) {TryStart=At(e.TryStart),TryEnd=At(e.TryEnd),HandlerStart=At(e.HandlerStart),HandlerEnd=At(e.HandlerEnd),FilterStart=At(e.FilterStart),CatchType=e.CatchType==null?null:resolver.Type(e.CatchType)});
 if(data.Locals.SequenceEqual(target.Body.Variables.Select(l=>l.Type.FullName))) body.LocalVarSigTok=target.Body.LocalVarSigTok;
 target.Body=body;
 Require(Hash(Encoding.UTF8.GetBytes(Canonical(target)))==data.CanonicalSha256,"Restored payload failed validation: "+data.Method);
}
}


