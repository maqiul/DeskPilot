# 📝 Changelog

DeskPilot 所有重要变更记录。版本遵循 [Semantic Versioning](https://semver.org/)。

## [v0.2.0] - 2026-06-25

### 🎉 新增功能

#### 3 个新 MCP 工具
- **MoveFilesTool** (`move_files`) - 批量移动文件
  - 支持 glob 过滤（如 `*.pdf`）
  - 可选递归子目录
  - 自动创建目标目录
  - Collision 自动加 `_2`/`_3` 后缀
- **FindDuplicatesTool** (`find_duplicates`) - 查找内容完全相同的文件
  - 按 SHA256 哈希判断（先按 size 预筛提速）
  - 报告浪费空间（可清理多少 MB）
  - 可选递归 + 最小文件大小过滤
- **RenameByPatternTool** (`rename_by_pattern`) - 批量重命名
  - 正则替换（支持 `$1`/`$2` 捕获组）
  - 前缀/后缀添加
  - DryRun 模式（只预览不重命名）
  - 3 种模式可组合使用

#### UI 进度展示
- `ChatViewModel` 新增 `ToolStatus` 字段（底部状态栏）
- `SemanticKernelChatService` 暴露 `ToolInvoking`/`ToolInvoked` 事件
- 使用 SK 1.32 推荐的 `IFunctionInvocationFilter`（替代过时的 events API）
- 工具调用时状态栏实时显示：
  - `🔧 正在调用 archive_files_by_date...`
  - `✅ archive_files_by_date 完成 (123ms)`
- WPF 状态栏带 ⚙️ 图标 + 蚂蚁灰文字 + 边框

### 🛠️ 改进
- `DeskPilot.Verify` 程序扩展为 4 工具统一 E2E 验证
  - 支持 `--tool <name>` 指定单个工具
  - 支持 `--no` DryRun 模式
  - 真实执行 + 总结报告

### 📦 项目变更
- 4 个工具统一注册到 `App.xaml.cs` DI 容器
- AI 系统 prompt 自动列出 4 个工具描述

### ✅ 测试
- **104/104 全过**（从 v0.1.2 的 73 → 104，+31）
  - `MoveFilesToolTests`: 7 个
  - `FindDuplicatesToolTests`: 10 个
  - `RenameByPatternToolTests`: 11 个
  - `ToolEventArgsTests`: 3 个
- 0 警告 0 错误

### 📊 E2E 验证（DeskPilot.Verify）
- 4 工具在真实文件系统上端到端通过
  - ArchiveByDate: 3 文件 → 按月归档 ✅
  - MoveFiles: 3 文件 → move_dst ✅
  - FindDuplicates: 找到 1 组重复 ✅
  - RenameByPattern: IMG_001~003 → photo_001~003 ✅

---

## [v0.1.2] - 2026-06-25

### 🎉 新增功能
- **GitHub 公开仓库上线**：https://github.com/maqiul/DeskPilot
  - 推送 master + 4 tags (v0.0.3, v0.1.0, v0.1.1, v0.1.2)
  - CI workflow + Issue 模板 + Contributing 指南
- **DeskPilot.Verify 项目**：离线 E2E 验证程序
  - 无需 API Key，直接跑工具看真实效果
  - 用法：`dotnet run --project src/DeskPilot.Verify -- <sourceDir> [granularity] [dateField] [--no]`

### ✅ 测试
- 73/73 全过（v0.1.1 → v0.1.2 测试无变化）

---

## [v0.1.1] - 2026-06-25

### 🎉 新增功能
- **AI 自动调用工具闭环**
  - `IToolRegistry` + `ToolRegistry` 工具注册中心
  - `ArchiveByDateTool` 加 `[KernelFunction("archive_by_date")]` 标注
  - `SemanticKernelChatService` 启用 `FunctionChoiceBehavior.Auto()`（SK 自动处理 tool calling 循环）
  - `App.xaml.cs` DI 注入工具到 Kernel
- **杀手锏工作流**：用户说"把 D:\发票 按月归档" → AI 自动调工具 → 报告

### ✅ 测试
- 73/73 全过（v0.1.0 → v0.1.1，+14 测试）

---

## [v0.1.0] - 2026-06-25

### 🎉 新增功能
- **第一个 MCP 工具**：`ArchiveByDateTool`（按日期归档）
  - `ITool` + `ToolResult` 统一抽象
  - 按修改/创建时间 + 年/月/日粒度归档
  - DryRun / glob 过滤 / 自定义目标 / collision 处理

### ✅ 测试
- 59/59 全过（+13 测试）

---

## [v0.0.3] - 2026-06-25

### 🎉 新增功能
- **动态模型列表 UI 闭环**
  - 设置窗口的"🔄 刷新模型列表"按钮
  - `OpenAIModelLister` / `DeepSeekModelLister` / `OllamaModelLister` 三个动态 Lister
  - 静态兜底（OpenAI 6 + DeepSeek 3）
  - 错误吞咽策略（网络错误返回空列表）

### ✅ 测试
- 46/46 全过

---

## [v0.0.2] - 2026-06-25

### 🎉 新增功能
- **多 AI Provider 支持**：OpenAI / DeepSeek / Ollama
- **4 种配置方式**：.env / User Secrets / 环境变量 / DPAPI 加密
- **设置窗口**：UI 配置 Provider/Key/Model
- **DPAPI 加密**：`%APPDATA%\DeskPilot\settings.dat`

---

## [v0.0.1] - 2026-06-25

### 🎉 首个发布
- 项目骨架 + WPF 聊天窗口
- Semantic Kernel 集成 + 蚂蚁金服橙配色
- MVVM 架构（CommunityToolkit.Mvvm）
- 完整 CI 文档
