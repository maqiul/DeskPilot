## 🎉 DeskPilot v0.5.1 — AI 流式输出 + CI 启动验证

### ⚡ AI 流式输出（打字机效果）

AI 回复现在像 ChatGPT 一样逐字"蹦"出来，不再是一大段突然冒出。

- **IChatService** 新增 `ChatStreamAsync`（`IAsyncEnumerable<string>` 逐 token 返回）
- **SemanticKernelChatService** 用 SK 的 `GetStreamingChatMessageContentsAsync` + `FunctionChoiceBehavior.Auto()` — Tool Calling 自动处理，工具先内部执行，后流式输出最终 LLM 回复
- **ChatViewModel** 改为先插入空 assistant 气泡 → `await foreach` 逐片追加 → 打字机效果
- **取消键优化**：取消后消息气泡保留已输出的内容 + `⏸️ 已取消`（而不是吞掉整条消息）

### 🛡️ CI 启动 smoke test（防 XAML 崩溃回归）

防止 v0.0.3 → v0.5.0 期间存在的 `XamlParseException` 再次出现。

- `DESKPILOT_SMOKE_TEST=1` 环境变量触发简化启动路径
- `StubChatService`：不调 AI，直接走完 **XAML 解析 → DI 注入 → 窗口创建** 全链路
- 自动 `Shutdown(0)` 退出：exit 0 = 通过，exit 2 = 崩溃
- `ci.yml` 改用 `Start-Process -Wait` + exit code 检测（替代旧的手动 kill）

### 🔧 其他改进

- `IChatService` 继承 `IDisposable`（统一生命周期管理）
- 修复 CI workflow 分支监听：`main` → `master`（之前 CI 从未触发过）
- `StubChatService` 实现完整 `IChatService` 接口（含流式方法）

### 📦 下载

| 文件 | 说明 |
|------|------|
| `DeskPilot-App-v0.5.1-win-x64.zip` | DeskPilot App（需要 .NET 8 运行时） |
| `DeskPilot-Mcp-v0.5.1-win-x64.zip` | MCP Server（自包含，无需运行时） |

> 完整变更历史见 [CHANGELOG.md](https://github.com/maqiul/DeskPilot/blob/master/CHANGELOG.md)
