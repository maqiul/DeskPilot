## 🎉 DeskPilot v0.4.2 — 修复 Release Notes 自动生成

### 🔧 修复

- **Release notes 终于能自动显示了！**
  - 根因：GITHUB_OUTPUT 的 heredoc 多行语法在复杂场景下不可靠（被 GitHub Actions 截断/吞行）
  - 修复：改用 `body_path` 参数（`softprops/action-gh-release@v2` 推荐的稳定方案）
  - `cp "$NOTES_FILE" release_notes.md` → `body_path: release_notes.md`

- **Release workflow 可靠性提升**
  - 避免 GITHUB_OUTPUT 多行传递的坑
  - 文件方式传递 100% 可靠

> 完整变更历史见 [CHANGELOG.md](https://github.com/maqiul/DeskPilot/blob/master/CHANGELOG.md)
