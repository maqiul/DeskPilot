# 贡献指南 🤝

非常感谢你有兴趣为 DeskPilot 做贡献！🎉

## 📋 行为准则

- 尊重所有参与者
- 接受建设性批评
- 关注对社区最有利的事情

## 🐛 报告 Bug

发现 Bug？请使用 [Bug Report 模板](../../issues/new?template=bug_report.md) 提交 Issue。

## 💡 提出新功能

有好点子？请使用 [Feature Request 模板](../../issues/new?template=feature_request.md) 提交 Issue。

## 🔧 提交 Pull Request

### 开发流程

1. **Fork** 本仓库
2. **Clone** 你的 Fork
3. **创建分支**：`git checkout -b feature/your-feature-name`
4. **开发**：编写代码 + 测试
5. **本地验证**：
   ```bash
   dotnet build DeskPilot.slnx    # 必须 0 错误
   dotnet test DeskPilot.slnx     # 必须全绿
   ```
6. **Commit**：`git commit -m "feat: 你的功能描述"`
7. **Push**：`git push origin feature/your-feature-name`
8. **创建 PR**：在 GitHub 上发起 Pull Request

### Commit 规范

我们使用 [Conventional Commits](https://www.conventionalcommits.org/zh-hans/)：

| 前缀 | 含义 | 示例 |
|------|------|------|
| `feat:` | 新功能 | `feat: 添加按日期归档文件工具` |
| `fix:` | Bug 修复 | `fix: 修复聊天历史在重启后丢失` |
| `docs:` | 文档更新 | `docs: 更新 ROADMAP 链接` |
| `style:` | 代码格式 | `style: 统一使用 4 空格缩进` |
| `refactor:` | 重构 | `refactor: 抽象 IChatService 接口` |
| `test:` | 测试 | `test: 添加 ChatService 边界测试` |
| `chore:` | 杂项 | `chore: 更新依赖版本` |

### 代码风格

- C# 使用 .NET 默认格式（`dotnet format`）
- WPF XAML 缩进 4 空格
- ViewModel 必须继承 `ObservableObject`（CommunityToolkit.Mvvm）
- 任何公开 API 必须有 XML 注释
- 新功能必须附带单元测试

## 🏗️ 项目结构

```
src/
├── DeskPilot.Core/        ← 核心业务逻辑（无 UI 依赖）
└── DeskPilot.App/         ← WPF 应用
tests/
└── DeskPilot.Tests/       ← 单元测试
docs/                      ← 文档
```

**原则：** Core 不能引用 App（保持可测试性），App 可以引用 Core。

## 📦 提交流程检查清单

提交 PR 前确认：

- [ ] 代码能 `dotnet build` 通过
- [ ] 所有测试能 `dotnet test` 通过
- [ ] 新功能有对应的单元测试
- [ ] 公开 API 有 XML 文档注释
- [ ] 更新了相关文档（README / ROADMAP / CHANGELOG）
- [ ] Commit 信息符合规范

## 🎯 适合新手的任务

查看 [Issues](../../issues?q=is%3Aopen+is%3Aissue+label%3A%22good+first+issue%22) 中带 `good first issue` 标签的任务。

## 📞 联系方式

- GitHub Issues：提交问题
- GitHub Discussions：讨论想法（待开启）

---

<p align="center">🌟 期待你的第一个 PR！</p>