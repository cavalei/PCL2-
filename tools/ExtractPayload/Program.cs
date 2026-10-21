using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

// 从"未上锁"的 PCL 构建里导出四个方法的文本 IL 载荷。
// 用法: ExtractPayload <参考构建.exe> <输出.json>
internal static class Program
{
    private static readonly string[] Targets =
    {
        "System.Void PCL.PageDownloadInstall::LoaderInit()",
        "System.Void PCL.ModModpack::ModpackInstall()",
        "PCL.ModLoader/LoaderCombo`1<System.String> PCL.ModModpack::ModpackInstall(System.String,System.String,System.String)",
        "System.Void PCL.PageDownloadResourceDetail::InitializeStackResourceVersions(System.Windows.Controls.StackPanel)",
    };

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("用法: ExtractPayload <参考构建.exe> <输出.json>");
            return 2;
        }

        using var module = ModuleDefMD.Load(args[0]);
        var byName = module.GetTypes().SelectMany(t => t.Methods).ToDictionary(m => m.FullName, m => m);
        var payload = new Payload { Schema = 1 };
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        foreach (var name in Targets)
        {
            if (!byName.TryGetValue(name, out var method))
            {
                Console.Error.WriteLine("缺少方法: " + name);
                return 3;
            }
            Sanitize(method);
            payload.Methods.Add(Dump(method));
            Console.WriteLine("已导出 " + name);
        }

        File.WriteAllText(args[1], JsonSerializer.Serialize(payload, options), new UTF8Encoding(false));
        Console.WriteLine("已写出: " + args[1]);
        return 0;
    }

    // 把编译时嵌入的个人路径替换成中性文件名：只改字符串操作数，指令与栈不变。
    private static void Sanitize(MethodDef method)
    {
        foreach (var ins in method.Body.Instructions)
        {
            if (ins.Operand is not string s) continue;
            if (!s.EndsWith(".vb", StringComparison.OrdinalIgnoreCase)) continue;
            if (!s.Contains('\\') && !s.Contains('/')) continue;
            var cut = Math.Max(s.LastIndexOf('\\'), s.LastIndexOf('/'));
            var leaf = s.Substring(cut + 1);
            Console.WriteLine("  脱敏: " + s + "  ->  " + leaf);
            ins.Operand = leaf;
        }
    }

    private static BodyData Dump(MethodDef method)
    {
        var body = method.Body;
        var data = new BodyData
        {
            Method = method.FullName,
            MaxStack = body.MaxStack,
            InitLocals = body.InitLocals,
            Locals = body.Variables.Select(v => v.Type.FullName).ToArray(),
            CanonicalSha256 = Hash(Encoding.UTF8.GetBytes(Canonical(method))),
        };

        var index = body.Instructions.Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);

        foreach (var ins in body.Instructions)
        {
            var item = new InstructionData { Opcode = ins.OpCode.Name };
            switch (ins.Operand)
            {
                case null:
                    item.Kind = "none";
                    break;
                case Instruction target:
                    item.Kind = "branch";
                    item.Indices = new[] { index[target] };
                    break;
                case Instruction[] targets:
                    item.Kind = "switch";
                    item.Indices = targets.Select(t => index[t]).ToArray();
                    break;
                case Local local:
                    item.Kind = "local";
                    item.Value = local.Index.ToString(CultureInfo.InvariantCulture);
                    break;
                case Parameter parameter:
                    item.Kind = "parameter";
                    item.Value = parameter.Index.ToString(CultureInfo.InvariantCulture);
                    break;
                case IMethod m:
                    item.Kind = "method";
                    item.Value = m.FullName;
                    break;
                case IField f:
                    item.Kind = "field";
                    item.Value = f.FullName;
                    break;
                case IType t:
                    item.Kind = "type";
                    item.Value = t.FullName;
                    break;
                case string s:
                    item.Kind = "string";
                    item.Value = s;
                    break;
                case sbyte sb:
                    item.Kind = "SByte";
                    item.Value = sb.ToString(CultureInfo.InvariantCulture);
                    break;
                case byte by:
                    item.Kind = "Byte";
                    item.Value = by.ToString(CultureInfo.InvariantCulture);
                    break;
                case int i32:
                    item.Kind = "Int32";
                    item.Value = i32.ToString(CultureInfo.InvariantCulture);
                    break;
                case long i64:
                    item.Kind = "Int64";
                    item.Value = i64.ToString(CultureInfo.InvariantCulture);
                    break;
                case float f32:
                    item.Kind = "Single";
                    item.Value = f32.ToString("R", CultureInfo.InvariantCulture);
                    break;
                case double f64:
                    item.Kind = "Double";
                    item.Value = f64.ToString("R", CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new NotSupportedException("未支持的操作数: " + ins.Operand.GetType().Name);
            }
            data.Instructions.Add(item);
        }

        foreach (var handler in body.ExceptionHandlers)
        {
            data.Handlers.Add(new EhData
            {
                Kind = (int)handler.HandlerType,
                TryStart = Idx(index, handler.TryStart),
                TryEnd = Idx(index, handler.TryEnd),
                HandlerStart = Idx(index, handler.HandlerStart),
                HandlerEnd = Idx(index, handler.HandlerEnd),
                FilterStart = Idx(index, handler.FilterStart),
                CatchType = handler.CatchType?.FullName,
            });
        }
        return data;
    }

    private static int Idx(Dictionary<Instruction, int> index, Instruction ins) =>
        ins == null ? -1 : index[ins];

    private static string Hash(byte[] b) => Convert.ToHexString(SHA256.HashData(b));

    // 必须与 PatcherCore.Canonical 完全一致，否则校验哈希对不上。
    private static string Canonical(MethodDef m)
    {
        if (!m.HasBody) return "NO BODY";
        var b = m.Body;
        var ids = b.Instructions.Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);
        int Id(Instruction i) => i == null ? -1 : ids[i];
        string Operand(object o) => o switch
        {
            null => "",
            Instruction i => "@" + Id(i),
            IList<Instruction> a => string.Join(",", a.Select(Id)),
            Local l => "local:" + l.Index,
            Parameter p => "arg:" + p.Index,
            IMethod x => x.FullName,
            IField x => x.FullName,
            IType x => x.FullName,
            float x => BitConverter.SingleToInt32Bits(x).ToString(),
            double x => BitConverter.DoubleToInt64Bits(x).ToString(),
            _ => o.ToString(),
        };
        return b.MaxStack + "|" + b.InitLocals + "\n"
            + string.Join(";", b.Variables.Select(l => l.Type.FullName)) + "\n"
            + string.Join("\n", b.Instructions.Select(i => i.OpCode.Code + "|" + Operand(i.Operand))) + "\n"
            + string.Join("\n", b.ExceptionHandlers.Select(e =>
                e.HandlerType + "|" + Id(e.TryStart) + "|" + Id(e.TryEnd) + "|" + Id(e.HandlerStart) + "|" + Id(e.HandlerEnd) + "|" + Id(e.FilterStart) + "|" + (e.CatchType?.FullName)));
    }
}

internal sealed class Payload
{
    public int Schema { get; set; }
    public List<BodyData> Methods { get; set; } = new();
}

internal sealed class BodyData
{
    public string Method { get; set; }
    public ushort MaxStack { get; set; }
    public bool InitLocals { get; set; }
    public string CanonicalSha256 { get; set; }
    public string[] Locals { get; set; }
    public List<InstructionData> Instructions { get; set; } = new();
    public List<EhData> Handlers { get; set; } = new();
}

internal sealed class InstructionData
{
    public string Opcode { get; set; }
    public string Kind { get; set; }
    public string Value { get; set; }
    public int[] Indices { get; set; }
}

internal sealed class EhData
{
    public int Kind { get; set; }
    public int TryStart { get; set; }
    public int TryEnd { get; set; }
    public int HandlerStart { get; set; }
    public int HandlerEnd { get; set; }
    public int FilterStart { get; set; }
    public string CatchType { get; set; }
}
