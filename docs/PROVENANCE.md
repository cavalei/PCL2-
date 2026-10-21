# 数据来源与可复现性（PROVENANCE）

本文件回答一个问题：**`Resources/pcl-2.99.json` 里的 IL 是从哪来的，能不能被复现。**

---

## 一句话结论

载荷里的四个方法体，来自**按 PCL 官方开源源码、只把 `RestrictMcDownload` 由 `True` 改为 `False` 后编译出来的程序集**。它们不是"破解"出来的，而是那份构建里本来就存在的正常方法；提取过程只是把元数据里的方法体读成文本。

任何人都能重走一遍，并得到逐字节相同的载荷。

---

## 载荷覆盖的方法

| 方法 | 指令数 | 异常处理器 |
|---|---:|---:|
| `PCL.PageDownloadInstall::LoaderInit()` | 18 | 0 |
| `PCL.ModModpack::ModpackInstall()` | 41 | 0 |
| `PCL.ModModpack::ModpackInstall(String,String,String)` | 330 | 3 |
| `PCL.PageDownloadResourceDetail::InitializeStackResourceVersions(StackPanel)` | 196 | 2 |

另有一处不靠载荷的指令级补丁：`PageDownloadModify.LoadInstanceFinished(...)` 中的 `ldc.i4.2 → ldc.i4.0`，通过 `_CardMinecraft` 后备字段 + `set_Visibility` 的指令序列唯一定位。

---

## 参考构建

| 项目 | 值 |
|---|---|
| 来源 | PCL 官方源码（Meloong-Git/PCL），仅改 `ModSecret.RestrictMcDownload = False` |
| 编译环境 | GitHub Actions `windows-latest`，`msbuild /p:Configuration=Release` |
| 文件大小 | 5,904,384 字节 |
| SHA-256 | `B55583652E113BE70E45BC2A1EF974114575D0AE6743D244FA419F01498A92B3` |

该二进制**不随本仓库分发**。它只是提取时的输入。

---

## 提取方式

```powershell
dotnet run --project tools/ExtractPayload -- <参考构建.exe> payload.json
```

`tools/ExtractPayload/Program.cs` 做的事：

1. 用 dnlib 载入参考构建（只读）
2. 按方法全名找到上述四个方法
3. **脱敏**：把编译时嵌进 `ldstr` 的个人构建路径替换成中性文件名
   - 例：`D:\a\pcl-selfbuild\...\Modules\Minecraft\ModModpack.vb` → `ModModpack.vb`
   - 只修改字符串操作数，指令、栈、异常处理器一概不动
4. 逐条导出操作码、操作数种类与完整签名（成员用全名而非 token）、局部变量类型、分支与 EH 索引
5. 按与 `src/PatcherCore.cs` 完全一致的 `Canonical()` 规则计算每个方法的校验哈希

导出的就是 `Resources/pcl-2.99.json`（schema 1）。

---

## 校验哈希（可对照）

| 方法 | Canonical SHA-256 |
|---|---|
| `LoaderInit()` | `20F0BAB8C386F72D18705E88A17DDBEAD905EEEDDBFDF150B3B32C4C23D04802` |
| `ModpackInstall()` | `EB96E076F171D5616028BDBFF24ED7248217232754237631C274CF4DEDDE8C7A` |
| `ModpackInstall(String,String,String)` | `87845830148ACDF63D8CDD3DD3118CEE72289BE5B55AE4DFEA0533BF02465853` |
| `InitializeStackResourceVersions(StackPanel)` | `30333D32EDCA13C43D2347252E9BD403C6C3C2C03F2BF21B7D8B6DD1A85C9204` |

> 以上为当前载荷的真实值（取自 `Resources/pcl-2.99.json`）。若你重跑提取后四个值完全一致，说明载荷来源与仓库一致。

---

## 交叉验证：两份独立构建给出同一份载荷

本仓库的载荷最初由一份参考构建导出。之后我们又用**另一份独立编译的构建**（同样源码、同样常量，不同时间/机器）跑了一次提取，四个方法的 `CanonicalSha256` 与仓库中的值**完全一致**。

这说明载荷取决于源码与常量，而不是某台机器或某次编译的偶然产物。

---

## 为什么不用"改二进制"的办法

官方构建里 `RestrictMcDownload` 是 `Const`，编译期内联。整合包安装入口被优化成**无条件抛异常**，其后的安装代码根本不存在于二进制中（不可达代码被裁掉）。

所以对官方 EXE 做字节补丁，无论怎么改都补不回功能——只能把方法体从"编译正确"的构建里取回来。这正是本工具采用载荷方案的原因。

---

## 许可与归属

- PCL 著作权归原作者龙腾猫跃，上游许可见 `docs/PCL-UPSTREAM-LICENCE.txt`
- 载荷是 PCL 的派生数据，**不适用**本仓库的 MIT 许可
- 本仓库不含任何 PCL 二进制
