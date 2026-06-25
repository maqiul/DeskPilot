# DeskPilot 🖥️✈️

> 你的桌面 AI 副驾驶 —— 让 AI 真正替你干活，不只是聊天。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Windows-0078D4)](https://github.com/dotnet/wpf)
[![Semantic Kernel](https://img.shields.io/badge/Semantic%20Kernel-AI-FF6F00)](https://github.com/microsoft/semantic-kernel)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](.github/workflows/ci.yml)
[![Tests](https://img.shields.io/badge/tests-104%20passed-brightgreen)](#)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen)](CONTRIBUTING.md)

**DeskPilot** 是一个基于 .NET 8 + WPF 的桌面 AI 助手，专为**办公场景**打造。
和 ChatGPT 套壳应用不同，DeskPilot 的 AI 可以**真正操作你的文件、处理你的文档、自动化你的日常任务**。

---

## ✨ 特性

- 💬 **智能问答** —— 支持 OpenAI / DeepSeek / Ollama（本地）等多模型
- 🔄 **动态模型列表** —— 设置窗口一键 🔄，自动从 Provider 拉取最新模型；离线/失败时用静态兜底
- 📄 **文档处理** —— 一句话完成 Excel 拆分、Word 转 PDF、批量重命名
- 🗂️ **文件整理** —— AI 自动分类、整理、归档你的桌面文件夹
- 🔌 **插件化架构** —— 基于 MCP (Model Context Protocol) 协议，易扩展
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
👤 你：把这 50 个 Word 文档的标题提取出来，做成 Excel

🤖 AI：✅ 已读取 50 个文档，生成 Excel 包含：
       文件名 | 标题 | 字数 | 创建时间
       已保存到 D:\汇总.xlsx
```

---

## 🏗️ 技术栈

| 层 | 技术 |
|---|---|
| UI 框架 | WPF (.NET 9) |
| AI 编排 | Microsoft Semantic Kernel |
| MVVM | CommunityToolkit.Mvvm |
| 依赖注入 | Microsoft.Extensions.DependencyInjection |
| 配置管理 | Microsoft.Extensions.Configuration |
| 日志 | Serilog |
| 测试 | xUnit + FluentAssertions + Moq |
| 协议 | MCP (Model Context Protocol) |

---

## 🚀 快速开始

### 环境要求

- Windows 10 / 11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- （可选）[Ollama](https://ollama.com/) 用于本地模型

### 编译运行

```bash
git clone https://github.com/yourname/deskpilot.git
cd deskpilot
dotnet restore
dotnet build
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

## 📂 项目结构

```
DeskPilot/
├── src/
│   ├── DeskPilot.Core/           # 核心库（AI 编排、Agent、Tools）
│   │   ├── Agents/               # AI Agent 实现
│   │   ├── Tools/                # 可调用的工具（文件操作、文档处理）
│   │   ├── Services/             # 业务服务
│   │   └── Models/               # 数据模型
│   ├── DeskPilot.App/            # WPF 应用
│   │   ├── Views/                # 视图（XAML）
│   │   ├── ViewModels/           # 视图模型
│   │   ├── Resources/            # 资源（图标、字符串）
│   │   └── Styles/               # 样式
│   └── DeskPilot.Verify/         # 工具 E2E 验证程序（控制台，调试用）
├── tests/                        # 单元测试
├── docs/                         # 文档
└── .github/                      # GitHub 配置（CI/CD、Issue 模板）

### 🛠️ 工具 E2E 验证（无需 API Key）

```bash
# 准备测试文件
mkdir -p D:\deskpilot_e2e_test\invoices
echo "发票A" > D:\deskpilot_e2e_test\invoices\inv_001.txt
echo "发票B" > D:\deskpilot_e2e_test\invoices\inv_002.txt

# 运行验证（默认 Created + Month）
dotnet run --project src/DeskPilot.Verify -- "D:\deskpilot_e2e_test\invoices" Month Created
# 跳过真实归档只预览：加 --no
dotnet run --project src/DeskPilot.Verify -- "D:\deskpilot_e2e_test\invoices" Month Created --no
```

输出示例：
```
📂 源目录:   D:\deskpilot_e2e_test\invoices
📄 原始文件数: 5
━━━ Step 1: DryRun 预览 ━━━
📋 [预览] 共 5 个文件，将移动 5 个，跳过 0 个
━━━ Step 2: 真实归档 ━━━
✅ 归档完成：移动 5 个，跳过 0 个，失败 0 个
━━━ Step 3: 验证归档结果 ━━━
📂 archive/ 下有 1 个子目录: 📁 2026-06/ (5 个文件)
✅ 源目录已全部清空，所有文件已归档
```
```

---

## 🗺️ Roadmap

查看 [ROADMAP.md](docs/ROADMAP.md) 了解详细规划。

- [x] **MVP (v0.0.1)** —— 智能问答窗口，支持 OpenAI/Ollama
- [ ] **v0.1** —— 文件整理 + 基础文档处理
- [ ] **v0.5** —— MCP 协议支持 + 插件系统
- [ ] **v1.0** —— 完整办公自动化套件 + 多语言支持

---

## 🤝 贡献

欢迎所有形式的贡献！

- 🐛 [报告 Bug](https://github.com/yourname/deskpilot/issues/new?template=bug_report.md)
- 💡 [提出新功能](https://github.com/yourname/deskpilot/issues/new?template=feature_request.md)
- 📝 [改进文档](docs/)
- 🔧 [提交 PR](https://github.com/yourname/deskpilot/pulls)

详见 [CONTRIBUTING.md](docs/CONTRIBUTING.md)。

---

## 📄 许可证

本项目采用 [MIT 许可证](LICENSE)。

---

## 🙏 致谢

- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel) —— AI 编排框架
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) —— MVVM 工具包
- [HandyControl](https://github.com/HandyOrg/HandyControl) —— WPF 控件库

---

<p align="center">⭐ 如果这个项目对你有帮助，欢迎 Star 支持！</p>