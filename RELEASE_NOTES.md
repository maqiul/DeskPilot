## [v0.11.0]

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