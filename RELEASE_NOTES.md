## 🐛 DeskPilot v0.9.1 — 修 v0.9.0 启动无界面 bug

### 修了啥

**v0.9.0 zip 在没装 .NET 8 Desktop Runtime 的机器上双击 exe 直接闪退**——看着像"无界面"。

**根因**：`release.yml` 用的是 `--self-contained false`，zip 里只有 `DeskPilot.App.exe` + 一堆 dll，**不包含 .NET 运行时**。在没装 .NET 8 Desktop Runtime 的机器上，Win32 app host 找不到运行时直接 exit -1，没有窗口、没有错误提示。

**修复**：改成 `--self-contained true` + `PublishSingleFile=true`，zip 里包含完整运行时（**73 MB 单文件**），下载即用，不用装任何运行时。

### 下载

| 文件 | 大小 | 说明 |
|------|------|------|
| `DeskPilot-App-v0.9.1-win-x64.zip` | ~73 MB | DeskPilot App 自包含单文件 exe |
| `DeskPilot-Mcp-v0.9.1-win-x64.zip` | ~7 MB | MCP Server 自包含（沿用 v0.9.0，仅重命名） |

> 完整变更历史见 [CHANGELOG.md](https://github.com/maqiul/DeskPilot/blob/master/CHANGELOG.md)