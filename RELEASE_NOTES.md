## [v0.12.0] - 2026-06-26

### 🆕 技能多步工作流（A2）

技能不再只是「prompt + 工具声明」模板，而是真正的多步流水线：按顺序自动调用多个工具，失败可中断 / 可跳过，结果实时显示在聊天区上方的「执行步骤」卡片里。

#### 🧩 数据模型
- 新增 `SkillStep` record（ToolName + Args + Description + Optional）
- `Skill.Steps` + `IsMultiStep` 计算属性 + `SafeSteps` Null 安全回退

#### ⚙️ SkillExecutor
- `ISkillExecutor` + `StepStatus` 枚举（Pending / Running / Done / Error / Skipped）+ `StepProgress` 实时进度实体
- Optional 失败继续，Required 失败中断整体流程
- `IProgress<StepProgress>` 实时推送 + CancellationToken 取消支持

#### 💬 ChatViewModel 多步分支
- 触发逻辑：IsMultiStep 走 SkillExecutor；否则保留 v0.9 prompt 填入 + 自动发送
- 聊天区上方加橙色「执行步骤」SectionCard（步骤编号 + StatusIcon + ToolName + Description + Summary）

#### 🛠 3 个 community 改多步示例
- `scan-invoices`：HashFiles 校验 → ArchiveByDate 按月归档 → FindDuplicates 查重
- `weekly-report-helper`：HashFiles 校验 → BatchResizeImage 压缩配图
- `git-commit-message`：HashFiles 验证 → RenameByPattern dry-run 给 CHANGELOG 加日期前缀

> ⚠️ **关键校准**：Subtask 描述里建议的工具（FindFiles / ReadText / WriteText / RunCommand / SendToAI）实际不存在于当前 7 工具集，已用现有 7 工具（HashFiles / ArchiveByDate / FindDuplicates / BatchResizeImage / RenameByPattern）组合实现多步。

### 🆕 接 ClawHub / ModelScope 真后端（A1）

替换 v0.11 的 Stub 占位，三个独立公开市场源全部接真后端（独立 GitHub 仓库 mock，避免依赖外网复杂 OAuth）：

| 源 | BaseUrl | 实现 |
|----|---------|------|
| QwenPaw | `maqiul/DeskPilot/main/skills` | `SkillMarketService` 直连 |
| ClawHub | `maqiul/DeskPilot-clawhub/main/skills` | `ClawHubMarketService`（组合模式）|
| ModelScope | `maqiul/DeskPilot-modelscope/main/skills` | `ModelScopeMarketService`（组合模式）|

- mock 仓库：4 + 4 = 8 个演示技能（pdf-merge / video-compress / markdown-to-pdf / qrcode-generator / speech-to-text / text-summarize / image-colorize / doc-translate）
- README 10 列 Markdown 表格，与 QwenPaw 完全一致
- 真源 404 行为：抛 `MarketFetchException`（取代 v0.11 Stub 演示数据）

### 🆕 自定义市场源（A1.2）

SettingsWindow 市场源 Tab 行末新增「+ 自定义」按钮：

- 弹黄色输入条，输入名称（例：`MyHub`）+ GitHub raw URL（例：`https://raw.githubusercontent.com/owner/repo/main/skills`）
- 添加后自动切到新源并刷新市场列表
- 状态条提示「✅ 已添加」「⚠️ 已存在」「❌ URL 无效」
- 同名拒绝 + URL 必须以 `http(s)://` 开头 + 末尾 `/` 自动 Trim

### 📈 测试覆盖
- **223 测试**（v0.11 baseline 213 + v0.12 新增 24 - 删 1 旧 Stub + 5 个 A1.2 = 223）
- 全量 `dotnet test` 全过
- smoke test stdout 0 字节 = 无 XamlParseException

### 📥 下载

- **DeskPilot-Setup-v0.12.0-win-x64.exe**（自包含安装包，单文件 ~73 MB）
- **DeskPilot-v0.12.0-win-x64.zip**（自包含 ZIP，单文件 ~73 MB）
- 解压即用，无需安装 .NET 8 Desktop Runtime
- GitHub Release：https://github.com/maqiul/DeskPilot/releases/tag/v0.12.0

## [v0.11.0] - 2026-06-26

### 🆕 技能市场重做（QwenPaw 风格）

#### 🎨 视觉升级：列表 → 卡片网格

- **3 列 WrapPanel 卡片网格**（卡片宽 280 px），对齐 QwenPaw 截图风格
- **卡片极简化**：Icon 圆形背景大块 + 名称 + 描述 3 行截断 + 右上来源徽章
- **顶部多市场源 Tab**：QwenPaw / ClawHub / ModelScope（chip + 对勾样式）
- **二级分类 Tab**：全部 / 财务 / 文件 / 开发 / 图片 / 文档（横排 chips）
- **搜索框常驻右上角**
- **浅蓝色提示条**「选择分类或输入关键词以浏览 {源名} 中的技能」

#### 🔍 详情弹窗

- 点卡片 → `SkillDetailWindow` 弹窗
- Icon 大块 + 名称 / 版本 / 作者 / 分类 + ★ 评分 + 📥 下载数
- 完整 Description + Prompt 模板预览（只读 TextBox + 📋 复制按钮）
- Tools 列表（chips 横排）
- 安装 / 卸载按钮（根据 IsInstalled 切换）

#### 🛠 多市场源架构

- 新建 `IMarketplaceSourceService` / `MarketplaceSourceService`
  - `QwenPaw`（GitHub 真源）+ `ClawHub` / `ModelScope`（占位，v0.12 接真后端）
- `ISkillMarket` 加 `SourceName` 属性 + `SkillMarketService` 加 sourceName 参数（默认 "QwenPaw"）
- `App.xaml.cs` DI 注册：`AddHttpClient("skill-market")` + `AddSingleton<IMarketplaceSourceService>`
- `SettingsViewModel` 加 `MarketSourceNames` / `SelectedMarketSource` / `CurrentMarket` + 切换自动重拉

#### 📊 数据模型扩展

- `SkillManifest` 加 5 字段：`ScreenshotUrl` / `Rating` / `Downloads` / `AuthorUrl` / `AuthorName`（默认值兼容旧数据）
- `ParseIndexFromMarkdown` 升级：支持 7 / 8 / 9 / 10 列解析（向后兼容）
- `skills/README.md` 加 3 列 + 11 个技能填数据
- `MarketSkillRow` 加 6 字段
- `SourceMatchConverter` + `CategoryMatchConverter` 新增

### 📈 测试

- 新增 13 测试（`MarketplaceSourceTests` 8 个 + `MarketSkillRowTests` 5 个）
- **189 / 189 全过**（原 176 + 13 新增）

### 📥 下载

- `DeskPilot-App-v0.11.0-win-x64.zip` —— 自包含单文件（约 73 MB），解压即用
- `DeskPilot-Mcp-v0.11.0-win-x64.zip` —— MCP 服务器（约 7 MB）

---

## [v0.10.0]

### 🆕 技能市场系统

#### 🌐 从「只读内置」升级为「市场 + 本地安装」

- **🌐 技能市场**：GitHub 仓库根 `skills/` 目录（README.md 索引 + 单个 JSON 文件）
- **📦 安装 / 卸载 / 检查更新**：从市场拉取技能、安装到本地、一键卸载、版本对比自动提示更新
- **🖼️ SettingsWindow 技能市场页**：分类筛选 + 搜索 + 安装/卸载/检查更新按钮 + 🔄 更新角标
- **🛠 ChatWindow 横条升级**：内置+已安装合并 + 🔄 角标 + 📦 已安装数量标签

### 🔧 技术细节

- **`ISkillMarket` 接口 + `SkillMarketService` 实现**（HttpClient 注入，GitHub raw URL）
- **`ISkillService` 扩展**：`InstallAsync` / `UninstallAsync` / `CheckUpdatesAsync` / `BuiltIn` / `Custom`
- **`Skill` 模型扩展**：`IsBuiltIn` / `Source` / `Version` + 运行时 `HasUpdate` 角标（`[JsonIgnore]`）
- **市场新技能**：`scan-invoices`（财务办公）/ `weekly-report-helper`（文档处理）/ `git-commit-message`（开发工具）

### 📥 下载

| 文件 | 大小 | 说明 |
|------|------|------|
| `DeskPilot-App-v0.10.0-win-x64.zip` | ~73 MB | DeskPilot App 自包含单文件 exe |
| `DeskPilot-Mcp-v0.10.0-win-x64.zip` | ~7 MB | MCP Server 自包含（沿用 v0.9.0） |

> 完整变更历史见 [CHANGELOG.md](https://github.com/maqiul/DeskPilot/blob/master/CHANGELOG.md)

---

## [v0.9.2]

### 🐛 Bug 修复

#### IDE 启动 / `dotnet run` 时无界面（与 v0.9.1 release 闪退无关）
- **症状**：在 Visual Studio 按 F5 或 `dotnet run --project src/DeskPilot.App` 启动，进程存在但窗口不显示
- **根因**：WPF UI 线程构造函数里 sync-over-async 死锁
- **修复**：`LocalJsonMemoryStore.LoadAsync/SaveAsync` 全部加 `ConfigureAwait(false)` + `SemanticKernelChatService.LoadHistoryAsync` 用 `Task.Run()` 包住