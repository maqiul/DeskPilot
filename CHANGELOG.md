# 📝 Changelog

DeskPilot 所有重要变更记录。版本遵循 [Semantic Versioning](https://semver.org/)。

## [v0.7.0] - 2026-06-25

### 🧠 新增功能

#### 本地记忆持久化：AI 跨会话记住你
- **`IMemoryStore`** 接口：抽象记忆存储（支持未来扩展 SQLite/云同步）
- **`LocalJsonMemoryStore`**：JSON 文件存储（`%AppData%/DeskPilot/memory.json`）
- **自动保存**：每次对话后自动保存，最多保留 100 条消息
- **启动恢复**：打开 DeskPilot 后 AI 自动加载上次对话上下文
- **清空功能**：点"清空对话"按钮同时删除本地记忆文件
- **容错**：文件损坏自动备份 + 降级（不影响启动）

## [v0.6.0] - 2026-06-25

### 🛡️ 新增功能

#### 权限控制：危险工具需用户确认
- **工具风险分级**：`ITool` 新增 `RiskLevel`（`Safe` / `Destructive`）
- **确认机制**：危险工具首次调用时拦截，AI 会自动询问用户"确认执行？"
- **智能缓存**：用户确认后 30 秒内同一参数再次调用自动放行
- **开关控制**：`AppSettings.RequireConfirmation`（设置窗口可开关，默认开）
- **拦截层**：`ToolCallObserver`（SK `IFunctionInvocationFilter`）在工具执行前检查

工具分级：
| 工具 | 风险等级 | 原因 |
|------|---------|------|
| find_duplicates | Safe | 只读扫描 |
| hash_files | Safe | 只读计算 |
| archive_files_by_date | Destructive | 移动文件 |
| move_files | Destructive | 移动文件 |
| rename_by_pattern | Destructive | 重命名文件 |
| batch_resize_image | Destructive | 覆盖图片 |
| extract_archive | Destructive | 解压可能覆盖 |

#### Release workflow 修复
- `release` job 加 `actions/checkout`（之前缺 checkout 导致 CHANGELOG.md 不可读）
- 指定 `ref: master` + `sparse-checkout`（只拉 release notes 文件，速度最快）

## [v0.5.1] - 2026-06-25

### 🎉 新增功能

#### AI 流式输出（打字机效果）
- **IChatService 新增 `ChatStreamAsync`**：`IAsyncEnumerable<string>` 逐 token 返回
- **SK 流式 API**：`GetStreamingChatMessageContentsAsync` + `FunctionChoiceBehavior.Auto()`
  - Tool Calling 自动处理——工具先内部执行，后流式输出最终 LLM 回复
- **ChatViewModel 改造**：先插入空 assistant 气泡 → 逐片追加 → 打字机效果
- **取消键优化**：取消后消息气泡保留已输出的内容 + `⏸️ 已取消`

#### CI 启动 smoke test（防 XAML 崩溃回归）
- `DESKPILOT_SMOKE_TEST=1` 环境变量触发简化启动路径
- `StubChatService`：不调 AI，直接走完 XAML 解析 → DI 注入 → 窗口创建全链路
- 自动 `Shutdown(0)` 退出：exit 0 = 通过，exit 2 = 崩溃

### 🔧 改进
- `IChatService` 继承 `IDisposable`（统一生命周期管理）
- `ci.yml` smoke test 改用 `DESKPILOT_SMOKE_TEST=1` + `-Wait` 模式（替代旧的手动 kill）

## [v0.5.0] - 2026-06-25

### 🎉 新增功能

#### 7 工具矩阵（4 → 7）
- **BatchResizeImageTool**：批量缩放图片（依赖 System.Drawing.Common）
- **ExtractArchiveTool**：解压 zip 文件（System.IO.Compression 内置）
- **HashFilesTool**：计算文件哈希（SHA256/SHA1/MD5 等，无额外依赖）
- **MCP Server 同步更新**：4 → 7 工具暴露

#### 修复 WPF 启动崩溃
- **根因**：`App.xaml` 残留 `StartupUri="Views/ChatWindow.xaml"` 导致无参构造 XamlParseException
- **修复**：移除 StartupUri，全走 DI 构造

#### 全部历史版本见下文

### 🎉 新增功能

#### MCP Server 封装（杀手锏新方向）
- **新项目**：`src/DeskPilot.Mcp/` —— .NET 8 控制台 stdio MCP server
- **4 个工具暴露**：archive_files_by_date / move_files / find_duplicates / rename_by_pattern
- **外部 AI 客户端可接入**：
  - Claude Desktop（JSON 配置文件）
  - Cursor
  - Continue.dev
  - 任何支持 MCP 协议的 AI 客户端
- **设计**：
  - 每个工具一个 `[McpServerTool]` 方法（强类型参数 + /// XML doc comment 描述）
  - 内部转 JSON 调 `ITool.ExecuteAsync` —— 零业务逻辑，全部复用现有工具
  - 日志走 stderr（避免污染 JSON-RPC 协议）
  - 用 `ModelContextProtocol 0.3.0-preview.4` SDK

#### MCP Server 端到端测试
- `McpServerTests` (3 个)：
  - `Server_Initialize_ReturnsServerInfo` — 握手成功
  - `Server_ToolsList_Returns4Tools` — 4 个工具全部注册
  - `Server_ToolsCall_FindDuplicates_ReturnsResult` — 真实调用 find_duplicates
- **真实启停 Mcp server 进程 + stdio JSON-RPC 通信**

### 📦 项目变更
- `DeskPilot.Mcp` 加入 `DeskPilot.slnx`
- 4 → **5 个项目**（Core/App/Tests/Verify/Mcp）

### ✅ 测试
- **107/107 全过**（v0.2.0 → v0.3.0，+3 MCP E2E）

### 🔧 集成示例（Claude Desktop）

`%APPDATA%\Claude\claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "deskpilot": {
      "command": "dotnet",
      "args": ["run", "--project", "D:\\opensource\\DeskPilot\\src\\DeskPilot.Mcp"]
    }
  }
}
```

之后在 Claude Desktop 就能直接说：
- "用 DeskPilot 把我桌面上重复的文件找出来"
- "把 D:\\发票 按月归档"

---

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
