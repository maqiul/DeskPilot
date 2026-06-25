## 🧠 DeskPilot v0.7.0 — 本地记忆

### AI 跨会话记住你

DeskPilot 现在有了真记忆。关掉应用、第二天再开，AI 仍然记得上次聊了什么、你们怎么聊的。

**怎么工作：**
1. 你问 AI 一个问题 → 它回复
2. 双方对话**自动保存**到本地（`%AppData%\DeskPilot\memory.json`）
3. 关闭 DeskPilot → 下次打开 → **AI 自动加载历史上下文**
4. 觉得记忆太多？点"清空对话" → 同步删除本地文件

**核心特点：**
- **本地优先**：所有数据存在你的电脑上，不上云
- **持久化**：关闭再打开、关机重启都不丢
- **容量控制**：最多保留 100 条消息，超出自动裁剪最旧
- **容错降级**：文件损坏自动备份 + 静默降级，不影响启动
- **可清空**：一键清除本地记忆

### 🛡️ 顺手加：权限开关

设置窗口里加了"启用危险操作确认"开关。关掉后 AI 直接执行文件操作（适合"信任模式"）；默认开启，安全第一。

### 🔧 其他改进

- 修复 `release.yml`：`sparse-checkout + ref:master` 导致 `softprops/action-gh-release` git 操作报 exit code 128
- 修复 release notes 提取：原代码 `cp` 整个 CHANGELOG.md，现在按版本号 `awk` 裁剪当前 section
- 修复 release notes 覆盖：加 `overwrite: true` 让重新 tag 时更新 release page
- 修复 `files: '**/*.zip'` glob 失败：改用精确文件名

### 📦 下载

| 文件 | 说明 |
|------|------|
| `DeskPilot-App-v0.7.0-win-x64.zip` | DeskPilot App（需要 .NET 8 运行时） |
| `DeskPilot-Mcp-v0.7.0-win-x64.zip` | MCP Server（自包含，无需运行时） |

> 完整变更历史见 [CHANGELOG.md](https://github.com/maqiul/DeskPilot/blob/master/CHANGELOG.md)
