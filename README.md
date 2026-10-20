# PCL 整合包解锁器

给 PCL2 2.99 恢复整合包安装能力的**修改器**。

选一个官方 `PCL2.exe`，生成一份可以正常安装整合包的副本。原文件不动。

> 第三方工具，非 PCL 官方发布。PCL 作者：龙腾猫跃。

---

## 它修什么

官方 2.99 把 `RestrictMcDownload` 写成 `Const = True`，编译时内联。结果是：

| 功能 | 官方状态 |
|---|---|
| 下载页原版版本列表 | 被替换成"关闭下载"任务 |
| 版本修改页原版入口 | 隐藏 |
| **整合包安装** | **入口被优化成无条件抛异常** |
| 资源页整合包"安装"按钮 | 不显示 |

前两项是条件判断，改几个字节就能绕过；**后两项是代码被编译器优化掉了**，二进制里根本没编进去——补丁救不了。

所以本工具的做法是：从同源码、`RestrictMcDownload = False` 编译出的版本里，把缺失的方法体**整体移植**过来。

---

## 怎么用

1. 双击 `PCL整合包解锁器.exe`
2. 点「选择文件」或把 `PCL2.exe` 拖进窗口
3. 校验通过后点「生成解锁版…」
4. 关闭原启动器，运行新生成的副本

命令行（可选）：

```
PCL整合包解锁器.exe --cli "PCL2.exe" "输出.exe"
```

---

## 支持范围

| 项目 | 要求 |
|---|---|
| 版本 | PCL 2.99.0.0 |
| 文件长度 | 5,980,160 或 5,985,280 字节 |
| 补丁点 | 校验通过才处理，否则一个字节都不写 |

已知的三种官方构建都支持：

| 长度 | 特征 | 补丁偏移 |
|---:|---|---|
| 5,980,160 | 未赞助版（主题色不可调） | 180442 / 205542 |
| 5,985,280 | 赞助版 / 更新累积版 | 182174 / 207274 |

工具按文件长度自动选择布局，不一致时拒绝处理、不写任何文件。

---

## 它做了什么

1. **两处字节补丁**
   - 下载页加载器：跳过硬编码的"关闭下载"分支
   - 版本修改页：`Collapsed` → `Visible`

2. **三个方法移植**（用 Mono.Cecil）
   - `ModModpack.ModpackInstall()` — 整合包安装入口
   - `ModModpack.ModpackInstall(String,String,String)` — 实际安装流程
   - `PageDownloadResourceDetail.InitializeStackResourceVersions(StackPanel)` — 资源页"安装"按钮

移植时逐条复制 IL，所有 token 按「声明类型 + 成员名」重映射到目标程序集；局部变量、泛型实例、TypeSpec 由 Cecil 重建。

---

## 验证

- 对两种官方构建实测，产物与人工验证过的版本**逐字节一致**（SHA-256 相同）
- 三个移植方法均通过 `RuntimeHelpers.PrepareMethod` 的 JIT 预编译校验
- 整合包实测：All the Mods 10（1.21.1 + NeoForge，499 个 mod）可正常安装

---

## 为什么 4 MB

内置了移植所需的数据：

| 内容 | 作用 |
|---|---|
| 供体 PCL（压缩） | 提供缺失方法体的原始 IL |
| Mono.Cecil | 读写程序集、处理签名与 token |
| PCLCS / MeloongCore / 若干依赖 DLL | Cecil 解析元数据时需要的程序集 |

全部资源运行时从内存加载，不落盘。

---

## 注意

- 只处理 PCL 2.99.0.0；官方更新后需要重新制作
- 只改上述四处，不碰登录、正版校验、下载源、更新
- 修改启动器行为可能违反其使用条款，风险自担
- 按「现状」提供，不附带任何担保

---

## 源码与自行构建

源码在 `src/`：

| 文件 | 内容 |
|---|---|
| `Program.cs` | 入口、内置资源加载、CLI 模式 |
| `Patcher.cs` | 校验、字节补丁、Cecil 移植引擎、程序集解析器 |
| `MainForm.cs` | WinForms 界面 |
| `PclUnlocker.csproj` | net48 工程（WinForms + Mono.Cecil） |

构建前需要准备 `src/res/`（未提交，因为包含 PCL 本体）：

```
src/res/
├─ donor.bin             解除限制的自编译版 PCL，Deflate 压缩
├─ PCLCS.dll / MeloongCore.dll / *.bin   元数据解析所需的程序集（压缩）
└─ Mono.Cecil.dll        0.11.6
```

然后：

```powershell
dotnet build src/PclUnlocker.csproj -c Release
```

产物在 `src/bin/Release/PCL整合包解锁器.exe`，单文件，约 4.25 MB。

### 工作流程

```
输入 exe
  → 校验长度与补丁点字节
  → 打两处字节补丁（加载器 / 可见性）
  → Cecil 读取目标与供体
  → 三个方法逐条复制 IL，token 按「声明类型 + 成员名」重映射
  → 写回，输出新文件
```

---

## 下载

到 [Releases](../../releases) 下载最新版，附带 SHA-256。

不要从第三方转存处获取——本工具会改动可执行文件，二进制必须能追溯到发布记录。