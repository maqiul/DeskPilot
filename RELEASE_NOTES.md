## 🛠 DeskPilot v0.9.0 — 技能系统 + 修欢迎卡片 bug

### 技能系统上线

DeskPilot 现在支持"技能"——把常用操作打包成一键触发的预设 prompt。点击后 AI 自动按预设模板 + 工具组合完成任务。

**用法 1：顶部快捷横条**
聊天窗口标题栏下方新增快捷技能横条，点击任意卡片 → 自动填入输入框并发送。

**用法 2：设置窗口管理**
设置 → 🛠 技能：列出全部 8 个内置技能，开关一键启用/禁用，关掉的不再显示在顶部横条。

### 8 个内置技能

| 图标 | 名称 | 分类 |
|------|------|------|
| 📁 | 整理下载文件夹 | 文件整理 |
| 🔍 | 找出重复的照片 | 文件整理 |
| ✏️ | 批量重命名文件 | 文件整理 |
| 🖼 | 批量压缩图片 | 图片处理 |
| 📦 | 批量解压压缩包 | 文件整理 |
| 🔐 | 计算文件哈希值 | 文件整理 |
| 📊 | 清理大文件 | 文件整理 |
| 🗓 | 按日期归档文件 | 文件整理 |

### 🐛 修复

- **欢迎卡片叠加 bug**：切换 AI 服务后，消息列表和"👋 你好"欢迎卡片同时显示，视觉上像中间悬浮弹窗。根因是 `BoolToVisibilityConverter` 没读 `ConverterParameter`，导致 `Invert` 永远失效。1 行改动修好。

### 🔧 内部改动

- `Skill` 数据模型 + `SkillSet` 集合辅助
- `ISkillService` + `SkillService`（默认 JSON 嵌入式资源 + 用户文件合并 + 损坏备份容错 + `SkillsChanged` 事件）
- `ChatViewModel.EnabledSkills` ObservableCollection 订阅事件
- `SettingsViewModel.Skills` + `SkillRow` 内部类（双向绑定 → 写回 Service）
- 3 处 `App.xaml.cs` DI 注册（真实启动 / smoke test / PromptForSettings）

### 📦 下载

| 文件 | 说明 |
|------|------|
| `DeskPilot-App-v0.9.0-win-x64.zip` | DeskPilot App（需要 .NET 8 运行时） |
| `DeskPilot-Mcp-v0.9.0-win-x64.zip` | MCP Server（自包含，无需运行时） |

> 完整变更历史见 [CHANGELOG.md](https://github.com/maqiul/DeskPilot/blob/master/CHANGELOG.md)