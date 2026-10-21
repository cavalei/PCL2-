# 自行验证（VERIFICATION）

所有步骤都在本机完成，不需要联网、不需要信任本仓库的任何二进制。

---

## 0. 先看结论

三组"官方构建 → 本工具产物"的哈希是固定的：

| 输入 | 输入 SHA-256 | 输出 SHA-256 |
|---|---|---|
| 累积更新版 | `5B64D6D5…9428FA` | `4B4AAF8F…C0BD3C` |
| 赞助版 | `4C3091AA…9F46F6` | `EBA6BC07…73FB67` |
| 未赞助版 | `6B6E1260…271E7386` | `FAB00425…C53212` |

完整值见 `Resources/versions.json`。下面是**怎么自己得出这些值**。

---

## 1. 验证"载荷确实是编译产物"（最关键）

```powershell
# 1) 取 PCL 官方源码
git clone --recursive https://github.com/Meloong-Git/PCL.git pcl

# 2) 只改一个常量
#    Plain Craft Launcher 2/Modules/ModSecret.vb
#    Public Const RestrictMcDownload As Boolean = True   ->   False

# 3) 编译（需要 .NET SDK 9 + VS 的 MSBuild；GitHub Actions 的 windows-latest 直接可用）
msbuild "Plain Craft Launcher 2.sln" /p:Configuration=Release /p:Platform="Any CPU"

# 4) 提取载荷
dotnet run --project tools/ExtractPayload -- "pcl\Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe" my-payload.json
```

然后比对 `my-payload.json` 里四个方法的 `CanonicalSha256`：

```
LoaderInit()                                  20F0BAB8C386F72D18705E88A17DDBEAD905EEEDDBFDF150B3B32C4C23D04802
ModpackInstall()                              EB96E076F171D5616028BDBFF24ED7248217232754237631C274CF4DEDDE8C7A
ModpackInstall(String,String,String)          87845830148ACDF63D8CDD3DD3118CEE72289BE5B55AE4DFEA0533BF02465853
InitializeStackResourceVersions(StackPanel)   30333D32EDCA13C43D2347252E9BD403C6C3C2C03F2BF21B7D8B6DD1A85C9204
```

**四个值一致 = 仓库里的载荷就是这个源码 + 这个常量编译出来的结果。** 不一致说明源码版本不同（PCL 更新过），此时工具也不该支持新版本。

> 提示：不必把整份 PCL 编出来，用 GitHub Actions 的 `windows-latest` 跑同样命令最省事。

---

## 2. 验证"工具产物等于已知输出"

准备一份支持的官方 EXE（自备），然后：

```powershell
./PclModpackEnabler.exe --input original.exe --output patched.exe
certutil -hashfile patched.exe SHA256
```

输出应与 `Resources/versions.json` 中对应的 `OutputSha256` 一致。

再验证输出文件本身没问题：

```powershell
./PclModpackEnabler.exe --input original.exe --output patched.exe --verify
```

`--verify` 是只读的：不写文件、不改时间戳，会重新检查产物哈希、四个方法体、6338 个未改方法、元数据表与堆、以及原生资源。

---

## 3. 验证"工具没做多余的事"

- 输入以 `FileShare.Read` 只读打开，全程禁止其它写入/删除；结束时再次核对输入哈希未变
- 输出先写同目录临时文件，校验通过后 `File.Replace`/`Move` 交付；失败保留旧文件
- 保存回原文件（含硬链接、规范化同路径）被拒绝
- 工具不联网、不写注册表、不启动子进程、不申请管理员权限

代码量约 900 行，全部在 `src/`，可以直接读。逐文件说明见 `docs/SOURCE-MAP.md`。

---

## 4. 复现工具本身

```powershell
./build.ps1        # 需要 .NET SDK 9；产物 dist/PclModpackEnabler.exe
./test.ps1         # 23 条断言，含 GUI 共享入口、覆盖保护、未知输入拒绝
```

工具自身的哈希会随编译器版本变化，**不承诺逐字节一致**；承诺的是行为和产物一致（第 2 节）。

---

## 5. 如果发现不符

请开 issue 并附上：输入文件 SHA-256、命令行、`--verify` 输出。若某份官方构建不在白名单里，工具会直接拒绝并打印哈希，把该哈希反馈即可。
