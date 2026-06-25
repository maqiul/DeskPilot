## 🎨 DeskPilot v0.8.0 — 视觉升级 + 暗色主题

### 暗色模式终于来了

DeskPilot 现在支持完整的暗色主题。一键切换、跟随系统、记忆你的选择。

**怎么用：**
1. 打开设置 → 🎨 外观
2. 选「浅色 / 暗色 / 跟随系统」
3. 立刻生效，重启后保持

**暗色配色：**
- 背景：`#1A1A1A` 深灰
- 卡片：`#252525` 略浅
- 边框：`#3A3A3A`
- 文字：`#E8E8E8` 主 + `#999999` 次
- 主色：橙色 `#FF7A28`（暗色下略亮，平衡对比度）

### 视觉细节升级

- **卡片阴影**：每个 Section/气泡都有轻微软阴影（看起来"飘"在背景上）
- **圆角统一**：卡片 12px、按钮 8px、输入框 12px
- **消息气泡**：圆形 36×36 头像（user 在右、assistant 在左），气泡带阴影
- **空状态**：首次打开显示 👋 欢迎卡片 + 4 个建议按钮（点一下自动发问）
- **加载动画**：3 个跳动圆点（替代原静态 "AI 思考中..." 文字）
- **标题栏**：橙色方块 logo + DeskPilot 名 + 副标题
- **图标统一**：🤖/🔑/💻/🛡️/🎨 各 Section 加图标

### 🔧 内部改动

- `Styles/DarkColors.xaml` 暗色配色
- `Services/ThemeManager.cs` 运行时合并/移除 ResourceDictionary
- `App.xaml.cs` 启动时按 settings 应用主题
- `Models/AppSettings.cs` 加 `Theme` 字段
- `RoleToAvatarColumnConverter` 头像列切换
- `EnumToBoolConverter` 给 RadioButton 用
- `ChatViewModel.HasMessages` 控制空状态卡片显隐

### 📦 下载

| 文件 | 说明 |
|------|------|
| `DeskPilot-App-v0.8.0-win-x64.zip` | DeskPilot App（需要 .NET 8 运行时） |
| `DeskPilot-Mcp-v0.8.0-win-x64.zip` | MCP Server（自包含，无需运行时） |

> 完整变更历史见 [CHANGELOG.md](https://github.com/maqiul/DeskPilot/blob/master/CHANGELOG.md)
