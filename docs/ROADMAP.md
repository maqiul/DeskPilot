# DeskPilot Roadmap 🗺️

> 桌面 AI 助手的演进路线图。每个阶段都有明确的可交付物和验收标准。

---

## ✅ v0.0.1 — MVP（已完成）

**目标：** 验证技术栈可行性，跑通"AI 对话"最小闭环。

### 已交付

- [x] 项目骨架（.NET 8 + WPF + Semantic Kernel + DI）
- [x] README + Roadmap 文档
- [x] 智能问答窗口（ChatWindow）
- [x] 多模型支持（OpenAI / DeepSeek / Ollama）
- [x] MVVM 架构（CommunityToolkit.Mvvm）
- [x] 配置文件（appsettings.json）+ 环境变量支持

### 验收

```bash
dotnet build DeskPilot.slnx    # 0 错误 0 警告
dotnet run --project src/DeskPilot.App
```

---

## 🚧 v0.1 — 文件整理 + 文档处理（计划中，预计 2 周）

**目标：** 从"聊天工具"升级为"能干活"的助手。AI 可以**真实操作文件、处理文档**。

### 核心功能

- [ ] **📂 智能文件整理**
  - 一句话整理桌面/下载文件夹（按类型/日期/项目分类）
  - 重命名规则生成（基于内容识别）
  - 重复文件检测 + 清理建议
- [ ] **📄 Office 文档处理**
  - Excel 拆分 / 合并 / 转 CSV
  - Word 转 PDF（带样式保留）
  - PDF 合并 / 拆分 / 水印
  - PPT 批量替换文本
- [ ] **🔧 MCP 工具协议**
  - 工具调用框架（基于 Semantic Kernel Function Calling）
  - 工具权限管理（读/写/删分级）
  - 操作前用户确认
- [ ] **📝 对话历史持久化**
  - SQLite 存储聊天记录
  - 按时间线查看历史会话
  - 重新加载上下文

### 技术要点

- 引入 `NPOI` / `PdfSharp` / `OpenXML` 处理文档
- 工具调用走 `KernelFunction` 抽象
- 操作前必须 `MessageBox.Show` 二次确认（防 AI 误操作）
- 操作记录写入日志（可回溯）

---

## 🎯 v0.5 — Agent 化 + 插件系统（计划中，预计 4 周）

**目标：** 用户可以自定义工具，扩展 AI 能力边界。

### 核心功能

- [ ] **🧩 插件系统**
  - 第三方插件加载（基于 MCP / 自定义接口）
  - 插件市场（GitHub 仓库托管）
  - 插件权限沙箱
- [ ] **🤖 多 Agent 协作**
  - Planner Agent：拆解复杂任务
  - Executor Agent：调用工具执行
  - Verifier Agent：检查结果
- [ ] **📊 任务编排**
  - 长任务进度条 + 后台执行
  - 任务队列管理
  - 失败重试 + 回滚
- [ ] **🌐 网络能力**
  - 网页抓取（指定 URL 提取信息）
  - RSS 订阅监控
  - 邮件摘要生成

### 技术要点

- 插件用 `AssemblyLoadContext` 热加载
- Agent 间通信走消息总线
- 引入 `Spectre.Console` 做富文本输出
- 长期任务用 `BackgroundService` 托管

---

## 🏆 v1.0 — 完整办公自动化套件（计划中，预计 8 周）

**目标：** 成为 Windows 桌面办公的 AI 标配，对标 Raycast + Microsoft Copilot。

### 核心功能

- [ ] **⌨️ 全局快捷键**
  - `Ctrl + Space` 唤起悬浮窗
  - 任意界面截图提问
  - 选中文本解释 / 翻译 / 重写
- [ ] **📋 剪贴板增强**
  - 智能识别剪贴板内容类型
  - 一键转换（JSON ↔ Table ↔ Markdown）
  - 历史剪贴板（100 条）
- [ ] **🗂️ 知识库**
  - 本地文档索引（向量数据库）
  - RAG 问答
  - 多格式支持（PDF / Word / Markdown / 代码）
- [ ] **🌍 多语言**
  - 界面：中文 / English / 日本語
  - Prompt：自动检测用户语言
  - 文档：多语言 README
- [ ] **☁️ 跨设备同步**（可选）
  - 配置云端备份
  - 对话记录同步
  - 插件云端分发

### 技术要点

- 全局快捷键用 Win32 API（`RegisterHotKey`）
- 截图用 `System.Drawing` + 剪贴板 API
- 向量库用 `Sqlite-vec` / `lancedb`
- 引入 `Avalonia` 评估跨平台可能性

---

## 🤝 社区共建

欢迎在以下方向贡献：

| 方向 | 难度 | 适合 |
|------|------|------|
| 🐛 Bug 修复 | ⭐ | 新手 |
| 📝 文档完善 | ⭐ | 新手 |
| 🎨 UI 美化 | ⭐⭐ | 前端/设计师 |
| 🔌 新工具开发 | ⭐⭐ | 全栈 |
| 🧠 AI Prompt 优化 | ⭐⭐ | 产品 |
| 🏗️ 架构改进 | ⭐⭐⭐ | 资深开发者 |

详见 [CONTRIBUTING.md](CONTRIBUTING.md)。

---

## 📅 时间线

```
2026-06  ━━ v0.0.1 MVP ✅
2026-07  ━━ v0.1 文件整理 📂
2026-08  ━━ v0.5 Agent 化 🤖
2026-10  ━━ v1.0 完整套件 🏆
```

> 📌 时间仅为参考，实际进度看社区贡献。

---

<p align="center">🌟 Star 本项目以关注进度更新！</p>