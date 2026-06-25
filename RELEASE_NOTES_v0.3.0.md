# 🚀 DeskPilot v0.3.0 — MCP Server 封装（外部 AI 客户端可接入）

> **发布日期：** 2026-06-25
> **Commit：** `028b0b5`
> **测试：** 107/107 ✅
> **下载：** Source code (zip) / (tar.gz)

---

## 🎉 这是什么版本？

v0.3.0 是 DeskPilot 的**第三个里程碑版本**，打开**新维度**——

**之前**：DeskPilot 只是你电脑上的一个 WPF 应用
**现在**：DeskPilot 4 个工具通过 [Model Context Protocol](https://modelcontextprotocol.io/) 暴露给**任何**支持 MCP 的 AI 客户端（Claude Desktop / Cursor / Continue.dev / 自研客户端...）

---

## ✨ 新增功能

### 🌐 MCP Server 封装

新项目 `src/DeskPilot.Mcp/` —— 独立的 .NET 8 stdio MCP server。

**暴露的 4 个工具**（MCP tool name）：

| 工具 | MCP 名称 | 能力 |
|------|---------|------|
| `ArchiveByDateTool` | `archive_files_by_date` | 按日期归档 |
| `MoveFilesTool` | `move_files` | 批量移动 |
| `FindDuplicatesTool` | `find_duplicates` | 找重复文件 |
| `RenameByPatternTool` | `rename_by_pattern` | 批量重命名 |

### 🔌 接入示例：Claude Desktop

编辑 `%APPDATA%\Claude\claude_desktop_config.json`：

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

**重启 Claude Desktop**，你就能在 Claude 里直接说：

```
用 deskpilot 把我桌面上重复的照片找出来
用 deskpilot 把 D:\发票 按月归档
用 deskpilot 把 D:\photos 里所有 IMG_*.jpg 改成 vacation_*.jpg
```

Claude 会自动调 DeskPilot 的工具完成任务。

### 🔌 接入示例：Cursor / Continue.dev

类似配置，把 `command` + `args` 指向 DeskPilot.Mcp 项目即可。

---

## 📊 数字说话

| 指标 | v0.2.0 | v0.3.0 | 变化 |
|------|--------|--------|------|
| 测试数 | 104 | **107** | **+3** |
| 项目数 | 4 | **5** | **+1** |
| MCP 工具 | 4 (内嵌) | 4 (内嵌 + **外部可调**) | **可被外部 AI 用** |
| 编译警告 | 0 | 0 | 持平 |
| 编译错误 | 0 | 0 | 持平 |
| GitHub 公开 | ✅ | ✅ | — |

### 测试明细（+3 MCP E2E）
- `McpServerTests.Server_Initialize_ReturnsServerInfo` — MCP 握手成功
- `McpServerTests.Server_ToolsList_Returns4Tools` — 4 个工具注册到 MCP server
- `McpServerTests.Server_ToolsCall_FindDuplicates_ReturnsResult` — 真实调用 find_duplicates 验证结果

---

## 🛠️ 技术细节

### 架构

```
┌─────────────────┐
│ Claude Desktop  │  (或 Cursor / Continue.dev / 任何 MCP client)
│   任何 AI 客户端 │
└────────┬────────┘
         │ stdio JSON-RPC (MCP 协议)
         ▼
┌─────────────────┐
│ DeskPilot.Mcp   │  ← 这次的 v0.3 新增
│  (stdio server) │
└────────┬────────┘
         │ 调 ITool.ExecuteAsync(JSON)
         ▼
┌─────────────────┐
│ DeskPilot.Core  │
│   4 个 ITool    │
└─────────────────┘
```

### 实现要点

- **`ModelContextProtocol 0.3.0-preview.4` SDK**
- 每个工具一个 `[McpServerTool]` 方法（强类型参数）
- **SDK 0.3 用 /// XML doc comment 描述参数**（无 `[Description]` attribute）
- 内部转 JSON 调 `ITool.ExecuteAsync` —— **零业务逻辑，全部复用现有工具**
- 日志走 `stderr`（不能走 stdout，会污染 JSON-RPC 协议）
- 工具响应：成功转 `OK: <summary>\n```json\n<data>\n````、失败转 `ERROR: <summary>`

---

## 🎯 杀手锏示例（外部 AI 客户端场景）

```
👤 用户 (在 Claude Desktop 里): 用 deskpilot 把 D:\downloads 重复的 PDF 找出来
🤖 Claude: 好的，我来调 deskpilot 的 find_duplicates 工具。

[Claude → MCP → DeskPilot.Mcp → FindDuplicatesTool]
[返回: 3 组重复 PDF，共 7 个文件，可清理 45.2 MB]

🤖 Claude: 找到 3 组重复 PDF：
  1. report_v1.pdf (3 份) — 2.3 MB × 2 = 4.6 MB
  2. invoice.pdf (2 份) — 1.2 MB × 1 = 1.2 MB
  3. handbook.pdf (2 份) — 39.4 MB × 1 = 39.4 MB

要删除哪些？我可以再调 deskpilot 的 move_files 把它们移到回收站目录。
```

---

## 📥 升级 / 安装

### 升级现有 v0.2.x

```bash
cd D:\opensource\DeskPilot
git pull origin master
git checkout v0.3.0
dotnet build DeskPilot.slnx
```

### 新接入 Claude Desktop

1. 编辑 `%APPDATA%\Claude\claude_desktop_config.json`
2. 加上 `mcpServers` 节点（见上文）
3. 重启 Claude Desktop

---

## 🐛 已知问题

- **Claude Desktop 配置只支持已发布的 MCP server**（`command` 指向 dll 路径），
  当前用 `dotnet run` 开发模式稍微慢一些（首次启动 ~3 秒）。
  v0.4 会发布预编译的 .exe
- 暂无

---

## 🗺️ 下一步（v0.4 规划）

- **🎨 主题/暗色模式**：Settings 切换 + 持久化
- **📊 使用统计**：本地记录 token 消耗 + 工具调用次数
- **🛠️ 更多工具**：`BatchResizeImageTool` / `ExtractArchiveTool` / `PdfMergeTool`
- **🚀 发布预编译 Mcp.exe**：避免 Claude Desktop 首次启动慢

---

## 🔗 相关链接

- 📖 [README](https://github.com/maqiul/DeskPilot#readme)
- 📝 [CHANGELOG](https://github.com/maqiul/DeskPilot/blob/v0.3.0/CHANGELOG.md)
- 🌐 [MCP 协议](https://modelcontextprotocol.io/)
- 🐛 [Issues](https://github.com/maqiul/DeskPilot/issues)
- 💬 [Discussions](https://github.com/maqiul/DeskPilot/discussions)

---

**Full Changelog**: https://github.com/maqiul/DeskPilot/compare/v0.2.0...v0.3.0
