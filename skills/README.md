---
title: DeskPilot 技能市场索引
description: 公开技能清单，按 ID 索引，对应 skills/{id}.json
---

# DeskPilot 技能市场

> 这份索引是 DeskPilot 桌面助手 v0.11 引入的"技能市场"功能的**唯一权威来源**。
> 应用启动时会从这里拉取技能列表，用户在设置窗口浏览、点击安装到本地。

## 索引规范

每个技能在下方 markdown 表格登记一行，字段：
- **id**：技能唯一标识（对应 `skills/{id}.json`）
- **name**：显示名
- **description**：简短说明
- **icon**：单个 Emoji
- **category**：分类（文件整理/图片处理/通用/...）
- **author**：作者署名
- **version**：语义化版本
- **screenshotUrl**（v0.11，可选）：缩略图 URL（详情弹窗用大图）
- **rating**（v0.11，可选）：0-5 星评分
- **downloads**（v0.11，可选）：下载次数

## 当前技能清单

| id | name | description | icon | category | author | version | screenshotUrl | rating | downloads |
|---|---|---|---|---|---|---|---|---|---|
| organize-downloads | 整理下载文件夹 | 按文件类型自动归类下载文件夹里的内容（PDF/图片/视频/压缩包/文档/其他） | 📁 | 文件整理 | maqiul | 1.0.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/organize-downloads.png | 4.8 | 1240 |
| find-duplicate-photos | 找出重复照片 | 扫描指定目录，列出内容哈希相同的重复图片（支持 SHA256） | 🔍 | 文件整理 | maqiul | 1.0.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/find-duplicate-photos.png | 4.5 | 870 |
| batch-rename | 批量重命名文件 | 按统一规则批量重命名（日期前缀 / 序号 / 正则替换） | ✏️ | 文件整理 | maqiul | 1.0.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/batch-rename.png | 4.7 | 620 |
| batch-resize-images | 批量压缩图片 | 把大图缩到指定宽度（如 1920px），保持宽高比，输出到新目录 | 🖼 | 图片处理 | maqiul | 1.0.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/batch-resize-images.png | 4.6 | 510 |
| batch-extract-archives | 批量解压压缩包 | 把下载文件夹里的所有 zip/7z/rar 批量解压到同名子目录 | 📦 | 文件整理 | maqiul | 1.0.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/batch-extract-archives.png | 4.4 | 480 |
| compute-file-hashes | 计算文件哈希值 | 批量计算文件的 SHA256 / MD5，输出 CSV 报告 | 🔐 | 文件整理 | maqiul | 1.0.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/compute-file-hashes.png | 4.2 | 320 |
| clean-large-files | 清理大文件 | 找出指定目录里大于 X MB 的大文件，让用户确认是否删除 | 📊 | 文件整理 | maqiul | 1.0.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/clean-large-files.png | 4.3 | 410 |
| archive-by-date | 按日期归档文件 | 把文件按修改日期归档到 YYYY/MM 子目录（适合照片、视频） | 🗓 | 文件整理 | maqiul | 1.0.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/archive-by-date.png | 4.5 | 290 |
| scan-invoices | 扫描发票并归档 | **v0.12 多步**：校验哈希 → 按月归档 → 查重 | 🧾 | 财务办公 | community | 1.1.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/scan-invoices.png | 4.9 | 180 |
| weekly-report-helper | 周报助手 | **v0.12 多步**：校验笔记 → 压缩配图 | 📝 | 文档处理 | community | 1.0.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/weekly-report-helper.png | 4.1 | 95 |
| git-commit-message | Git 提交信息生成 | **v0.12 多步**：校验变更 → dry-run 备份 CHANGELOG | 🔧 | 开发工具 | community | 1.1.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/git-commit-message.png | 4.7 | 220 |
| code-review-helper | 代码评审助手 | **v0.13 多步**：搜 TODO/FIXME → 统计代码量 | 🔍 | 开发工具 | community | 1.0.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/code-review-helper.png | 4.3 | 65 |
| file-organizer | 文件智能分类归档 | **v0.13 多步**：按关键词分类 → 按日期归档 | 📂 | 文档处理 | community | 1.0.0 | https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills/screenshots/file-organizer.png | 4.5 | 110 |

## 如何贡献新技能

1. Fork [maqiul/DeskPilot](https://github.com/maqiul/DeskPilot) 仓库
2. 在 `skills/` 目录下新建 `{your-skill-id}.json`，格式参考现有文件
3. 在上表添加一行登记
4. 提交 PR，标题 `skill: <name>`

详细 JSON schema 见 [`SCHEMA.md`](./SCHEMA.md)。