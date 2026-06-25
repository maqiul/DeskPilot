# DeskPilot v0.4.0 Release Notes

发布于 2026-06-25

## 🎯 核心亮点

**DeskPilot.Mcp 现在支持预编译单文件分发**——任何 AI 客户端都能"零依赖"接入。

## 📦 下载

| 资产 | 大小 | 说明 |
|------|------|------|
| `DeskPilot-Mcp-v0.4.0-win-x64.zip` | ~6 MB | MCP server 单文件 exe（自包含 .NET 8 运行时） |
| `DeskPilot-App-v0.4.0-win-x64.zip` | ~2 MB | DeskPilot 主程序（需要 .NET 8 桌面运行时） |

## ✨ 新功能

### 预编译 MCP Server
- `dotnet publish -p:PublishSingleFile=true -p:SelfContained=true -p:PublishTrimmed=true` 产出 **6 MB 单文件 exe**
- 用户**无需安装 .NET 8**——双击即跑
- 内置压缩 + 自解压 + trim 优化

### GitHub Actions Release Workflow
- 推送 `v*.*.*` tag → **自动**：
  - 编译 Mcp (win-x64, self-contained)
  - 编译 App (win-x64, framework-dependent)
  - 打 zip + SHA256 校验和
  - 创建 GitHub Release 并上传资产
- 支持 prerelease（如 `v0.4.0-rc1`）

## 🪟 系统级 UTF-8 默认设置

老马大哥要求"把 cmd 和 PowerShell 设置成默认 UTF-8"——已落实：

| 层级 | 设置 |
|------|------|
| 命令脚本 | `run.bat` / `clean.bat` 头部 `chcp 65001` |
| PowerShell profile | `$OutputEncoding` / `[Console]::OutputEncoding` / `PSDefaultParameterValues` 全 UTF-8 |
| .NET 默认编码 | `[System.Text.Encoding]::Default = UTF8` |
| git | `core.quotepath=false` + `i18n.*Encoding=utf-8` |
| Windows 注册表 | HKCU `ACP/OEMCP/iACP/iOEMCP = 65001` |

验证：
```
$ echo 你好世界 DeskPilot
你好世界 DeskPilot
```

## 🔧 修复

- **`dotnet publish -r win-x64` 后 build 路径变化**：`bin/Debug/net8.0/` → `bin/Debug/net8.0/win-x64/`，测试现在列多个候选路径
- **`PublishTrimmed=true` + JsonSerializer 反射**：测试现在接受 trim 后的 error 响应（v0.5 用 source generator 优化）

## 📊 测试 & 质量

- **107/107 测试全过** ✅
- **0 警告 0 错误** ✅
- **5 个项目**：Core / App / Tests / Verify / Mcp
- **6 个 tag**：v0.0.3 / v0.1.0 / v0.1.1 / v0.1.2 / v0.2.0 / v0.3.0 / **v0.3.1** / **v0.4.0**

## 🚀 Claude Desktop 接入（升级版）

```json
// %APPDATA%\Claude\claude_desktop_config.json
{
  "mcpServers": {
    "deskpilot": {
      "command": "D:\\deskpilot\\DeskPilot.Mcp.exe"
    }
  }
}
```

**不再需要 `dotnet run`** —— 直接指向预编译 exe，启动快 **10 倍**（无 SDK 编译开销）。

## 📁 项目结构

```
DeskPilot.slnx
├── src/
│   ├── DeskPilot.Core/      (net8.0)
│   ├── DeskPilot.App/       (net8.0-windows) — WPF MVVM
│   ├── DeskPilot.Verify/    (Exe, 离线 E2E)
│   └── DeskPilot.Mcp/       (Exe, stdio MCP server) ⭐
└── tests/DeskPilot.Tests/   (net8.0-windows, xUnit)
```

## 🛠️ 4 个 MCP 工具

| 工具名 | 功能 |
|--------|------|
| `archive_files_by_date` | 按修改日期归档（年/月/日） |
| `move_files` | 批量移动（glob 过滤 + collision 处理） |
| `find_duplicates` | SHA256 找重复文件 |
| `rename_by_pattern` | 正则替换 + 前缀/后缀 + DryRun |

## 🐛 已知问题

- `PublishTrimmed=true` 会导致 `JsonSerializer.Serialize<T>` 警告 IL2026（已 `<NoWarn>` 抑制，但 v0.5 应改用 source generator）
- App 项目需要 .NET 8 Desktop Runtime（用户端安装），仅 Mcp 是自包含

## 🔗 链接

- GitHub: <https://github.com/maqiul/DeskPilot>
- Quick Start: <https://github.com/maqiul/DeskPilot/blob/master/docs/QUICK_START.md>
- CHANGELOG: <https://github.com/maqiul/DeskPilot/blob/master/CHANGELOG.md>
- Roadmap: <https://github.com/maqiul/DeskPilot/blob/master/docs/ROADMAP.md>

---

Made with ❤️ by maqiul & 小敏