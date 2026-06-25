# 更新日志

所有显著的更改都将记录在此文件中。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### 🎉 新增

- **v0.0.3 — 动态模型列表（UI 闭环）**
  - `IModelLister` 接口 + 3 个实现：`OpenAIModelLister` / `DeepSeekModelLister` / `OllamaModelLister`
  - `HttpModelListerBase` 通用基类（鉴权 + 错误吞咽）
  - `AiModelCatalog` 静态兜底（OpenAI 6 个 / DeepSeek 3 个 / Ollama 动态）
  - `ModelListerFactory` 路由 AiProvider → 具体 Lister
  - `AppSettings.CachedModels` 字段：拉到的列表加密缓存到 `%APPDATA%\DeskPilot\settings.dat`
  - `SettingsViewModel.RefreshModelsCommand` + `CurrentModelList` ObservableCollection
  - **设置窗口每个 Provider 卡片新增 🔄 刷新按钮**（含 loading 状态）
  - **模型下拉框改为 `IsEditable` ComboBox**：可下拉选，也可手动输入
  - `Microsoft.Extensions.Http 8.0.0` 引入（HttpClient 注入）
  - **新增 22 个单元测试**（ModelLister 16 + Refresh Command 6）

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