## 🛡️ DeskPilot v0.6.0 — 权限控制

### 危险工具需用户确认

DeskPilot 现在有了真正的安全边界。AI 在调用危险工具（移动/重命名/解压/缩放图片）前，会先征求你的同意。

**怎么工作：**
1. 你让 AI 做一件事（比如"把桌面的 PDF 移到 D:\docs"）
2. AI 决定调 `move_files` 工具
3. 🛑 **拦截！** AI 回复："⚠️ 即将移动 15 个文件到 D:\docs。确认执行吗？"
4. 你回复"确认" → AI 再次调用 → ✅ 执行

**工具风险分级：**

| 风险等级 | 工具 |
|---------|------|
| ✅ Safe | find_duplicates, hash_files |
| ⚠️ Destructive | archive_files_by_date, move_files, rename_by_pattern, batch_resize_image, extract_archive |

**可配置：** 设置窗口里可以开关"危险操作需确认"（默认开启）。

### 🔧 其他改进

- 修复 `release.yml` release job 缺少代码 checkout（导致 Release Notes 始终为空）
- 修复 CI workflow 分支监听：`main` → `master`

### 📦 下载

| 文件 | 说明 |
|------|------|
| `DeskPilot-App-v0.6.0-win-x64.zip` | DeskPilot App（需要 .NET 8 运行时） |
| `DeskPilot-Mcp-v0.6.0-win-x64.zip` | MCP Server（自包含，无需运行时） |

> 完整变更历史见 [CHANGELOG.md](https://github.com/maqiul/DeskPilot/blob/master/CHANGELOG.md)
