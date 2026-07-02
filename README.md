# DeskPilot 🖥️✈️

> 你的桌面 AI 副驾驶 —— 让 AI 真正替你干活，不只是聊天。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Windows-0078D4)](https://github.com/dotnet/wpf)
[![Semantic Kernel](https://img.shields.io/badge/Semantic%20Kernel-AI-FF6F00)](https://github.com/microsoft/semantic-kernel)
[![Tools](https://img.shields.io/badge/tools-18-orange)](#-特性)
[![Tests](https://img.shields.io/badge/tests-296%20passed-brightgreen)](#)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen)](CONTRIBUTING.md)

**DeskPilot** 是一个基于 **.NET 8 + WPF** 的桌面 AI 助手，专为**办公场景**打造。
和 ChatGPT 套壳应用不同，DeskPilot 的 AI 可以**真正操作你的文件、处理你的文档、自动化你的日常任务**。

---

## ✨ 特性

- 💬 **智能问答** —— 支持 OpenAI / DeepSeek / Ollama（本地）等多模型
- 🔄 **动态模型列表** —— 设置窗口一键 🔄，自动从 Provider 拉取最新模型；离线/失败时用静态兜底
- 📄 **文档处理** —— 一句话完成 Excel 拆分、Word 转 PDF、PDF 合并/裁剪、批量重命名、图片旋转/裁剪
- 🗂️ **文件整理** —— AI 自动分类、整理、归档你的桌面文件夹
- 🧩 **技能中心（v0.15+）** —— 独立窗口（`Ctrl+Shift+K`）浏览/安装/卸载多步工作流技能
- 🛒 **多市场源（v0.11+）** —— 官方源 + 社区源 + 自定义源 三个 Tab 切换
- 🔌 **MCP Server（v0.5+）** —— 把 10 个核心工具通过 Model Context Protocol 暴露给 Claude Desktop / Cursor 等外部 AI
- 🔒 **隐私优先** —— 支持完全本地运行（Ollama），API Key 用 Windows DPAPI 加密
- 🌐 **中文优先** —— 原生支持中文界面和中文文档场景
- ⚡ **轻量快速** —— 原生 WPF，启动 < 2 秒，内存占用 < 200MB

---

## 🎬 演示场景

```
👤 你：把这个文件夹里所有发票 PDF 按月份归档

🤖 AI：好的，我扫描到 23 份 PDF。按发票日期归类如下：
       📁 2024-01/  (5份)
       📁 2024-02/  (8份)
       ...
       要执行吗？

👤 你：执行

🤖 AI：✅ 完成。已移动 23 个文件，跳过 2 个无日期文件。
```

```
👤 你：把这 50 张扫描件图片按 EXIF 时间重命名

🤖 AI：✅ 已读取 50 张图片的 EXIF DateTimeOriginal：
       DSC00001.jpg → 2024-01-15_14-30-00.jpg
       DSC00002.jpg → 2024-01-15_14-31-12.jpg
       ...
       已保存到原目录
```

---

## 🏗️ 技术栈

| 层 | 技术 |
|---|---|
| UI 框架 | WPF (.NET 8) |
| AI 编排 | Microsoft Semantic Kernel |
| MVVM | CommunityToolkit.Mvvm |
| 依赖注入 | Microsoft.Extensions.DependencyInjection |
| 配置管理 | Microsoft.Extensions.Configuration |
| 日志 | Serilog |
| 测试 | xUnit |
| 协议 | MCP (Model Context Protocol) SDK 0.3 |
| 文档处理 | PdfSharpCore 1.3.65（纯托管）+ ClosedXML 0.102.3 |
| 图标处理 | System.Drawing.Common（仅 Windows）|

---

## 📦 工具清单（共 18 个）

### Core 库（15 个）

| 分类 | 工具 | 说明 |
|------|------|------|
| 📄 PDF | `merge_pdfs` | 多 PDF 合并 |
| 📊 Excel | `batch_excel` | 批量 Excel 拆分/Sheet 提取/数据提取 |
| 🖼️ 图片 | `batch_resize_image` | 批量缩放 |
| 🖼️ 图片 | `convert_image` | 格式转换（JPG/PNG/GIF/BMP）|
| 🖼️ 图片 | `rotate_image` | 旋转（90/180/270）+ 翻转 |
| 🖼️ 图片 | `crop_image` | 矩形区域裁剪 |
| 🖼️ 图片 | `rename_by_exif` | 按 EXIF DateTimeOriginal 批量重命名 |
| 📁 文件 | `find_duplicates` | 按哈希找重复文件 |
| 📁 文件 | `move_files` | 批量移动（支持创建目标目录）|
| 📁 文件 | `rename_by_pattern` | 正则替换/前缀/后缀重命名 |
| 📁 文件 | `archive_by_date` | 按文件日期归档 |
| 📁 文件 | `hash_files` | SHA256/MD5 哈希 |
| 📦 归档 | `extract_archive` | ZIP/RAR 解压 |
| 🔍 搜索 | `search_content` | 文件内容正则搜索 |
| 📊 文本 | `text_stats` | 字符/行/词数统计 |

### MCP Server 暴露（11 个）

`merge_pdfs` / `batch_excel` / `find_duplicates` / `move_files` / `rename_by_pattern` / `archive_by_date` / `hash_files` / `extract_archive` / `search_content` / `text_stats` / `convert_image`

外部 AI 客户端（Claude Desktop / Cursor / Continue.dev）通过 stdio JSON-RPC 调用。

---

## 🚀 快速开始

### 环境要求

- **Windows 10 / 11**（WPF + System.Drawing 仅支持 Windows）
- **.NET 8 SDK**（[下载](https://dotnet.microsoft.com/download/dotnet/8.0)）
- （可选）[Ollama](https://ollama.com/) 用于本地模型

### 编译运行

```bash
git clone https://github.com/maqiul/DeskPilot.git
cd DeskPilot
dotnet restore DeskPilot.sln
dotnet build DeskPilot.sln --configuration Release
dotnet run --project src/DeskPilot.App
```

### 配置模型

DeskPilot 支持 **4 种配置方式**（按推荐度排序）：

#### 🥇 方式一：`.env` 文件（最推荐）

在项目根目录创建 `.env` 文件（参考 `.env.example`）：

```bash
# 复制模板
cp .env.example .env

# 编辑 .env，填入真实 Key
OPENAI_API_KEY=sk-xxxxxxxxxxxx
```

`.env` 已加入 `.gitignore`，不会被提交到 Git。

#### 🥈 方式二：.NET User Secrets（开发用，IDE 友好）

```bash
cd src/DeskPilot.App
dotnet user-secrets init
dotnet user-secrets set "AI:OpenAI:ApiKey" "sk-xxxxxxxxxxxx"
dotnet user-secrets set "AI:Provider" "OpenAI"
```

存到 `%APPDATA%\microsoft\UserSecrets\<项目ID>\secrets.json`，完全本地。

#### 🥉 方式三：环境变量

```powershell
# PowerShell（当前窗口）
$env:OPENAI_API_KEY = "sk-xxxxxxxxxxxx"

# PowerShell（永久）
[System.Environment]::SetEnvironmentVariable("OPENAI_API_KEY", "sk-xxx", "User")
```

#### ⚠️ 方式四：`appsettings.json`（不推荐）

直接编辑文件填 Key，但**文件可能误提交到 Git**。

### 切换 AI Provider

修改 `.env` 或 `appsettings.json` 中的 `AI:Provider`：

| Provider | Key 环境变量 | 备注 |
|----------|-------------|------|
| `OpenAI` | `OPENAI_API_KEY` | 海外服务，需科学上网 |
| `DeepSeek` | `DEEPSEEK_API_KEY` | 国内可用，便宜 |
| `Ollama` | 无需 Key | 本地模型，完全离线 |

> 💡 缺 Key 启动时会有友好弹窗提示，不用死记命令。

---

## 🔌 MCP Server 集成（v0.5+）

DeskPilot 内置 MCP Server，可被外部 AI 客户端调用：

```json
// Claude Desktop 配置示例
{
  "mcpServers": {
    "deskpilot": {
      "command": "dotnet",
      "args": ["run", "--project", "D:\\opensource\\DeskPilot\\src\\DeskPilot.Mcp"]
    }
  }
}
```

启动后，11 个工具（PDF 合并、Excel 处理、文件查找、批量重命名、EXIF 重命名等）可在 Claude Desktop / Cursor / Continue.dev 中直接调用。

---

## 📂 项目结构

```
DeskPilot/
├── src/
│   ├── DeskPilot.Core/           # 核心库（18 工具 + AI 编排）
│   │   ├── Tools/                # ITool 实现
│   │   ├── Services/             # 业务服务（Skill/Marketplace/Chat）
│   │   └── Models/               # 数据模型（record 类型）
│   ├── DeskPilot.Mcp/            # MCP Server（暴露 11 工具给外部 AI）
│   └── DeskPilot.App/            # WPF 应用
│       ├── Views/                # XAML 窗口
│       ├── ViewModels/           # MVVM ViewModel
│       ├── Converters/           # WPF 值转换器
│       └── Resources/            # 主题/图标
├── tests/                        # xUnit 测试（320 用例）
│   └── DeskPilot.Tests/
├── docs/                         # 文档
└── .github/                      # GitHub 配置（CI/CD）
```

---

## 🗺️ Roadmap

### ✅ 已完成（v0.0.1 → v0.16.4）

- [x] **v0.0-v0.4** —— 智能问答 + 多模型 + 本地记忆
- [x] **v0.5** —— MCP 协议支持 + 插件系统
- [x] **v0.6** —— 三层权限控制 + 危险操作确认
- [x] **v0.7** —— 本地记忆 + 对话历史
- [x] **v0.8** —— 主题切换（深色/浅色）
- [x] **v0.9** —— 技能系统（CRUD 模型）
- [x] **v0.10** —— 技能市场（GitHub 索引）
- [x] **v0.11** —— 多市场源架构
- [x] **v0.12** —— 技能多步工作流
- [x] **v0.13** —— 搜索/文本统计 2 工具
- [x] **v0.14** —— PDF 合并 + 图片转换 + Excel 批量 3 工具
- [x] **v0.14.1** —— WPF XamlParseException 修复
- [x] **v0.15** —— 独立技能中心窗口（Ctrl+Shift+K）
- [x] **v0.15.1** —— XamlParseException 热修复
- [x] **v0.16** —— F smoke test + B 图片工具 + E MCP +3 工具 + C SkillDetail 集成 + CI 修复
- [x] **v0.17** —— A 图片 EXIF 重命名工具 + 文档完整同步 + CI 修复第二步 + 6 个 doc-only 同步版本（v0.17.1→v0.17.6）
- [x] **v0.18** —— 系统托盘 NotifyIcon（关闭最小化 + 双击恢复 + 右键菜单退出）
- [x] **v0.19** —— 单实例 Mutex（防止多开 + 第二次启动激活旧窗口）
- [x] **v0.20** —— ChatWindow 标题栏显示版本号（v{X.Y.Z}）
- [x] **v0.21** —— 启动时间统计（OnStartup Console 输出）
- [x] **v0.22** —— 一键导出对话为 Markdown（文件菜单 → 导出）
- [x] **v0.23** —— 自动检查更新服务（GitHub Releases API + SemanticVersion 比较）
- [x] **v0.24** —— 对话历史搜索（实时关键词过滤，大小写不敏感）

### 🔜 下一步（v0.25+）

- [ ] **i18n 抽 resx** —— 全局多语言支持
- [ ] **PDF 拆分** —— 按页数/范围拆分 PDF
- [ ] **Skill 模板化** —— 用户可创建自定义技能
- [ ] **Markdown 预览** —— 聊天消息支持 Markdown 渲染

---

## 🧪 测试

```bash
# 跑全量测试
dotnet test DeskPilot.sln --configuration Release

# 当前状态：291/291 通过
```

测试覆盖：Core 工具（14 工具 × 4-6 用例）+ MCP Server（3 集成测试）+ Skill/Marketplace/ViewModel 业务逻辑。

---

## 🤝 贡献

欢迎所有形式的贡献！

- 🐛 [报告 Bug](https://github.com/maqiul/DeskPilot/issues/new?template=bug_report.md)
- 💡 [提出新功能](https://github.com/maqiul/DeskPilot/issues/new?template=feature_request.md)
- 📝 [改进文档](docs/)
- 🔧 [提交 PR](https://github.com/maqiul/DeskPilot/pulls)

详见 [CONTRIBUTING.md](docs/CONTRIBUTING.md)。

---

## 📄 许可证

本项目采用 [MIT 许可证](LICENSE)。

---

## 🙏 致谢

- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel) —— AI 编排框架
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) —— MVVM 工具包
- [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) —— MCP 协议实现
- [PdfSharpCore](https://github.com/ststeiger/PdfSharpCore) —— 纯托管 PDF 处理
- [ClosedXML](https://github.com/ClosedXML/ClosedXML) —— 纯 .NET Excel 处理

---

<p align="center">⭐ 如果这个项目对你有帮助，欢迎 Star 支持！</p>
