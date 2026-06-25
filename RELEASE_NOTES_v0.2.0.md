# 🚀 DeskPilot v0.2.0 — 4 工具矩阵 + UI 进度展示

> **发布日期：** 2026-06-25
> **Commit：** `1796c5a`
> **测试：** 104/104 ✅
> **下载：** Source code (zip) / (tar.gz)

---

## 🎉 这是什么版本？

v0.2.0 是 DeskPilot 的**第二个里程碑版本**，重点突破：

- **从 1 个工具扩到 4 个工具**——AI 真正能干更多活
- **UI 状态栏实时显示工具调用**——告别"AI 在干啥"黑盒
- **完整 E2E 验证**——所有工具在真实文件系统上端到端跑通

---

## ✨ 新增功能

### 🛠️ 3 个新 MCP 工具

| 工具 | AI 调用名 | 能力 |
|------|----------|------|
| **MoveFilesTool** | `move_files` | 批量移动文件 + glob 过滤 + 递归子目录 + collision 处理 |
| **FindDuplicatesTool** | `find_duplicates` | SHA256 找内容重复文件 + 报告浪费空间 + 最小文件大小过滤 |
| **RenameByPatternTool** | `rename_by_pattern` | 正则替换（支持 `$1`/`$2`）+ 前缀/后缀 + DryRun 预览 |

加上 v0.1 的 `ArchiveByDateTool`（按日期归档），DeskPilot 现在有 **4 个工具** 覆盖日常文件管理全场景。

### 📊 UI 进度展示（杀手锏增强）

之前：用户说"归档文件" → AI 沉默几秒 → 突然吐结果
现在：用户说"归档文件" → 状态栏立刻显示 **🔧 正在调用 archive_files_by_date...** → 完成后变成 **✅ archive_files_by_date 完成 (87ms)** → 然后 AI 自然语言总结

实现细节：
- 用 SK 1.32 推荐的 `IFunctionInvocationFilter`（替代过时 events）
- 状态栏带 ⚙️ 图标 + 蚂蚁灰文字 + 顶部边框
- 空状态自动隐藏（`StringToVisibilityConverter`）

### 🧪 DeskPilot.Verify 离线验证

无需 API Key，一键验证 4 个工具在真实文件系统上的端到端行为：

```bash
dotnet run --project src/DeskPilot.Verify -- "D:\test_files"
dotnet run --project src/DeskPilot.Verify -- "D:\test_files" --no           # DryRun
dotnet run --project src/DeskPilot.Verify -- "D:\test_files" --tool move    # 只测 move
```

E2E 实测（D:\deskpilot_e2e_test）：

```
✅ ArchiveByDateTool:   扫描 3, 移动 3, 失败 0
✅ MoveFilesTool:       扫描 3, 移动 3, 失败 0
✅ FindDuplicatesTool:  扫描 3, 重复组 1, 重复文件 2
✅ RenameByPatternTool: 扫描 3, 重命名 3, 失败 0
```

---

## 📊 数字说话

| 指标 | v0.1.2 | v0.2.0 | 变化 |
|------|--------|--------|------|
| 测试数 | 73 | **104** | **+31** |
| MCP 工具 | 1 | **4** | **+3** |
| 编译警告 | 0 | 0 | 持平 |
| 编译错误 | 0 | 0 | 持平 |
| 项目数 | 4 | 4 | 持平 |
| GitHub 公开 | ✅ | ✅ | — |

---

## 🔧 升级指南

如果你已经用过 v0.1.x：

```bash
cd D:\opensource\DeskPilot
git pull origin master
git checkout v0.2.0
run.bat
```

**破坏性变更**：无。新增的 3 个工具默认不调用，只在 AI 觉得需要时才调。

---

## 🎯 杀手锏示例

```
👤 用户：把 D:\发票 按月归档
🤖 AI 调 archive_files_by_date(sourceDirectory="D:\发票", granularity="Month")
📊 UI 状态栏：🔧 正在调用 archive_files_by_date...
🛠️ 工具：扫描 → 分组 → 移动 → 报告
📊 UI 状态栏：✅ archive_files_by_date 完成 (87ms)
🤖 AI：已成功归档 23 个发票文件，按月分到 2026-05 和 2026-06 子目录
```

```
👤 用户：桌面上有重复的照片吗？
🤖 AI 调 find_duplicates(directory="C:\Users\me\Desktop", pattern="*.jpg")
📊 UI 状态栏：🔧 正在调用 find_duplicates...
🛠️ 工具：扫描 → SHA256 哈希 → 找重复
📊 UI 状态栏：✅ find_duplicates 完成 (1234ms)
🤖 AI：发现 3 组重复照片，共 8 个文件，可清理 45.2 MB
```

```
👤 用户：把这批 IMG_*.jpg 改成 vacation_*.jpg
🤖 AI 调 rename_by_pattern(directory="...", find="IMG_", replace="vacation_")
📊 UI 状态栏：🔧 正在调用 rename_by_pattern...
🛠️ 工具：扫描 → 正则替换 → 改名
📊 UI 状态栏：✅ rename_by_pattern 完成 (45ms)
🤖 AI：已重命名 23 个文件
```

---

## 🐛 已知问题

无。当前所有功能都已通过单元测试 + E2E 验证。

---

## 🗺️ 下一步（v0.3 规划）

- **🌐 MCP Server 封装**：用 `ModelContextProtocol` NuGet 把工具暴露给 Claude Desktop / Cursor
- **🎨 主题/暗色模式**：Settings 切换 + 持久化
- **📊 使用统计**：本地记录 token 消耗 + 工具调用次数
- **🛠️ 更多工具**：`BatchResizeImageTool` / `ExtractArchiveTool`

---

## 🙏 致谢

- [Semantic Kernel](https://github.com/microsoft/semantic-kernel) — AI 编排引擎
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM 框架
- [DotNetEnv](https://github.com/tonerdo/dotnet-env) — .env 文件加载
- [xUnit](https://xunit.net/) — 测试框架
- 蚂蚁金服设计语言 — UI 配色灵感

---

## 📥 下载

- **Source code (zip):** https://github.com/maqiul/DeskPilot/archive/refs/tags/v0.2.0.zip
- **Source code (tar.gz):** https://github.com/maqiul/DeskPilot/archive/refs/tags/v0.2.0.tar.gz

## 🔗 相关链接

- 📖 [README](https://github.com/maqiul/DeskPilot#readme)
- 📝 [CHANGELOG](https://github.com/maqiul/DeskPilot/blob/v0.2.0/CHANGELOG.md)
- 🛣️ [ROADMAP](https://github.com/maqiul/DeskPilot/blob/v0.2.0/docs/ROADMAP.md)
- 🐛 [Issues](https://github.com/maqiul/DeskPilot/issues)
- 💬 [Discussions](https://github.com/maqiul/DeskPilot/discussions)

---

**Full Changelog**: https://github.com/maqiul/DeskPilot/compare/v0.1.2...v0.2.0
