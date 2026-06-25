# DeskPilot 快速开始 🚀

> 5 分钟跑起来你的桌面 AI 助手。

---

## 前置条件

- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（或 .NET 10 SDK）
- 一个 AI 服务的 API Key（或本地 Ollama）

---

## 🎯 三步启动

### Step 1：克隆并进入项目

```bash
git clone <your-repo-url> deskpilot
cd deskpilot
```

### Step 2：配置 API Key

**方式 A：编辑 .env 文件（推荐）**

```bash
copy .env.example .env
notepad .env
```

填入你的 Key，例如：
```
OPENAI_API_KEY=sk-xxxxxxxxxxxx
AI_PROVIDER=OpenAI
```

**方式 B：用 User Secrets（开发用）**

```bash
cd src/DeskPilot.App
dotnet user-secrets set "AI:OpenAI:ApiKey" "sk-xxxxxxxxxxxx"
dotnet user-secrets set "AI:Provider" "OpenAI"
cd ..\..
```

**方式 C：完全本地（Ollama，无需 Key）**

```bash
# 1. 装 Ollama：https://ollama.com/
# 2. 拉模型
ollama pull qwen2.5:7b

# 3. 启动服务（另一个终端）
ollama serve

# 4. 改 .env 里的 Provider = Ollama
```

### Step 3：启动

**Windows 一键：**
```bash
run.bat
```

**或手动：**
```bash
dotnet run --project src/DeskPilot.App
```

---

## 🎉 启动成功的标志

窗口弹出，左上角有 `✈️ DeskPilot` 标题，底部有输入框。

试着问一句："你好，你能做什么？"

---

## 🆘 常见问题

### Q1：弹窗说"未检测到 AI API Key"
**A：** 检查 `.env` 文件是否在项目根目录、Key 是否替换了占位符。

### Q2：编译报错 `SKEXP0010`
**A：** 已经在 csproj 里加了 `<NoWarn>SKEXP0010;SKEXP0001</NoWarn>`，如果还有问题，重启 IDE。

### Q3：OpenAI 连不上（超时/被墙）
**A：** 切换到 DeepSeek 或 Ollama：
- DeepSeek：国内可用，便宜
- Ollama：完全本地，零成本

### Q4：想换模型（比如 gpt-4o、deepseek-reasoner）
**A：** 编辑 `.env`，把 `OPENAI_MODEL=gpt-4o-mini` 改成 `gpt-4o` 即可。

---

## 📂 项目目录速查

```
deskpilot/
├── .env                 ← 你的配置文件（Key 在这里）
├── run.bat              ← 一键启动脚本
├── README.md            ← 项目介绍
├── docs/
│   ├── QUICK_START.md   ← 本文档
│   └── ROADMAP.md       ← 版本规划
└── src/
    └── DeskPilot.App/   ← 主程序入口
```

---

## 🔗 下一步

- 查看 [ROADMAP.md](ROADMAP.md) 了解项目计划
- 查看 [README.md](../README.md) 了解技术架构
- 想贡献？查看 CONTRIBUTING.md（待编写）

---

<p align="center">🌟 玩得开心！有问题来 GitHub 提 Issue</p>