# 更新记录

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