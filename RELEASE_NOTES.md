## 🐛 DeskPilot v0.9.2 — 修 IDE 启动无界面死锁

### 修了啥

**在 Visual Studio 按 F5 或 `dotnet run --project src/DeskPilot.App` 启动，进程存在但窗口不显示**——和 v0.9.1 release 闪退是**两个独立 bug**。

**根因**：WPF UI 线程构造函数里 sync-over-async 死锁：
1. `App.OnStartup`（UI 线程）→ `GetRequiredService<ChatWindow>()` → DI 解析链 → `SemanticKernelChatService..ctor`
2. `..ctor` 调 `LoadHistoryAsync()` → `_memoryStore.LoadAsync().Wait()`
3. `LoadAsync()` 内 `await File.ReadAllTextAsync(StorePath, ct)`（默认 `ConfigureAwait(true)`）
4. 异步 I/O 完成后回调想回 SyncContext（UI 线程），但 UI 线程在 `.Wait()` 里死等
5. **死锁**

**定位方法**：`dotnet-dump collect -p <pid>` + `clrstack` 看主线程在 `SynchronizationContext.WaitHelper` 死等。

**修复（2 文件 / 6 行变更）**：
- `LocalJsonMemoryStore.LoadAsync` / `SaveAsync` 全部加 `.ConfigureAwait(false)`
- `SemanticKernelChatService.LoadHistoryAsync` 把 `LoadAsync()` 包进 `Task.Run()`

---

## 🌐 DeskPilot v0.10.0 — 技能市场 + 本地安装

### 新增功能

**从「只读内置 + 启用切换」升级为「市场 + 本地安装」模型**

- **🌐 技能市场**：GitHub 仓库根 `skills/` 目录（README.md 索引 + 单个 JSON 文件）
- **📦 安装 / 卸载 / 检查更新**：从市场拉取技能、安装到本地、一键卸载、版本对比自动提示更新
- **🖼️ SettingsWindow 技能市场页**：分类筛选 + 搜索 + 安装/卸载/检查更新按钮 + 🔄 更新角标
- **🛠 ChatWindow 横条升级**：内置+已安装合并 + 🔄 角标 + 📦 已安装数量标签

### 技术细节

- **`ISkillMarket` 接口 + `SkillMarketService` 实现**（HttpClient 注入，GitHub raw URL）
- **`ISkillService` 扩展**：`InstallAsync` / `UninstallAsync` / `CheckUpdatesAsync` / `BuiltIn` / `Custom`
- **`Skill` 模型扩展**：`IsBuiltIn` / `Source` / `Version` + 运行时 `HasUpdate` 角标（`[JsonIgnore]`）
- **市场新技能**：`scan-invoices`（财务办公）/ `weekly-report-helper`（文档处理）/ `git-commit-message`（开发工具）

### 下载

| 文件 | 大小 | 说明 |
|------|------|------|
| `DeskPilot-App-v0.10.0-win-x64.zip` | ~73 MB | DeskPilot App 自包含单文件 exe |
| `DeskPilot-Mcp-v0.10.0-win-x64.zip` | ~7 MB | MCP Server 自包含（沿用 v0.9.0） |

> 完整变更历史见 [CHANGELOG.md](https://github.com/maqiul/DeskPilot/blob/master/CHANGELOG.md)