# 源码文件说明（SOURCE MAP）

工具全部代码约 900 行，位于 `src/`。下面是每个文件的职责，方便直接审阅。

---

## 入口与界面

| 文件 | 行数级别 | 职责 |
|---|---|---|
| `src/Program.cs` | ~60 | 进程入口：注册内嵌依赖解析、区分 GUI / 命令行、`--help` |
| `src/MainForm.cs` | ~250 | WinForms 界面：三步式布局、拖拽、状态与日志、生成对话框 |
| `src/AssemblyInfo.cs` | ~10 | 程序集标题与版本（3.0.0.0） |

## 补丁核心

| 文件 | 行数级别 | 职责 |
|---|---|---|
| `src/PatcherCore.cs` | ~130 | 统一入口 `Process()`：只读打开输入、白名单校验、构建、验收、原子写出 |
| `src/Verification.cs` | ~90 | `Canonical()` 规范化文本、全方法逐条比对、元数据表/堆/资源比对 |
| `src/PeVerification.cs` | ~80 | PE 结构检查：MZ/PE/CLR 头、原生资源目录逐 leaf 解析与哈希 |
| `src/BodyRestore.cs` | ~60 | 把 JSON 载荷还原成 `CilBody`：指令、分支、局部变量、异常处理器 |
| `src/TargetResolver.cs` | ~70 | 按"成员全名"在目标模块内解析方法/字段/类型/签名；找不到即失败，无 Importer 兜底 |
| `src/CardVisibility.cs` | ~50 | 定位 `_CardMinecraft` + `set_Visibility` 的唯一指令序列，把 `ldc.i4.2` 改为 `ldc.i4.0` |
| `src/Payload.cs` | ~50 | 载荷与版本条目的数据模型（System.Text.Json / Newtonsoft 均可） |

## 安全与输出

| 文件 | 行数级别 | 职责 |
|---|---|---|
| `src/AtomicOutput.cs` | ~25 | 临时文件 + `File.Replace`/`Move`，失败保留旧输出 |
| `src/FileIdentity.cs` | ~30 | 调 `GetFileInformationByHandle` 比对卷号 + 文件索引，拒绝写入原文件的硬链接/别名 |
| `src/Compatibility.cs` | ~10 | net48 缺少的少量 API 垫片 |

## 数据

| 文件 | 大小 | 说明 |
|---|---|---|
| `Resources/pcl-2.99.json` | ~103 KB | 四个方法的文本 IL 载荷 + 每个方法的校验哈希 |
| `Resources/versions.json` | <1 KB | 三种官方构建的输入/输出哈希白名单 |

载荷来源与提取方式见 `docs/PROVENANCE.md`；提取工具在 `tools/ExtractPayload/`。

## 测试与构建

| 文件 | 职责 |
|---|---|
| `tests/Program.cs` | 23 条断言：白名单、覆盖保护、硬链接、原子写入、精确产物哈希 |
| `test.ps1` / `build.ps1` | 一键测试 / 构建 |
| `.github/workflows/build.yml` | CI：构建 + 测试，`v*` 标签出 Release |

---

## 审阅建议顺序

1. `PatcherCore.Process()` —— 看输入保护与验收顺序
2. `Verification.Canonical()` —— 看"逐条比对"到底比了什么
3. `BodyRestore.Restore()` + `TargetResolver` —— 看载荷如何落回目标程序集
4. `MainForm` —— 界面只是薄壳，逻辑都调 `Process()`
