# 更新日志

所有显著的更改都将记录在此文件中。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### 🎉 新增

- **v0.1 — 第一个 MCP 工具：按日期归档文件**
  - `ITool` 接口 + `ToolResult`（统一工具抽象，未来可暴露为 MCP Server）
  - `ArchiveByDateTool`：按修改时间/创建时间 + 年/月/日粒度归档
  - **功能特性**：
    - 支持修改时间 / 创建时间切换
    - 支持年 / 月 / 日三种粒度
    - 支持自定义目标目录（默认 `{source}/archive/`）
    - 支持文件 glob 过滤（`*.pdf`）
    - 支持 **DryRun 模式**（只预览不实际移动）
    - 智能 collision 处理（同名 + `_2`/`_3`/`_4` 后缀）
    - 标准 `ToolResult` 输出（Success / Summary / Data / ErrorMessage）
  - **新增 13 个单元测试**（月粒度 / 年粒度 / 日粒度 / Created时间 / DryRun / 自定义目标 / 模式过滤 / 重名冲突 / 错误处理 / 空目录 / 不存在目录 / 无效 JSON / 报告统计）
  - **测试统计**：46 → **59 测试**

### 🛠️ v0.1 之前的版本

- **v0.0.3 — 动态模型列表（UI 闭环）**

### 🐛 修复

- **`dotnet test` 缓存旧版 App.dll 导致测试与代码错位**：新增 `clean.bat` 一键清 bin/obj + 重建
- `SecureSettingsService.Load` 的 `catch` 收紧为只捕获 `CryptographicException`，让其他异常可见
- `SettingsFilePath_IsUnderAppData` 用 `Assert.EndsWith` 消除 xUnit2009 警告

## [0.0.2] - 2026-06-25

### 🎉 新增

- **设置窗口**：UI 配置 Provider / API Key / Model（不再手动改 .env）
- **DPAPI 加密存储**：API Key 等敏感信息用 Windows DPAPI 加密到 `%APPDATA%\DeskPilot\settings.dat`
- **动态重建**：切换 Provider 不重启，自动重建 Kernel 和 ChatService
- **`SettingsViewModel`** + **可注入 Action 模式**（测试不依赖 WPF STA）
- **测试覆盖**：从 3 个测试 → 24 个（新增 SettingsViewModel 12 + SecureSettingsService 9）

### 🛠️ 技术栈新增

- `Microsoft.Extensions.Configuration.{Json,EnvironmentVariables,UserSecrets} 10.0.9`
- `Microsoft.Extensions.DependencyInjection 10.0.9`

## [0.0.1] - 2026-06-25

### 🎉 新增

- 首个 MVP 版本发布
- 智能问答窗口，支持多 AI Provider（OpenAI / DeepSeek / Ollama）
- MVVM 架构（CommunityToolkit.Mvvm）
- 三种配置方式：.env 文件 / User Secrets / 环境变量
- 缺 Key 时友好弹窗提示
- 一键启动脚本（run.bat）
- 单元测试覆盖（3 个测试用例）

### 🛠️ 技术栈

- .NET 8 + WPF
- Semantic Kernel 1.32.0
- CommunityToolkit.Mvvm 8.4.2
- DotNetEnv 3.2.0
- xUnit + 自写 Stub

[Unreleased]: https://github.com/yourname/deskpilot/compare/v0.0.1...HEAD
[0.0.1]: https://github.com/yourname/deskpilot/releases/tag/v0.0.1