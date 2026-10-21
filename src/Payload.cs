using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using dnlib.DotNet;
using dnlib.DotNet.Emit;

internal sealed class Payload {
 public int Schema {get;set;}=1;
 public List<BodyData> Methods {get;set;}=new();
}
internal sealed class BodyData {
 public string Method {get;set;}
 public ushort MaxStack {get;set;}
 public bool InitLocals {get;set;}
 public string CanonicalSha256 {get;set;}
 public string[] Locals {get;set;}
 public List<InstructionData> Instructions {get;set;}=new();
 public List<EhData> Handlers {get;set;}=new();
}
internal sealed class InstructionData {
 public string Opcode {get;set;}
 public string Kind {get;set;}
 public string Value {get;set;}
 public int[] Indices {get;set;}
}
internal sealed class EhData {
 public ExceptionHandlerType Kind {get;set;}
 public int TryStart {get;set;}
 public int TryEnd {get;set;}
 public int HandlerStart {get;set;}
 public int HandlerEnd {get;set;}
 public int FilterStart {get;set;}
 public string CatchType {get;set;}
}
internal sealed class VersionEntry {
 public string Version {get;set;}
 public string InputSha256 {get;set;}
 public string OutputSha256 {get;set;}
}

