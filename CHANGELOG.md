# 更新记录

## 3.2 — 2026-10-22

- 输入不在白名单时给出具体原因，而不是只打印哈希：
  - 已是本工具产物 → 提示无需重复处理
  - 文件大小与官方构建接近但不同 → 「该版本已被解锁下载，无法确认文件安全性，请使用原版重试！」
  - 大小一致但哈希不符 → 说明内容已被改动
  - 其他 → 不支持的版本
  - `versions.json` 增加 `InputSize` 字段

## 3.1 — 2026-10-21

- 版本号升至 3.1，产物名统一为 `PclModpackEnabler.exe`
- 界面重做：三步式布局、状态提示、处理日志、进度条
- 新增 `tools/ExtractPayload`：可从源码构建复现载荷
- 新增 `docs/PROVENANCE.md`、`docs/VERIFICATION.md`、`docs/SOURCE-MAP.md`、`SECURITY.md`
- CI 工作流修正（此前误放在 `github/workflows/`）
## 3.0 — 2026-10-21

- 统一 GUI 与命令行到同一个 `PatcherCore.Process()` 入口
- 覆盖已有输出改用 `File.Replace`，替换失败保留旧文件
- 新增硬链接 / 路径别名保护（`GetFileInformationByHandle`）
- 补齐三种官方构建的白名单与输出哈希；修复 dnlib 重算 MaxStack 导致的误报
- 界面重做：三步式布局、状态提示、处理日志、进度条
- 新增 `tools/ExtractPayload`：可从源码构建复现载荷
- 新增 `docs/PROVENANCE.md`、`docs/VERIFICATION.md`、`docs/SOURCE-MAP.md`
- 程序集版本 3.0.0.0

## 2.x

- 采用 dnlib 文本载荷方案，单文件 net48，支持三种构建
- 校验：产物哈希、全方法比对、元数据与资源比对

## 1.0

- 最初版本：Cecil 方案，从参考构建移植三个方法体