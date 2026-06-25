# 更新日志

所有显著的更改都将记录在此文件中。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### 🎉 新增

- **v0.1.1 — AI 自动调用工具（Tool Calling 闭环）**
  - `ToolRegistry` 工具注册中心（`IToolRegistry` 接口 + 实现）
    - `Register()` 验证工具必须有 `[KernelFunction]` 方法
    - `CreateKernelPlugins()` 把工具打包为 SK 的 `KernelPlugin` 列表
    - `ListTools()` 返回描述符（含 schema + function 数量）
  - `ArchiveByDateTool` 加 `[KernelFunction("archive_by_date")]` 标注
    - SK 自动识别为可调用 function（参数自动推断 schema）
    - 强类型参数 → JSON → 走 `ITool.ExecuteAsync` 单一实现路径
  - `SemanticKernelChatService` 启用 `FunctionChoiceBehavior.Auto()`
    - 系统 prompt 自动包含工具清单
    - SK 1.32 自动处理 tool calling 循环（无需手动循环）
  - `App.xaml.cs` DI 注册 `IToolRegistry` + 创建 Kernel 后注入工具 plugin
  - **新增 14 个单元测试**（ToolRegistry 7 + ChatService 7）
  - **测试统计**：59 → **73 测试**

### 🛠️ v0.1 之前的版本

- **v0.1 — 第一个 MCP 工具：按日期归档文件**
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