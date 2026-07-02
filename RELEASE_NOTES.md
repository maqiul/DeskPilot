## [v0.22.0] - 2026-07-01

### 🆕 一键导出对话为 Markdown

#### 功能
- 文件菜单 → 导出对话
- 弹出 SaveFileDialog，默认文件名 `deskpilot-yyyyMMdd-HHmmss.md`
- 输出 Markdown 格式（标题 + 时间 + 用户/AI 分组）

#### 验证
- ✅ 全量 307/307 测试通过
- ✅ smoke test EXIT 0

## [v0.21.0] - 2026-07-01

### 🆕 启动时间统计

`App.xaml.cs` 加静态 `Stopwatch`，`OnStartup` 完成后输出 `[DeskPilot] OnStartup completed in {ms}ms` 到 Console。

#### 验证
- smoke test 输出 `[DeskPilot] OnStartup completed in 1664ms`
- 全量 303/303 测试通过

## [v0.20.0] - 2026-07-01

### 🆕 ChatWindow 标题栏显示版本号

主窗口标题栏从静态 "DeskPilot 桌面 AI 助手" 改为动态绑定 "DeskPilot 桌面 AI 助手 v{X.Y.Z}"。

#### 实现
- `src/DeskPilot.App/ViewModels/ChatViewModel.cs` - 加 `WindowTitle` 属性
- `src/DeskPilot.App/Views/ChatWindow.xaml` - `Title="{Binding WindowTitle}"`

#### 验证
- ✅ 全量 303/303 测试通过
- ✅ smoke test EXIT 0

## [v0.19.0] - 2026-07-01

### 🆕 单实例 Mutex

DeskPilot 启动时检测是否已有实例运行，避免多开。

#### 功能
- **第一次启动**：正常创建主窗口
- **第二次启动**：检测到 Mutex 已存在 → 自动激活旧窗口 + 退出当前进程
- **应用退出时清理 Mutex**

#### 实现
- `src/DeskPilot.App/Services/SingleInstanceService.cs` - 新建（Mutex + Win32 ShowWindow/SetForegroundWindow）
- `src/DeskPilot.App/GlobalUsings.cs` - 加 Mutex 别名
- `src/DeskPilot.App/App.xaml.cs` - OnStartup 最开始加 Mutex 检查

#### 验证
- ✅ 全量 301/301 测试通过
- ✅ smoke test EXIT 0

## [v0.18.0] - 2026-07-01

### 🆕 系统托盘 NotifyIcon

WPF App 关闭时最小化到 Windows 系统托盘，而不是直接退出进程。

#### 功能
- **关闭主窗口 → 最小化到托盘**
- **双击托盘图标 → 恢复窗口**（自动取消最小化 + 激活到前台）
- **托盘右键菜单**：「显示主窗口」+「退出」
- **应用退出时清理托盘**（避免图标残留）

#### 实现
- `src/DeskPilot.App/DeskPilot.App.csproj` - 加 `<UseWindowsForms>true</UseWindowsForms>`
- `src/DeskPilot.App/GlobalUsings.cs` - 新建（12 个全局 using 别名解决 WinForms 命名冲突）
- `src/DeskPilot.App/Services/TrayIconService.cs` - 新建（NotifyIcon 包装 + 双击恢复 + 右键菜单）
- `src/DeskPilot.App/Views/ChatWindow.xaml.cs` - 加 `SetTrayIcon()` + `Closing` event → 取消 + 隐藏
- `src/DeskPilot.App/App.xaml.cs` - 主分支 + smoke test 分支都注入 TrayIcon + Exit 清理

#### 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 297/297 测试通过
- ✅ smoke test EXIT 0

#### 踩坑
- `<UseWindowsForms>` 引入 WinForms 全局命名空间污染 → `GlobalUsings.cs` 用 `global using` 别名修复
- `SystemIcons` 在 `System.Drawing` 而非 `System.Windows.Forms`

## [v0.17.4] - 2026-07-01

### 📝 README.md Roadmap 详细化

README.md v0.17 Roadmap 单行勾选扩展为「EXIF + 文档同步 + CI 修复第二步」。

- 1 行新增 + 1 行删除
- 配套 commit `a444fc0` + tag `v0.17.4`

无代码变更，纯文档细节同步。

## [v0.17.3] - 2026-07-01

### 📝 CHANGELOG 同步 v0.17.2 release.yml 修复

CHANGELOG.md v0.17.2 section 完整记录 v0.16.4 CI 修复事故第二步（release.yml `.slnx` → `.sln` + RELEASE_NOTES.md 同步 v0.17 sections）。

- 34 行新增
- 配套 commit `e434201` + tag `v0.17.3`

无代码变更，纯文档完整性同步。

## [v0.17.1] - 2026-07-01

### 📝 文档同步

CHANGELOG.md v0.17.0 section 末尾追加 README.md 同步说明（commit `c9c7318`）。

- 18 行新增
- 记录 v0.17.0 README 同步背景（badge 17→18、工具表缺 `rename_by_exif`、Roadmap 未勾选）+ 修复 + 同步内容
- 配套 commit `bd66174` + tag `v0.17.1`

无代码变更，纯文档完整性同步。

## [v0.17.0] - 2026-07-01

### 🆕 RenameByExifTool 图片 EXIF 批量重命名

新增第 18 个工具：按图片 **EXIF DateTimeOriginal**（拍摄时间）批量重命名。适用于「相机/手机照片按拍摄时间整理」「扫描件按 EXIF 时间重命名」等场景。

#### 工具参数
| 参数 | 类型 | 必填 | 默认 | 说明 |
|------|------|------|------|------|
| `directory` | string | ✅ | - | 目标目录绝对路径 |
| `pattern` | string | ❌ | `*.jpg` | glob 过滤 |
| `dateFormat` | string | ❌ | `yyyy-MM-dd_HH-mm-ss` | 日期格式 |
| `prefix` | string | ❌ | - | 可选前缀 |
| `fallbackToFileDate` | bool | ❌ | `true` | 无 EXIF 时是否用文件修改时间 |
| `dryRun` | bool | ❌ | `false` | true 只预览不重命名 |

#### 示例
- `DSC00001.jpg`（拍摄于 2024-06-15 14:30:00）→ `2024-06-15_14-30-00.jpg`
- 或带前缀：`IMG_2024-06-15_14-30-00.jpg`

#### 实现
- `src/DeskPilot.Core/Tools/RenameByExifTool.cs`（9644 bytes）
- 使用 `System.Drawing.Common`（v0.5 已引入，零新依赖）
- EXIF PropertyItem 0x9003 = DateTimeOriginal
- 支持 JPG/JPEG/PNG（PNG 无 EXIF 自动 fallback 到文件修改时间）
- 冲突解决同 `RenameByPatternTool`（`_2` / `_3` 后缀）
- 5 个新测试，全部通过

#### 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 296/296 测试通过（v0.16 291 + 5 新增）
- ✅ WPF App smoke test PASSED（exit 0）

## [v0.16.3] - 2026-06-27

### 🐛 CI Hotfix - `.slnx` 转回 `.sln`

修复 v0.15.0 → v0.16.3 的 6 个 Release workflow 全部失败的问题——`.slnx` 是 .NET 9 SDK 引入的新格式，.NET 8 SDK 不支持。转回 `.sln` 格式后 CI 重新工作。

#### 根因
- `DeskPilot.slnx` 是 .NET 9 SDK 引入的 XML 解决方案文件
- CI runner 用 `dotnet-version: 8.0.x`（.NET 8 SDK）
- .NET 8 SDK 报 `error MSB4068: 无法识别元素 <Solution>`
- 本地装 .NET 8.0.100 SDK 100% 重现

#### 修复
- 删除 `DeskPilot.slnx`，新建 `DeskPilot.sln`（传统 .NET 兼容格式）
- 强制更新 tag `v0.16.3` 触发新 workflow

#### 验证
- .NET 8 SDK 本地 build 0 错误 + 291/291 测试全过

### 🆕 SkillDetailWindow 集成进 SkillCenterWindow Market Tab

修复 v0.15 D3 Market Tab 卡片没详情入口的痛点——点击卡片不再只能直接安装，而是弹出 SkillDetailWindow 查看完整技能详情（Description + Prompt 预览 + Tools 列表 + 安装/卸载/检查更新）。

#### 变更
- SkillCenterWindow.xaml Market Tab 卡片 Border 加 `MouseLeftButtonUp="MarketSkillCard_Click"` + `Tag="{Binding Id}"` + `Cursor="Hand"`
- SkillCenterWindow.xaml.cs 加 `MarketSkillCard_Click` handler：通过 `App.Services` 拿 `ISkillService` + `ISkillMarket` → 创建 `SkillDetailViewModel` → `ShowDialog()`
- SkillCenterWindowTests.cs 加 3 个新测试（XAML 验证 + cs 验证 + DI 验证）

#### 架构亮点
- **不污染 ViewModel**：通过 `App.Services.GetService<>()` 拿 service，不改 SkillCenterViewModel
- **依赖复用**：复用 v0.11 已存在的 SkillDetailWindow + SkillDetailViewModel
- **owner 模式**：`{ Owner = this }` 让详情窗跟着 SkillCenterWindow 关闭

#### 验证
- 291/291 测试全过（v0.16.2 baseline 288 + 3 新增）
- smoke test EXIT 0

## [v0.16.2] - 2026-06-27

### 🆕 MCP Server 新增 3 个工具（7 → 10）

把 v0.13/v0.14 的 3 个工具通过 MCP 协议暴露给外部 AI 客户端（Claude Desktop / Cursor / Continue.dev）：

- `merge_pdfs` - 把多份 PDF 合并为一份新 PDF
- `convert_image` - 把图片从源格式转换为目标格式
- `text_stats` - 统计文本文件的字符数/单词数/行数

MCP 工具总数：v0.5 4 → 7 → **v0.16.2 10**

#### 验证
- 288/288 测试全过（v0.16.1 baseline 288 + 0 新增）
- smoke test 0 字节

## [v0.16.1] - 2026-06-27

### 🆕 图片旋转/裁剪 2 个新工具

#### 🔄 rotate_image（图片旋转 + 翻转）
- `rotation: 0/90/180/270` 旋转 + `flip: none/horizontal/vertical` 翻转
- 旋转 + 翻转可同时指定（9 种组合）
- 输出格式自动从 outputPath 后缀推断

#### ✂️ crop_image（图片裁剪）
- 输入 `(x, y, width, height)` 矩形区域
- 超出源图片边界的部分自动截断
- 输出格式自动从 outputPath 后缀推断

#### 验证
- 288/288 测试全过（v0.16.0 baseline 280 + 8 新增）
- smoke test 0 字节

## [v0.15.0] - 2026-06-27

### 🆕 独立技能中心窗口

从 SettingsWindow 的技能市场 Tab 抽出来，做成独立的 SkillCenterWindow 窗口：标题 + 3 Tab（市场 / 已安装 / 有更新）+ 底部状态栏。ChatWindow 顶部加菜单条 + Ctrl+Shift+K 全局快捷键打开。

#### 🪟 SkillCenterWindow（900x640 主窗口）
- 3 个 TabItem：🌐 技能市场 + 📦 已安装 + 🔄 有更新
- 标题栏：🛠 技能中心 + 「浏览 / 安装 / 管理 DeskPilot 的所有技能（内置 + 市场）」
- 底部状态栏：实时 StatusMessage + 当前选中市场源徽章
- Melon 风格（橙底白字选中态 TabItem + 圆角卡片）

#### 🛒 技能市场 Tab
- 源 Tab 横排：QwenPaw / ClawHub / ModelScope + 用户自定义（RadioButton + StackPanel Horizontal）
- 分类 chips：预设 8 个（全部 / 财务办公 / 文件整理 / 开发工具 / 图片处理 / 文档处理 / 办公自动化 / 示例）
- 搜索框：实时双向绑定 MarketSearchText + Delay=300 防抖
- 刷新按钮：🔄 刷新市场 → LoadMarketCommand
- 3 列 WrapPanel 卡片网格：每张 280px 圆角 10（Icon 圆形 40px + Name/Author/SourceName 徽章 + Description 3 行截断 + ⭐ 评分 + 📥 下载数 + 版本 + 分类 + ✅ 已安装标识）

#### 📦 已安装 Tab
- ListView 6 列：图标 + 技能名 + 版本 + 分类 + 作者 + 操作（🗑 卸载按钮）
- 数据源：InstalledSkills ObservableCollection 订阅 ISkillService.SkillsChanged 自动 RefreshInstalled
- 操作栏：🔄 刷新列表 + 💡「内置技能不可卸载」提示

#### 🔄 有更新 Tab
- ListView 4 列：技能 ID + 本地版本 + 最新版本 + 操作（⬆ 一键更新按钮）
- 数据源：UpdateAvailableSkills（ISkillService.CheckUpdatesAsync → 过滤 HasUpdate=true）
- 操作栏：🔄 检查更新 + 💡「仅显示有更新的技能」提示

#### 🧠 SkillCenterViewModel（7 个 RelayCommand 业务）
- 注入 IMarketplaceSourceService + ISkillService 2 服务
- 5 个 ObservableProperty：StatusMessage / SelectedMarketSource / MarketCategory / MarketSearchText / IsLoadingMarket
- 3 个 ObservableCollection：MarketSkillRows / InstalledSkills / UpdateAvailableSkills
- 命令：LoadMarket / LoadInstalled / LoadUpdates / Install / Uninstall / UpdateSkill / RefreshStatus
- SkillsChanged 事件订阅 → 自动 RefreshInstalled

#### 🍱 ChatWindow 顶部菜单
- 新建 `<Menu Grid.Row="0">`：文件（设置 / 退出）+ 技能（打开技能中心 Ctrl+Shift+K / 刷新已安装技能）+ 帮助（关于 DeskPilot）
- ChatWindow.xaml.cs 加 ExitMenuItem_Click（Application.Current.Shutdown）+ AboutMenuItem_Click（MessageBox 显示版本 + GitHub URL）
- Window.InputBindings Ctrl+Shift+K 调 ShowSkillCenterCommand
- ChatViewModel.ShowSkillCenter 用 SkillCenterWindow 工厂 delegate 注入 → MVVM 纯净
- App.xaml.cs 主分支 + smoke test 分支 ChatViewModel DI 工厂注入 SkillCenterWindow

### 📈 测试覆盖
- **280 测试全过**（v0.14.1 baseline 259 + v0.15 新增 21 = 280）
  - SkillCenterWindowTests 3 个（D1 基础结构）
  - SkillCenterViewModelTests 5 个（D2 业务逻辑 + Stub 服务）
  - SkillCenterMarketTabTests 7 个（D3 XAML 文本验证）
  - SkillCenterIntegrationTests 6 个（D4 Menu + 快捷键 + 3 Tab 完整集成）
- smoke test stdout/stderr 0 字节 = 无 XamlParseException（SkillCenterWindow 22582 bytes XAML 全量解析成功）

### 🔧 关键决策
- **MVVM 纯净**：`ChatViewModel.ShowSkillCenter` 用 `Func<SkillCenterWindow>?` 工厂 delegate 注入，避免 ViewModel 直接 new Window
- **避 STA 线程问题**：XAML 验证测试改用 `File.ReadAllText` + `Regex.Match` + `Assert.Contains`，不用 `XamlReader.Parse`
- **RelayCommand 生成名规则**：`async Task XxxAsync` → `XxxCommand`（去 Async 后缀）
- **Positional record 必须用构造语法**：`SkillManifest` / `Skill` 是 positional record（13/12 参），不能用对象初始化器

### 📥 下载

- **DeskPilot-Setup-v0.15.0-win-x64.exe**（自包含安装包，单文件 ~73 MB）
- **DeskPilot-v0.15.0-win-x64.zip**（自包含 ZIP，单文件 ~73 MB）
- 解压即用，无需安装 .NET 8 Desktop Runtime
- GitHub Release：https://github.com/maqiul/DeskPilot/releases/tag/v0.15.0

---

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
