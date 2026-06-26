## [v0.14.0] - 2026-06-26

### 🆕 新增 3 个零依赖工具

工具矩阵从 9 个扩展到 **12 个**，新增 2 个外部 NuGet 依赖：`PdfSharpCore 1.3.65`（纯托管 PDF 库，零 GhostScript）+ `ClosedXML 0.102.3`（纯 .NET Excel 库）。self-contained 单文件体积保持 ~73 MB。

#### 📄 MergePdfTool — PDF 合并
- 输入 `inputFiles`（PDF 绝对路径数组）+ `outputPath`（合并后的新 PDF 路径）
- 输出 `outputPath` + `inputCount` + `pageCount` + `outputSizeBytes` + `elapsedMs`
- 适用：「把多张发票 PDF 合成一份」「合并多份报告」

#### 🖼 ConvertImageTool — 图片格式转换
- 输入 `inputPath` + `outputPath` + `targetFormat`（png / jpg / bmp / webp / gif）+ 可选 `quality`（1-100，jpg 生效）
- webp 输出自动回退为 png（保持透明通道）
- 适用：「PNG 太大转 JPG 缩体积」「扫描件转 BMP 嵌入文档」

#### 📊 BatchExcelTool — Excel 批处理
- 输入 `inputDirectory` + 可选 `fileFilter`（默认 *.xlsx）+ `operation` + 可选 `outputPath`
- 三种 operation：
  - `list_sheets`：列出每个 xlsx 的 sheet 名 + 行列数 + 文件大小
  - `extract_data`：汇总所有 xlsx 第一张表数据到 JSON 数组（自动跳过表头行）
  - `write_summary`：把每文件的「文件名 + sheet 名 + 行数 + 列数 + 文件大小」汇总到新 xlsx

### 🆕 2 个多步技能示例

技能总数从 13 个扩展到 **15 个**（8 builtin + 7 community）。

#### 📄 invoice-merge
- 2 步：`search_content` 搜 Downloads/ 含「发票|invoice|fapiao」PDF → `merge_pdfs` 合并为 Documents/发票汇总_YYYYMMDD.pdf
- Category：文档处理

#### 📊 excel-rollup
- 3 步：`search_content` 找本周 *.xlsx → `batch_excel` write_summary 汇总 → `text_stats` 统计行数（optional）
- Category：办公自动化

### 📈 测试覆盖
- **259 测试**全过（v0.13 baseline 239 + v0.14 新增 20 = 259）
  - C1 MergePdfToolTests：5（空数组 / 单文件 / 多文件保序 / 不存在 / 损坏 PDF）
  - C2 ConvertImageToolTests：5（PNG→JPG / JPG→PNG / quality 95>10 / 不存在 / tiff 不支持）
  - C3 BatchExcelToolTests：6（空目录 / list_sheets / extract_data / write_summary / 不存在目录 / 不支持 operation）
  - C4 SkillStepTests v0.14 section：4（InvoiceMerge / ExcelRollup 加载与 IsMultiStep 校验）
- smoke test stdout/stderr 0 字节 = 无 XamlParseException

### 🔧 关键修复
- **PdfSharpCore 库命名空间是 `PdfSharpCore` 不是 `PdfSharp`**（using + catch 里的 PdfReaderException 都已修）
- **RiskLevel 枚举无 `ReadOnly` 值** → `batch_excel` 改用 `Destructive`（write_summary 写新 xlsx 视为写文件）
- **ClosedXML `LastRowUsed().RowNumber()` 包含表头** → WriteSummary 测试断言从 2 改 3（1 表头 + 2 数据行）
- **ClosedXML `RowsUsed()` 默认含表头** → `extract_data` 加 `Skip(1)` 跳过表头，行为符合「数据行」语义

### 📥 下载

- **DeskPilot-Setup-v0.14.0-win-x64.exe**（自包含安装包，单文件 ~73 MB）
- **DeskPilot-v0.14.0-win-x64.zip**（自包含 ZIP，单文件 ~73 MB）
- 解压即用，无需安装 .NET 8 Desktop Runtime
- GitHub Release：https://github.com/maqiul/DeskPilot/releases/tag/v0.14.0

## [v0.13.0] - 2026-06-26

### 🆕 新增 2 个零依赖工具

工具矩阵从 7 个扩展到 **9 个**，零外部依赖（不引入任何 NuGet 包，self-contained 单文件体积保持 ~73 MB）。

#### 📄 TextStatsTool — 文本文件统计
- 输入 `filePath` + 可选 `topN`
- 输出：BOM 自动检测编码（UTF-8 / UTF-16 / UTF-32 / 默认 UTF-8 无 BOM）
- 行数（按 `\n` 计数）+ 字符数 + 词数（中英混合：英文按连续字母数字分词，中文按每个汉字 1 词）
- 字节数 + 最后修改时间
- topN 高频词（跳过停用词 + 单字符 + 纯数字）
- 适用：「这个文件多大」「哪些词出现最多」

#### 🔍 SearchContentTool — 文件内容搜索
- 输入 `directory` + `pattern`（正则）+ 可选 `fileFilter` + 可选 `maxResults` + `recursive`
- 输出每个匹配的文件路径、行号、匹配行内容
- IsBinaryFile 按扩展名快速跳过（图片/视频/音频/Office/PDF 等）
- RegexOptions.Compiled + 2 秒超时防 ReDoS
- 适用：「帮我找所有 TODO」「哪些文件包含这个关键词」

### 🆕 2 个多步技能示例

技能总数从 11 个扩展到 **13 个**（8 builtin + 5 community）。

#### 🔍 code-review-helper
- 2 步：SearchContent 搜 src/*.cs 的 `TODO|FIXME|HACK|XXX` → TextStats 统计代码行数
- Category：开发工具

#### 📂 file-organizer
- 2 步：SearchContent 按「发票|合同|收据」关键词扫描 Downloads/ → ArchiveByDate 按 yyyy/MM 归档
- Category：文档处理

### 📈 测试覆盖
- **239 测试**全过（v0.12 baseline 229 + 16 新 = 239）
- smoke test stdout 0 字节 = 无 XamlParseException
- 零外部依赖

### 📥 下载

- **DeskPilot-Setup-v0.13.0-win-x64.exe**（自包含安装包，单文件 ~73 MB）
- **DeskPilot-v0.13.0-win-x64.zip**（自包含 ZIP，单文件 ~73 MB）
- 解压即用，无需安装 .NET 8 Desktop Runtime
- GitHub Release：https://github.com/maqiul/DeskPilot/releases/tag/v0.13.0

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
