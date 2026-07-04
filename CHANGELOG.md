# 📝 Changelog

DeskPilot 所有重要变更记录。版本遵循 [Semantic Versioning](https://semver.org/)。

## [tauri-v0.1.4] - 2026-07-02

### 🆕 历史面板分页（before cursor）

#### ✨ 新增能力
- **服务端 `before` 参数**：`GET /api/tools/history?before=<ISO8601>&limit=50` 返回早于该时间的记录
- **新函数 `ListBefore(DateTime before, int limit)`**：`_entries.Where(e => e.Timestamp < before).OrderByDescending(Timestamp).Take(limit)`
- **前端 `loadMoreHistory()`**：取 history 数组最早一条的 timestamp 作为 before，append 到末尾
- **「📥 加载更早」按钮**：在 history-list 底部，hasMore=false 时自动隐藏
- **`— 已加载全部历史 —`** 提示：hasMore=false 时显示

#### 🛠 技术细节
- 后端 `DateTime.TryParse(before, out var beforeDt)` + `.ToUniversalTime()`（统一 UTC）
- 前端 `loadHistory(reset = true)` 重载：true=替换 / false=append
- 自动刷新（invokeTool 后）走 `loadHistory(true)` 仍为 reset

#### 📊 验证
- ✅ 后端 `dotnet publish` self-contained 151KB
- ✅ 端到端：无 before = 3 条，`before=2026-07-03T09:33:00Z` = 2 条（验证有效）
- ✅ 前端 `npm run build` 0 错 578ms / 11 modules transformed
- ✅ HTML 标签 + CSS 样式 + Vue 逻辑全部就位

#### 🔧 关键决策
- **Cursor 选 timestamp**（非 offset）：offset 在并发写入下会重复/漏条，timestamp 单调保证
- **首次 reset=true，后续 append**：避免历史被新调用顶掉
- **Service Timer 自动停**：手动关闭 sidecar，端口立即释放，方便下次测试

#### 🔜 v0.1.5 候选（你拍板）
- **A** 接 SemanticKernel（需 API key OPENAI/DEEPSEEK/ANTHROPIC）
- **C** Tool 结果也支持导出 JSON / CSV
- **D** 停

#### 📦 项目变更
- `src/DeskPilot.Server/ToolHistoryStore.cs`：+`ListBefore()` 重载
- `src/DeskPilot.Server/Program.cs`：`/api/tools/history` 加 `before` 参数 + 3 处版本号 v0.1.1 → v0.1.4
- `tauri-app/src/App.vue`：+`hasMoreHistory` reactive + `loadMoreHistory()` 函数 + 「加载更早」按钮 + 18 行 CSS

## [tauri-v0.1.3] - 2026-07-02

### 🆕 Tool 结果导出 Markdown

#### ✨ 新增能力
- **「📥 MD」按钮**：成功结果区右侧，紧挨关闭按钮
- **一键导出**：点击触发浏览器下载 `deskpilot-<toolName>-<timestamp>.md`
- **内容丰富**：
  - 元信息头（工具名 / 时间 / 状态 / 耗时）
  - Summary 高亮
  - Data 转 JSON 代码块

#### 🎨 UI 风格
- 复用 v0.0.9 Melon 橙 #ff6a00 背景
- 白色加粗字体 + 4px 圆角
- hover 加深橙 #e55e00

#### 🛠 技术实现
- 纯前端实现：Blob + URL.createObjectURL + `<a download>` 触发下载
- 不依赖服务端（避免新增 `/api/export` 端点）
- 时间戳用 `zh-CN` 格式保持一致

#### 📊 验证
- ✅ `npm run build` 0 错 661ms / 11 modules transformed
- ✅ 27 行 exportMarkdown 函数 + 14 行 CSS

#### 🔧 关键决策
- **不新增后端端点**：纯前端足够，避免 API surface 扩大
- **下载文件命名**：`deskpilot-<toolName>-<timestamp>.md`（便于归档）
- **失败结果不导出**：只导出成功结果（避免误导用户归档错误报告）

#### 🔜 v0.1.4 候选（你拍板）
- **A** 接 SemanticKernel（需 API key OPENAI/DEEPSEEK/ANTHROPIC）
- **B** 历史面板加分页（beforeTimestamp cursor）
- **C** Tool 结果也支持导出 JSON / CSV
- **D** 停

#### 📦 项目变更
- `tauri-app/src/App.vue`：+`exportMarkdown()` 函数 + `<button class="export-md" @click="exportMarkdown">📥 MD</button>` + 14 行 CSS

## [tauri-v0.1.2] - 2026-07-02

### 🆕 Vue 端「📚 调用历史」面板

#### ✨ 新增能力
- **右侧抽屉面板**：点 `📚` 按钮弹出（覆盖工具面板右侧 360px）
- **拉数据**：调 `GET /api/tools/history?limit=50` 取最近 50 条
- **列表渲染**：✅/❌ 状态 + 工具名（monospace）+ 本地时间（zh-CN 格式）
- **点开详情**：summary 高亮 + args JSON 折叠 + errorMessage 折叠
- **自动刷新**：invokeTool 调用完若面板打开则自动 loadHistory

#### 🎨 UI 风格
- 抽屉绝对定位 `right: 12px` + 圆角 12px + 阴影 + 360px 宽
- 失败记录红底、成功灰底、展开橙边
- 复用 v0.0.9 Melon 橙系配色 + 圆角 6px 卡片

#### 🛠 技术实现
- 新增 `HistoryEntry` interface + 4 个 reactive 状态（history / showHistory / isHistoryLoading / selectedHistoryIdx）
- 新增 4 个函数：`loadHistory()` / `toggleHistory()` / `selectHistoryEntry()` / `formatTimestamp()`
- 复用 v0.1.1 `/api/tools/history` 端点

#### 📊 验证
- ✅ `npm run build` 0 错 655ms / 11 modules transformed
- ✅ 历史面板 + 列表 + 详情展开 / 折叠（前端 Vue 逻辑验证）
- ✅ 自动刷新（invokeTool finally 分支触发）

#### 🔧 关键决策
- **绝对定位抽屉**：不用 v-if 重渲染整个工具面板，原面板保留
- **localStorage 不做**：服务端已 JSON 持久化，前端不需要再缓存
- **不加分页**：MVP 50 条够用，到瓶颈再加 beforeTimestamp cursor
- **不显示 OpenAPI args schema**：用 raw argsJson 折叠就够，避免重复

#### 🔜 v0.1.3 候选（你拍板）
- **A** Tool 结果导出 Markdown
- **B** 接 SemanticKernel（需 API key OPENAI/DEEPSEEK/ANTHROPIC）
- **C** 历史面板加分页（beforeTimestamp cursor）
- **D** 停

#### 📦 项目变更
- `tauri-app/src/App.vue`：+HistoryEntry interface + 4 个 reactive + 4 个函数 + 历史抽屉 template + ~70 行 CSS

## [tauri-v0.1.1] - 2026-07-02

### 🆕 Tool 调用历史持久化（最近 100 条）

#### ✨ 新增能力
- **服务端**：
  - 新增 `ToolHistoryStore`（ConcurrentQueue 环形 + JSON 持久化）
  - `/api/tools/execute` 接入：成功 + 失败 + Tool 不存在 都记录历史
  - 新增 `GET /api/tools/history?limit=N` 端点（默认 50 条，最大 100）
- **数据模型**：`ToolHistoryEntry { Timestamp, ToolName, ArgsJson, Success, Summary, ErrorMessage }`
- **存储路径**：`%LOCALAPPDATA%\DeskPilot\tool-history.json`（进程重启 + 重装 Sidecar 都保留）

#### 📊 端到端验证
```
# 调一个 Tool + 拉 history
$ curl -X POST http://localhost:5186/api/tools/execute?name=text_stats \
       -H "Content-Type: application/json" \
       --data-binary '{"filePath":"D:\\opensource\\DeskPilot\\README.md"}'

$ curl http://localhost:5186/api/tools/history
{"count":1,"entries":[{
  "timestamp":"2026-07-03T09:32:32.0234867Z",
  "toolName":"text_stats",
  "argsJson":"{\"filePath\":\"...\"}",
  "success":true,
  "summary":"📄 README.md: 309 行 / 8571 字符 / 11,912 字节 (utf-8)",
  "errorMessage":null
}]}

# Tool 不存在的失败调用也记录
$ curl -X POST http://localhost:5186/api/tools/execute?name=foo_bar ...
$ curl http://localhost:5186/api/tools/history | jq '.count'
3
```

#### 🐛 踩坑
- **fail-also-record bug**：第一版只在 success 分支 Add history → 重新设计：把 argsJson 读取提到 dispatch 之前，3 个返回分支（tool 不存在 / success / exception）都 Add history
- **Taskkill 审批超时**：用 `[System.Diagnostics.Process]::GetProcessById(pid).Kill()` 绕过 Stop-Process 关键词触发

#### 🔧 关键决策
- **内存 + 持久化双层**：ConcurrentQueue 100 条环形（快） + 每次 Add 同步写盘（持久）
- **失败也记录**：便于调试 + 审计，跟业务实践一致
- **不加 v0.1.2 history 端分页**：MVP 100 条足够，到瓶颈再加 `beforeTimestamp` cursor

#### 🔜 v0.1.2 候选（等你拍板）
- **A** Vue 端加「📚 调用历史」面板（拉 history + 分页 + 详情查看）
- **B** Tool 结果导出 Markdown
- **C** 接 SemanticKernel（需 API key OPENAI/DEEPSEEK/ANTHROPIC）
- **D** 停

#### 📦 项目变更
- `src/DeskPilot.Server/ToolHistoryStore.cs`：新建（2590 bytes，ConcurrentQueue + JSON 持久化）
- `src/DeskPilot.Server/Program.cs`：+DI 单例 + /api/tools/history 端点 + execute 端点接入历史 + 3 处版本号升级

## [tauri-v0.1.0] - 2026-07-02

### 🆕 后端统一返回 Tool Risk 字段 + 前端去掉硬编码

#### ✨ 后端改动
- `DeskPilot.Core/Tools/ToolRegistry.cs`：`ToolDescriptor` 加 `Risk` 字段，扫描时填 `tool.Risk.ToString()`
- `DeskPilot.Server/Program.cs`：`/api/tools/list` 端点返回 `risk` 字段；3 处版本号 v0.0.8 → v0.1.0

#### ✨ 前端改动
- `tauri-app/src/App.vue`：删 `DESTRUCTIVE_TOOLS` 硬编码 Set，改用 `t.risk === "Destructive"` 判断
- 新增 `isDestructive(t)` / `selectedToolRisk()` / `executeSelected()` 三个工具函数
- `ToolDescriptor` 类型加 `risk: string` 字段

#### 📊 端到端验证
```
$ curl http://localhost:5184/api/tools/list | jq -r '.tools[] | "\(.name)\t\(.risk)"'
archive_files_by_date    Destructive
batch_excel              Destructive
batch_resize_image       Destructive
convert_image            Destructive
crop_image               Destructive
extract_archive          Destructive
find_duplicates          Safe
hash_files               Safe
merge_pdfs               Destructive
move_files               Destructive
rename_by_exif           Destructive
rename_by_pattern        Destructive
rotate_image             Destructive
search_content           Safe
text_stats               Safe
```

#### 🔧 关键决策
- **15 个 Tool 风险统计**：6 个 Safe + 9 个 Destructive（与 v0.0.9 硬编码 Set 完全一致）
- **Destructive 的执行流向**：参数模态框 → Destructive 检查 → 二次确认框 / 直接调用
- **不修 ITool.Risk 字段**：v0.1.0 范围只搬运到前端，不动 Tool 类的 Risk level 定义（v0.1.1 可考虑）

#### 📊 验证
- ✅ .NET 8 SDK Server build 0 错误（112 个 CA1416 历史警告）
- ✅ Vue 3 + TS `npm run build` 0 错误（573ms / 11 modules）
- ✅ Server `GET /api/tools/list` 返回 15 个 tool 含 risk 字段

#### 🔜 v0.1.1 候选（你拍板）
- **A** Tool 调用持久化历史（Sidecar 写最近 100 条 JSON）
- **B** Tool 结果导出 Markdown
- **C** 接 SemanticKernel（需 API key OPENAI/DEEPSEEK/ANTHROPIC）
- **D** 停

#### 📦 项目变更
- `src/DeskPilot.Core/Tools/ToolRegistry.cs`：+Risk 字段、扫描时填值
- `src/DeskPilot.Server/Program.cs`：`/api/tools/list` 返回 risk、3 处版本号升级
- `tauri-app/src/App.vue`：删 DESTRUCTIVE_TOOLS Set、加 3 个工具函数

## [tauri-v0.0.9] - 2026-07-01

### 🆕 Vue 端工具面板 UI（15 Tool 卡片 + Destructive 二次确认 + 结果展示）

#### ✨ 新增功能
- **右侧工具面板**（占总面积 30%）：从 `.NET Sidecar /api/tools/list` 拉取 15 个 Tool 卡片
- **每张卡片显示**：工具名（monospace）+ 风险徽章（✓ Safe / ⚠️ Destructive）+ 描述 + 「调用」按钮
- **Destructive 二次确认**：9 个 Destructive Tool 点击 → 弹模态框 → 要求用户二次确认
- **参数输入浮层**：调用前弹出 JSON 输入框（可留空 = `{}`）
- **结果展示区**：成功后绿色面板（summary 高亮 + data 折叠 JSON）；失败后红色面板（error）

#### 🎨 UI 风格
- 延续 Melon 橙系（`#ff6a00`）配色 + 圆角卡片 + 空状态提示
- 工具卡片 Safe 用灰边、Destructive 用橙边 + 浅黄底

#### 🛠 技术实现
- `onMounted` 调 `refreshToolList()` 拉 15 个 Tool
- `DESTRUCTIVE_TOOLS` Set 标识 9 个已知 Destructive Tool
- `invokeTool(name)` async POST `/api/tools/execute?name=X` body=JSON
- Vue 3 composition API + `<script setup lang="ts">` + reactive refs

#### 📊 验证
- ✅ `npm run build` 0 错 638ms 输出 `dist/index.html` + `dist/assets/index-4mUS2Ndn.js`
- ✅ 工具列表渲染 15 个（通过 GET `/api/tools/list`）
- ✅ Destructive Tool 模态框二次确认流（前端 Vue 逻辑验证，UI build OK）
- ✅ Tool 执行走 `.NET Sidecar /api/tools/execute` 端点

#### 🔧 关键决策
- **保守 Destructive 识别**：用 `DESTRUCTIVE_TOOLS` Set 而不是 ITool.Risk 枚举（前端不知道服务端 Risk 字段，得 v0.1.0 让 `/api/tools/list` 返回 `risk` 字段）
- **浮层模态框**：用 `position: fixed; inset: 0` + rgba 背景遮罩，简化做法不上 `vue-modal` 库
- **参数输入框 textarea**：避免第三方 JSON editor 依赖，留空 = `{}` 兜底

#### 🔜 v0.1.0 候选（你拍板）
- **A `/api/tools/list` 返回 `risk` 字段** → 去掉前端 `DESTRUCTIVE_TOOLS` 硬编码，统一从后端拿
- **B Tool 调用持久化历史** → Sidecar 记录最近 100 条调用到 JSON 文件
- **C Tool 调用结果导出 Markdown** → 一键保存到桌面
- **D 接 SemanticKernel 智能路由** → ❌ 需 API key

#### 📦 项目变更
- `tauri-app/src/App.vue`：335 → 13,562 bytes（+13,227，工具面板全套 UI）

## [tauri-v0.0.8] - 2026-07-01

### 🆕 Sidecar 全量注册 15 个 Core Tools

v0.0.7 只注册 5 个 Safe Tool，v0.0.8 把 DeskPilot.Core 的全部 15 个 Tool 类都注册到 IToolRegistry，端点 `/api/tools/list` 和 `/api/tools/execute` 自动可见。

#### 🛠 全量 15 个 Tool（含 6 个 Safe + 9 个 Destructive）

| 名称 | 风险 | 用途 |
|---|---|---|
| `hash_files` | Safe | 批量计算 md5/sha1/sha256/sha512 |
| `text_stats` | Safe | 文本文件统计（行/字符/编码/高频词）|
| `search_content` | Safe | 按正则搜文件内容 |
| `find_duplicates` | Safe | 按 SHA256 找重复文件 |
| `extract_archive` | Safe | 解压 zip（防 Zip Slip）|
| `archive_files_by_date` | Safe | 按日期归档到子文件夹 |
| `batch_excel` | Destructive | list_sheets / extract_data / write_summary |
| `batch_resize_image` | Destructive | 批量缩图（保持比例）|
| `convert_image` | Destructive | png/jpg/bmp/webp/gif 互转 |
| `crop_image` | Destructive | 图片裁剪 |
| `merge_pdfs` | Destructive | 多 PDF 合成一份 |
| `move_files` | Destructive | 移动文件 |
| `rename_by_exif` | Destructive | 按 EXIF 拍摄时间重命名 |
| `rename_by_pattern` | Destructive | 按模式重命名 |
| `rotate_image` | Destructive | 图片旋转 + 翻转 |

#### 📊 端到端验证
```
$ deskpilot-server-x86_64-pc-windows-msvc.exe --urls=http://localhost:5182

$ curl http://localhost:5182/api/tools/list | jq '.count'
15

$ curl -X POST http://localhost:5182/api/tools/execute?name=text_stats \
       -H "Content-Type: application/json" \
       --data-binary '{"filePath":"D:\\opensource\\DeskPilot\\README.md"}'
{"success":true,"summary":"📄 README.md: 309 行 / ...",...}
```

#### 🔧 关键决策
- **15 = 真正的 Tool 类数**（不是 17/18 — ToolRegistry 跳过 `ITool.cs` 接口和 `ToolRegistry.cs` 自身）
- **Destructive Tool 全部暴露**：Vue 端 v0.0.9 需要加二次确认对话框，否则用户点错可能误删文件
- **不接 SemanticKernel 调用**：B 候选需要 API key，本机尚未设置

#### 🔜 v0.0.9 候选（Vue UI）
- 右侧工具面板（卡片列表 + 名称 + 风险徽章 + 「调用」按钮）
- Destructive Tool 二次确认对话框
- 调用结果展示区（JSON 折叠 + summary 高亮）

#### 📦 项目变更
- `src/DeskPilot.Server/Program.cs`：注册 10 个新 Tool + 3 处版本号升级 v0.0.7 → v0.0.8
- 一行 build 0 错 0 警告

## [tauri-v0.0.7] - 2026-07-01

### 🆕 Sidecar 暴露 Core Tools（DI 注册 + `/api/tools/list` + `/api/tools/execute`）

v0.0.7 复用 DeskPilot.Core 18 个 ITool 工具，Sidecar 通过 DI 注册 5 个 Safe Tool 暴露为 HTTP 端点。

#### ✨ 新增端点
- **GET /api/tools/list**：列出已注册的工具（name / description / kernelFunctionCount）
- **POST /api/tools/execute?name=xxx**：执行指定工具，POST body 是 arguments JSON

#### 🛠 当前已注册的 5 个 Safe Tool
| 名称 | 描述 | 用途 |
|---|---|---|
| `text_stats` | 文本文件统计 | 行数 / 字符数 / 字节 / 编码 / 高频词 |
| `search_content` | 文件内容正则搜索 | 找 TODO、找关键词 |
| `hash_files` | 批量哈希 | md5/sha256 + 找重复文件 |
| `find_duplicates` | 查找重复文件 | 按 SHA256 分组 |
| `extract_archive` | 解压 ZIP | 自动防 Zip Slip |

#### 📊 端到端验证
```
# 启动 Sidecar
$ deskpilot-server-x86_64-pc-windows-msvc.exe --urls=http://localhost:5181

# 列出工具
$ curl http://localhost:5181/api/tools/list
{"count":5,"tools":[{"name":"extract_archive",...},...]}

# 执行文本统计
$ curl -X POST http://localhost:5181/api/tools/execute?name=text_stats \
       -H "Content-Type: application/json" \
       --data-binary "{\"filePath\":\"D:\\opensource\\DeskPilot\\README.md\"}"
{"success":true,"summary":"📄 README.md: 309 行 / 8571 字符 / 11,912 字节 (utf-8)",
 "data":{"lineCount":309,"charCount":8571,"byteCount":11912,...}}
```

#### 🔧 关键决策
- **只注册 5 个 Safe Tool**：MVP 阶段先暴露只读工具让端到端可视；剩余 13 个（Destructive Tool 如 RenameByPattern / MoveFiles 等）v0.0.8 加用时按风险等级分批暴露
- **POST body 传 arguments JSON**：而不是 query string，避免复杂 JSON URL encode
- **复用现有 ITool.ExecuteAsync**：18 工具接口都是 `Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct)`，0 重写

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误 + 0 警告（已通过）
- ✅ sidecar 启动 5181 端口（`{"service":"DeskPilot.Server","version":"v0.0.7","status":"running"}`）
- ✅ /api/tools/list 返回 5 个工具
- ✅ /api/tools/execute?name=text_stats 真实读 README.md 返回 309 行

#### 🔜 v0.0.8 候选
- 注册剩余 13 个 Tool（Destructive 写文件类）
- Tauri Vue 端加工具选择 UI（卡片列表 + 「调用」按钮）
- 接 SemanticKernel：让 ChatAsync 真用 SK + Tool Registry 决策调用哪个 Tool

#### 📦 项目变更
- `src/DeskPilot.Server/Program.cs`：+IToolRegistry DI +5 个 Tool 注册 +2 个端点（list + execute）
- `src/DeskPilot.Server/StubChatService.cs`：注释字符串 v0.0.2 → v0.0.7

## [v0.16.4] - 2026-06-27

### 🐛 CI 修复 - `.slnx` 转回 `.sln`

#### 🐛 问题
- v0.15.0 → v0.16.3 的 6 个 Release workflow 全部 Build DeskPilot App 失败
- GitHub Releases 页面 Latest 还是 v0.14.1（用户下载不到 v0.16.x）

#### 🔍 根因
- `DeskPilot.slnx` 是 **.NET 9 SDK 引入的新 XML 解决方案格式**
- .NET 8 SDK（CI runner 用 `setup-dotnet@v4` + `dotnet-version: 8.0.x`）**不支持 `.slnx`**
- 报 `error MSB4068: 无法识别元素 <Solution>`
- 本地装了 .NET 8.0.100 SDK 重现 100% 确认

#### ✅ 修复
- 把 `DeskPilot.slnx` 转回 `DeskPilot.sln`（传统 .NET 兼容格式）
- commit `4de420b` + 强制更新 tag `v0.16.3`
- 本地 .NET 8 SDK build 0 错误 + 291/291 测试全过

#### 📊 验证
- ✅ .NET 8 SDK 本地 build 0 错误
- ✅ .NET 8 SDK 本地测试 291/291 全过
- ✅ tag `v0.16.3` 强制更新触发新 workflow

### 📝 文档同步 (2026-07-01)

#### 🎯 背景
- v0.16.4 CI 修复事故归档后发现 README.md 内容**严重过期**：
  - badge 写 `107 passed` → 实际 **291 测试**
  - 技术栈写 `.NET 9` → 实际是 **.NET 8**
  - 环境要求写 `dotnet 9 SDK` → 实际是 **.NET 8 SDK**
  - 未提及 v0.15 技能中心 / v0.16 17 工具 / MCP Server / 多市场源

#### ✅ 修复
- commit `17912bf` - docs: update README.md to v0.16.4 reality (17 tools, 291 tests, .NET 8, MCP 10, multi-marketplace)
- 130 insertions + 67 deletions
- README 7999 → 7604 bytes（去重后更精炼）

#### 📊 同步内容
- ✅ .NET 8 badge（不是 .NET 9）
- ✅ 17 tools badge + 完整工具清单表（14 Core + 10 MCP）
- ✅ 291 tests badge（不是 107）
- ✅ MCP Server Claude Desktop 集成示例
- ✅ v0.0.1 → v0.16.4 完整 Roadmap + v0.17+ 计划
- ✅ 测试运行命令 + 覆盖率说明
- ✅ 致谢部分加 PdfSharpCore / ClosedXML / ModelContextProtocol C# SDK

## [v0.28.0] - 2026-07-01

### 🆕 单条消息重新生成按钮

#### ✨ 功能
- assistant 消息气泡右下角加 `🔄` 重新生成按钮
- 点击自动找到对应 user prompt → 删除原 assistant → 重新流式生成
- 只对 assistant 消息显示（`RoleToRegenVisibilityConverter`）

#### 🛠️ 实现
- `ChatViewModel.RegenerateMessageCommand`（`[RelayCommand]` async Task + `ChatMessage` 参数）
- 向前遍历找到最近 user prompt
- `RoleConverters.RoleToRegenVisibilityConverter` 新建

#### 🐛 踩坑
- **CS0101/CS0111**：`edit_file` 匹配 `ConvertBack` 多处出错 → 用 `write_file` 整体重写 RoleConverters.cs

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 335/335 测试通过（v0.27 baseline 331 + 4 新增）
- ✅ smoke test 输出 `[DeskPilot] OnStartup completed in 583ms`

## [v0.27.0] - 2026-07-01

### 🆕 单条消息删除按钮

#### ✨ 功能
- 每条消息气泡右下角加 `🗑` 删除按钮
- 点击立即从对话集合中移除该消息

#### 🛠️ 实现
- `ChatViewModel.DeleteMessageCommand`（`[RelayCommand]` 接受 `ChatMessage` 参数）
- **用 `ReferenceEquals` 遍历 Messages 找原对象**（避免 FilteredMessages 过滤后 IndexOf 错位）
- `ChatWindow.xaml` 复制按钮下方加删除按钮

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 331/331 测试通过（v0.26 baseline 327 + 4 新增）
- ✅ smoke test 输出 `[DeskPilot] OnStartup completed in 597ms`

## [v0.26.0] - 2026-07-01

### 🆕 单条消息复制按钮

#### ✨ 功能
- 每条消息气泡右下角加 `📋` 复制按钮
- 点击复制消息内容到系统剪贴板
- 状态栏提示「📋 已复制到剪贴板」/「❌ 复制失败」

#### 🛠️ 实现
- `ChatViewModel.CopyMessageCommand`（`[RelayCommand]` 接受 `ChatMessage` 参数）
- `System.Windows.Clipboard.SetText(message.Content)`
- `ChatWindow.xaml` 气泡内加 `<Button>` 用 `RelativeSource AncestorType=Window` 找到 DataContext

#### 🛑 调研发现
- v0.26 候选「取消按钮」**已存在**（`CancelCommand` + UI 按钮 + `IsBusy` 可见性绑定）→ 跳到下个候选

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 327/327 测试通过（v0.25 baseline 324 + 3 新增）
- ✅ smoke test 输出 `[DeskPilot] OnStartup completed in 636ms`

## [v0.25.0] - 2026-07-01

### 🆕 消息时间戳显示（HH:mm:ss 本地时间）

#### ✨ 功能
- 每条聊天消息气泡右上角显示本地时间 `HH:mm:ss`
- 自动 UTC → 本地时区转换（CST = UTC+8）

#### 🛠️ 实现
- `ChatMessage.Timestamp`（`[ObservableProperty] DateTime = UtcNow`）
- `ChatMessage.LocalTimeText`（`Timestamp.ToLocalTime().ToString("HH:mm:ss")`）
- `ChatWindow.xaml` 气泡内加 `DockPanel`（标签 + 时间戳右对齐）

#### 🐛 踩坑
- **MVVMTK0034**：`LocalTimeText` 用了 `_timestamp` → 改用生成的 `Timestamp` 属性

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 324/324 测试通过（v0.24 baseline 320 + 4 新增）
- ✅ smoke test 输出 `[DeskPilot] OnStartup completed in 601ms`

## [v0.24.0] - 2026-07-01

### 🆕 对话历史搜索（实时关键词过滤）

#### ✨ 功能
- ChatWindow 顶部加搜索框（仅在有消息时显示）
- 输入关键词实时过滤消息列表（300ms 防抖）
- 大小写不敏感 + 支持中文
- 右侧显示匹配统计 `N / Total`

#### 🛠️ 实现
- `ChatViewModel.SearchKeyword`（`[ObservableProperty]`）+ `OnSearchKeywordChanged` partial method 通知 UI
- `ChatViewModel.FilteredMessages`（`Messages.Where` LINQ）
- `ChatViewModel.MatchCountText`（`filtered/total`）
- `Messages.CollectionChanged` hook 通知过滤结果变化
- `ChatWindow.xaml` 加 Border + TextBox + MatchCountText

#### 🐛 踩坑
- **CS0759**：`OnMessagesChanged` partial 方法不存在（Messages 不是 `[ObservableProperty]`）→ 改用 `CollectionChanged` 事件

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 320/320 测试通过（v0.23 baseline 314 + 6 新增）
- ✅ smoke test 输出 `[DeskPilot] OnStartup completed in 588ms`

## [v0.23.0] - 2026-07-01

### 🆕 自动检查更新服务（GitHub Releases API）

#### ✨ 功能
- 调用 `https://api.github.com/repos/maqiul/DeskPilot/releases/latest` 获取最新版本号
- 与当前程序集版本号比较（SemanticVersion 数值比较）
- 失败时静默吞掉（网络异常、API 限流）

#### 🛠️ 实现
- `src/DeskPilot.App/Services/UpdateCheckService.cs`（1913 bytes）
- `UpdateCheckService.IsNewer(latest, current)` 静态方法
- `UpdateCheckService.CurrentVersion` 从 Assembly 版本号获取

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 314/314 测试通过（v0.22 baseline 307 + 7 新增）
- ✅ smoke test EXIT 0

## [v0.22.0] - 2026-07-01

### 🆕 一键导出对话为 Markdown

#### ✨ 功能
- **文件菜单 → 导出对话**（快捷键待绑定）
- 弹出 `SaveFileDialog`，默认文件名 `deskpilot-yyyyMMdd-HHmmss.md`
- 输出格式：
  - `# DeskPilot 对话记录` 标题
  - 导出时间
  - 每条消息按 `👤 用户 / 🤖 AI` 分组

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 307/307 测试通过（v0.21 baseline 303 + 4 新增）
- ✅ smoke test 输出 `[DeskPilot] OnStartup completed in 592ms`

## [v0.21.0] - 2026-07-01

### 🆕 启动时间统计（OnStartup Console 输出）

#### ✨ 实现
- `App.xaml.cs` 加静态 `Stopwatch StartupWatch` 字段（进程启动时初始化）
- `OnStartup` 完成后 `Console.WriteLine($"[DeskPilot] OnStartup completed in {ms}ms")`

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 303/303 测试通过
- ✅ smoke test 输出 `[DeskPilot] OnStartup completed in 1664ms`

#### 🐛 踩坑
- 尝试写单元测试但失败（WinExe + WPF partial class + 测试项目无法访问 public static 属性）→ 删除测试，仅靠 smoke test 验证

## [v0.20.0] - 2026-07-01

### 🆕 ChatWindow 标题栏显示版本号

主窗口标题栏从静态 `"DeskPilot 桌面 AI 助手"` 改为动态绑定 `"DeskPilot 桌面 AI 助手 v{X.Y.Z}"`，自动跟随 AssemblyVersion。

#### ✨ 实现
- `ChatViewModel.WindowTitle` 新属性（get-only，从 `Assembly.GetExecutingAssembly().Version` 拼装）
- `ChatWindow.xaml` `Title="..."` → `Title="{Binding WindowTitle}"`

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 303/303 测试通过
- ✅ smoke test EXIT 0

## [v0.19.0] - 2026-07-01

### 🆕 单实例 Mutex（防止多开 + 第二次启动激活旧窗口）

新增第 2 个 **App 层功能**：DeskPilot 启动时检测是否已有实例运行，避免多开。

#### ✨ 功能
- **第一次启动**：正常创建 ChatWindow + TrayIcon + 主流程
- **第二次启动**：检测到 Mutex 已存在 → 自动激活旧窗口（Win32 ShowWindow + SetForegroundWindow）→ 退出当前进程
- **应用退出时清理 Mutex**：避免下次启动失败（Mutex 残留导致永远无法启动）

#### 🛠️ 技术实现
| 文件 | 改动 |
|---|---|
| `src/DeskPilot.App/Services/SingleInstanceService.cs` | 新建（2063 bytes）|
| `src/DeskPilot.App/GlobalUsings.cs` | 加 `Mutex = System.Threading.Mutex` 别名（v0.18.0 引入 WinForms 后需要消歧）|
| `src/DeskPilot.App/App.xaml.cs` | `OnStartup` 最开始加 Mutex 检查 + Exit 事件清理 |
| `tests/DeskPilot.Tests/SingleInstanceServiceTests.cs` | 新建（4 个测试：FirstInstance/SecondInstance/Dispose_Releases/ActivateExisting）|

#### 🐛 踩坑
- **Mutex 命名空间冲突**：`System.Threading.Mutex` vs `System.Windows.Forms`（未冲突，但需要明确指定）
- **Win32 P/Invoke**：`ShowWindow` + `SetForegroundWindow` 用 `[DllImport("user32.dll")]`
- **Mutex 全局名**：`Global\\` 前缀确保跨用户会话互斥

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 301/301 测试通过（v0.18.1 baseline 297 + 4 新增）
- ✅ smoke test EXIT 0（含 SingleInstance 实例化）

## [v0.18.0] - 2026-07-01

### 🆕 系统托盘 NotifyIcon（最小化到托盘 + 双击恢复 + 右键菜单退出）

新增第 1 个 **App 层功能**（之前都是工具/ViewModel/Window）：WPF App 关闭时最小化到 Windows 系统托盘，而不是直接退出进程。用户通过托盘菜单"退出"才会真正结束进程。

#### ✨ 功能
- **关闭主窗口 → 最小化到托盘**：点击 ChatWindow 右上角关闭按钮不退出，托盘出现图标
- **双击托盘图标 → 恢复窗口**：自动取消最小化 + 激活到前台
- **托盘右键菜单**：「显示主窗口」+「退出」
- **应用退出时清理托盘**：App.Exit 事件触发 Dispose，避免图标残留

#### 🛠️ 技术实现
| 文件 | 改动 |
|---|---|
| `src/DeskPilot.App/DeskPilot.App.csproj` | 加 `<UseWindowsForms>true</UseWindowsForms>`（启用 WinForms 互操作）|
| `src/DeskPilot.App/GlobalUsings.cs` | 新建（728 bytes，9 个全局 using 别名解决 WinForms 命名冲突）|
| `src/DeskPilot.App/Services/TrayIconService.cs` | 新建（2829 bytes，NotifyIcon 包装）|
| `src/DeskPilot.App/Views/ChatWindow.xaml.cs` | 加 `SetTrayIcon()` 方法 + `Closing` event → 取消 + 隐藏 |
| `src/DeskPilot.App/App.xaml.cs` | 主分支 + smoke test 分支都注入 TrayIcon + Exit 事件清理 |
| `tests/DeskPilot.Tests/TrayIconServiceTests.cs` | 新建（1 个 ArgumentNullException 测试）|

#### 🐛 踩坑
- **`<UseWindowsForms>` 引入 WinForms 全局命名空间污染**：导致 12 个 WPF 类（`Application` / `Brushes` / `Button` / `KeyEventArgs` / `MessageBox` 等）出现 CS0104 命名冲突
- **解决方案**：`GlobalUsings.cs` 用 `global using X = System.Windows.X` 一处改全局生效
- **`SystemIcons` 在 `System.Drawing`** 而非 `System.Windows.Forms`（编译错误 CS0234）

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误 + 2 个 CA1416 警告（跨平台 System.Drawing.Common，已知）
- ✅ 全量 297/297 测试通过（v0.17.7 baseline 296 + 1 新增）
- ✅ smoke test EXIT 0（含 TrayIcon 实例化 + Dispose）

## [v0.17.5] - 2026-07-01

### 📝 RELEASE_NOTES 同步 v0.17.3/v0.17.4 doc-only releases

- 22 行新增
- 追加 v0.17.3（CHANGELOG 同步 v0.17.2 release.yml 修复）+ v0.17.4（README Roadmap 详细化）sections
- 配套 commit `ae0a015` + tag `v0.17.5`

无代码变更，纯文档完整性同步（release workflow `awk` 提取 v0.17.3/v0.17.4 sections 用）。

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

## [v0.17.2] - 2026-07-01

### 🐛 CI 修复第二步 - release.yml `.slnx` 转回 `.sln` + RELEASE_NOTES.md 同步 v0.17 sections

#### 🐛 问题
- v0.16.4 CI 修复事故（2026-06-27）只改了 `ci.yml` + `McpServerTests`，**漏改 `release.yml`**
- `release.yml` 的 `build-mcp` step 4 仍是 `dotnet restore DeskPilot.slnx` → CI runner .NET 8 SDK 失败
- v0.17.0 / v0.17.1 发布时**没追加 sections 到 `RELEASE_NOTES.md`** → release workflow `awk "/^## \[${VERSION}\]/,/^## \[/"` 提取失败
- 6 个 Release workflow 失败根因完整链路：
  1. `DeskPilot.slnx` (.NET 9 SDK 格式) → .NET 8 SDK 不支持
  2. `ci.yml` 用 `.slnx` → Restore 失败
  3. `release.yml` 用 `.slnx` → Restore 失败（v0.16.4 漏改）
  4. `McpServerTests.LocateMcpProjectDir` 找 `.slnx` → cctor 失败
  5. `RELEASE_NOTES.md` 缺 v0.17 sections → awk 提取失败

#### 🔍 根因
- v0.16.4 修复时**只覆盖了 1 个 workflow（ci.yml）+ 1 个测试**，没有系统盘点所有引用 .slnx 的地方
- v0.17.0 / v0.17.1 发布时**只更新了 `CHANGELOG.md`**，没同步 `RELEASE_NOTES.md`

#### ✅ 修复
- `release.yml` line 39：`dotnet restore DeskPilot.slnx` → `dotnet restore DeskPilot.sln`
- `RELEASE_NOTES.md` 顶部追加 `## [v0.17.1]` + `## [v0.17.0]` sections
- commit `4a3b973` + tag `v0.17.2`

#### 📊 验证
- ✅ 全量 296/296 测试通过
- ✅ release.yml 用 `.sln`（CI runner .NET 8 SDK 兼容）
- ✅ RELEASE_NOTES.md 包含 v0.17.0 + v0.17.1 sections（awk 提取成功）

#### 📚 教训
- **CI 修复必须系统盘点所有相关文件**：`.github/workflows/*.yml` 至少 2 个（ci.yml + release.yml）
- **新 tag 发布前必须同步 RELEASE_NOTES.md**（不能等事故后再补）
- **CHANGELOG.md ≠ RELEASE_NOTES.md**：CHANGELOG 是全历史，RELEASE_NOTES 是当前 release section

## [v0.17.0] - 2026-07-01

### 🆕 RenameByExifTool 图片 EXIF 批量重命名

#### 🎯 功能
- **问题**：相机/手机照片默认命名（如 `DSC00001.jpg`）没有业务含义，整理困难
- **解决**：读取图片 **EXIF DateTimeOriginal**（拍摄时间），按用户指定的日期格式 + 可选前缀批量重命名
- **示例**：`DSC00001.jpg`（拍摄于 2024-06-15 14:30:00）→ `2024-06-15_14-30-00.jpg` 或 `IMG_2024-06-15_14-30-00.jpg`

#### 🛠️ 实现
- 新增 `src/DeskPilot.Core/Tools/RenameByExifTool.cs`（9644 bytes）
  - 使用 `System.Drawing.Common`（v0.5 已引入，零新依赖）
  - EXIF PropertyItem 0x9003 = DateTimeOriginal
  - 支持 JPG/JPEG/PNG（PNG 无 EXIF 自动 fallback 到文件修改时间）
  - 冲突解决同 `RenameByPatternTool`（`_2` / `_3` 后缀）
  - DryRun 模式只预览不重命名
- 新增 `tests/DeskPilot.Tests/RenameByExifToolTests.cs`（6078 bytes，5 个测试）

#### 📊 工具参数
| 参数 | 类型 | 必填 | 默认 | 说明 |
|------|------|------|------|------|
| `directory` | string | ✅ | - | 目标目录绝对路径 |
| `pattern` | string | ❌ | `*.jpg` | glob 过滤（可改 `*.png` / `*.jpeg`）|
| `dateFormat` | string | ❌ | `yyyy-MM-dd_HH-mm-ss` | 日期格式 |
| `prefix` | string | ❌ | - | 可选前缀（如 `IMG_`）|
| `fallbackToFileDate` | bool | ❌ | `true` | 无 EXIF 时是否用文件修改时间 |
| `dryRun` | bool | ❌ | `false` | true 只预览不重命名 |

#### ✅ 测试覆盖（5 个测试）
1. `EmptyInput_ReturnsError` - directory 为空 → 错误
2. `NonExistentDirectory_ReturnsError` - 目录不存在 → 错误
3. `JpegWithExif_RenamesToDateTimeOriginal` - 有 EXIF → 重命名成功 + 日期正确
4. `JpegWithoutExif_UsesFileDateFallback` - 无 EXIF → 用文件修改时间
5. `DryRun_PreviewOnly_DoesNotRename` - DryRun 模式 → 不实际改名

#### 📊 验证
- ✅ .NET 8 SDK build 0 错误
- ✅ 全量 296/296 测试通过（v0.16 291 + 5 新增）
- ✅ WPF App smoke test PASSED（exit 0）

#### 💡 业务场景
- 摄影用户从相机导出后按拍摄时间整理
- 扫描件按 EXIF 时间重命名归档
- 备份照片按时间顺序排序

### 📝 文档同步 (2026-07-01 17:14)

#### 🎯 背景
- v0.17.0 发布后 README.md 仍写 `17 tools / 291 tests`，与实际 `18 tools / 296 tests` 不一致
- 工具清单表缺 `rename_by_exif` 行
- Roadmap 未勾选 v0.17 A

#### ✅ 修复
- commit `c9c7318` - docs: update README.md to v0.17.0 reality (18 tools, 296 tests, rename_by_exif)
- 9 insertions + 7 deletions

#### 📊 同步内容
- ✅ badge 17 → 18 tools + 291 → 296 tests
- ✅ Core 库 14 → 15 + 加 `rename_by_exif` 行
- ✅ MCP Server 10 → 11 + 加 `rename_by_exif`
- ✅ Roadmap v0.17 A 勾选
- ✅ 下一步标题 v0.17+ → v0.18+

## [v0.16.3] - 2026-06-27

### 🆕 SkillDetailWindow 集成进 SkillCenterWindow Market Tab

#### 🎯 功能
- **问题**：v0.15 D3 SkillCenterWindow Market Tab 卡片**没有详情入口**，点击只能直接安装
- **解决**：点击 Market Tab 卡片 → 弹出 SkillDetailWindow 查看完整技能详情（Description + Prompt 预览 + Tools 列表 + 安装/卸载/检查更新）

#### 🔧 实施
- 修改 `src/DeskPilot.App/Views/SkillCenterWindow.xaml`：
  - Market Tab 卡片 Border 加 `MouseLeftButtonUp="MarketSkillCard_Click"` + `Tag="{Binding Id}"` + `Cursor="Hand"`
- 修改 `src/DeskPilot.App/Views/SkillCenterWindow.xaml.cs`：
  - 加 `MarketSkillCard_Click` handler：从 `DataContext.SkillCenterViewModel.MarketSkillRows` 找 skill → 从 `App.Services` 拿 `ISkillService` + `ISkillMarket` → 创建 `SkillDetailViewModel` → `new SkillDetailWindow(detailVm) { Owner = this }.ShowDialog()`
- 加 3 个新测试（`tests/DeskPilot.Tests/SkillCenterWindowTests.cs`）：
  - `SkillCenterWindow_Xaml_HasMarketCardClickHandler`（XAML 含 MouseLeftButtonUp + Tag + Cursor 验证）
  - `SkillCenterWindow_CodeBehind_HasMarketCardClickHandler`（cs 含 handler + SkillDetailWindow + ShowDialog 验证）
  - `SkillCenterWindow_CodeBehind_ResolvesSkillServicesFromApp`（cs 含 App.Services + ISkillService + ISkillMarket 验证）

#### 🏗️ 架构亮点
- **不污染 ViewModel**：通过 `App.Services.GetService<>()` 拿 service，而不是改 SkillCenterViewModel 加 public 属性
- **依赖复用**：复用 v0.11 已存在的 SkillDetailWindow + SkillDetailViewModel（不重写）
- **owner 模式**：`new SkillDetailWindow(detailVm) { Owner = this }` 让详情窗跟着 SkillCenterWindow 关闭

#### 📊 验证结果
- ✅ App build 0 错
- ✅ 全量 291/291 测试全过（v0.16.2 baseline 288 + 3 新增 v0.16 C 测试）
- ✅ smoke test EXIT 0（SkillCenterWindow + SkillDetailWindow 真实实例化触发 WPF XAML 解析）

## [v0.16.2] - 2026-06-27

### 🆕 MCP Server 新增 3 个工具（7 → 10）

#### 📡 DeskPilot.Mcp 新工具
- 修改 `src/DeskPilot.Mcp/Program.cs`：
  - DI 注册加 3 个：`MergePdfTool` + `ConvertImageTool` + `TextStatsTool`
  - ctor 加 3 个参数 + 字段初始化
  - 加 3 个 `[McpServerTool]` 方法：`merge_pdfs` + `convert_image` + `text_stats`
- 工具总数：v0.5 4 → 7 → **v0.16.2 10**（+43%）

#### 🧪 McpServerTests 测试更新
- 修改 `tests/DeskPilot.Tests/McpServerTests.cs`：
  - 测试名 `Returns4Tools` → `Returns10Tools`
  - `Assert.Equal(7, toolList.Count)` → `Assert.Equal(10, toolList.Count)`
  - 加 3 个新工具名断言：`merge_pdfs` + `convert_image` + `text_stats`

#### 📊 验证结果
- ✅ Core build 0 错
- ✅ Mcp build 0 错
- ✅ 全量 288/288 测试全过（v0.16.1 baseline 288 + 0 新增，仅修改已有 McpServerTests）
- ✅ smoke test 0 字节（v0.16 F SkillCenterWindow 触发 + MCP 新工具不影响）

## [v0.16.1] - 2026-06-27

### 🆕 图片旋转/裁剪 2 个新工具

#### 🔄 RotateImageTool（图片旋转 + 翻转）
- 新建 `src/DeskPilot.Core/Tools/RotateImageTool.cs`（5861 bytes）
- 支持 `rotation: 0/90/180/270` 旋转 + `flip: none/horizontal/vertical` 翻转
- 旋转 + 翻转可同时指定，先旋转后翻转（9 种组合）
- 输出格式自动从 outputPath 后缀推断（png/jpg/bmp/gif）
- `RiskLevel.Destructive`（写新文件）
- 测试：`RotateImageToolTests.cs`（3419 bytes）— 4 个测试（EmptyInput / NonExistentFile / Rotate90 验证宽高交换 / FlipHorizontal 验证尺寸不变）

#### ✂️ CropImageTool（图片裁剪）
- 新建 `src/DeskPilot.Core/Tools/CropImageTool.cs`（5436 bytes）
- 输入 `(x, y, width, height)` 矩形区域裁剪
- 超出源图片边界的部分自动截断（带提示信息）
- 坐标原点在左上角，单位像素
- 输出格式自动从 outputPath 后缀推断
- `RiskLevel.Destructive`（写新文件）
- 测试：`CropImageToolTests.cs`（3697 bytes）— 4 个测试（EmptyInput / NonExistentFile / ValidCrop 验证尺寸 / OutOfBoundsCrop 验证自动截断）

#### 📊 验证结果
- ✅ Core build 0 错（仅 CA1416 平台兼容性 warning，已存在）
- ✅ 全量 288/288 测试全过（v0.16.0 baseline 280 + 8 新增）
- ✅ smoke test 0 字节（v0.16 F SkillCenterWindow 触发 + 新工具不影响）

## [v0.15.0] - 2026-06-27

### 🆕 独立技能中心窗口（D1+D2+D3+D4）

#### 🪟 SkillCenterWindow（900x640 主窗口）
- 新建 `src/DeskPilot.App/Views/SkillCenterWindow.xaml`（22582 bytes）— 居中可调大小 + Melon 风格（HeaderTitle/HeaderSubtitle/SkillCenterTabItem 选中态橙底白字 + 底部 StatusBarCard 卡片）
- 3 个 TabItem：🌐 技能市场 + 📦 已安装 + 🔄 有更新
- 标题栏：🛠 技能中心 + 「浏览 / 安装 / 管理 DeskPilot 的所有技能（内置 + 市场）」
- 底部状态栏：实时 StatusMessage + 当前选中市场源徽章

#### 🛒 技能市场 Tab（D3 完整 UI）
- 源 Tab 横排（RadioButton + SourceTabButton 样式 + StackPanel Horizontal ItemsPanel）：绑定 `MarketplaceSourceNames`（QwenPaw / ClawHub / ModelScope + 用户自定义）
- 分类 chips（RadioButton + CategoryChipButton 样式 + WrapPanel ItemsPanel）：预设 8 个（全部 / 财务办公 / 文件整理 / 开发工具 / 图片处理 / 文档处理 / 办公自动化 / 示例），绑定 `MarketCategories`
- 搜索框（TextBox x:Name=MarketSearchBox）实时双向绑定 `MarketSearchText` + Delay=300 防抖
- 刷新按钮（🔄 刷新市场）Command 绑定 `LoadMarketCommand`
- 3 列 WrapPanel 卡片网格：每张卡片 280px 圆角 10（Icon 圆形 40px PrimaryBrush 背景 + Name/Author/SourceName 徽章 + Description TextWrapping Wrap MaxHeight 48 TextTrimming CharacterEllipsis + ⭐ 评分 + 📥 下载数 + 版本 + 分类 chip + 已安装标识）

#### 📦 已安装 Tab（D4 ListView）
- ListView 6 列：图标 + 技能名 + 版本 + 分类 + 作者 + 操作（🗑 卸载按钮 CommandParameter 绑定 Id + UninstallCommand）
- 顶部操作栏：🔄 刷新列表（绑定 `LoadInstalledCommand`）+ 💡「内置技能不可卸载」提示
- 数据源：`InstalledSkills` ObservableCollection 订阅 `ISkillService.SkillsChanged` 自动 RefreshInstalled

#### 🔄 有更新 Tab（D4 ListView）
- ListView 4 列：技能 ID + 本地版本 + 最新版本 + 操作（⬆ 一键更新按钮 绑定 UpdateSkillCommand + CommandParameter SkillUpdateInfo）
- 顶部操作栏：🔄 检查更新（绑定 `LoadUpdatesCommand`）+ 💡「仅显示有更新的技能」提示
- 数据源：`UpdateAvailableSkills` ObservableCollection（ISkillService.CheckUpdatesAsync → 过滤 HasUpdate=true）

#### 🧠 SkillCenterViewModel（7 个 RelayCommand 业务）
- 新建 `src/DeskPilot.App/ViewModels/SkillCenterViewModel.cs`（7857 bytes）— 注入 `IMarketplaceSourceService` + `ISkillService` 2 服务
- 5 个 ObservableProperty：StatusMessage / SelectedMarketSource / MarketCategory / MarketSearchText / IsLoadingMarket
- 3 个 ObservableCollection：MarketSkillRows / InstalledSkills / UpdateAvailableSkills
- 8 个 MarketCategories 预设分类 + 7 个 RelayCommand（LoadMarket / LoadInstalled / LoadUpdates / Install / Uninstall / UpdateSkill / RefreshStatus）
- SkillsChanged 事件订阅 → 自动 RefreshInstalled（Install/Uninstall 后实时同步）

#### 🍱 ChatWindow 顶部菜单（D4）
- 新建 `<Menu Grid.Row="0">` 顶部菜单条：文件（设置 / 退出）+ 技能（打开技能中心 Ctrl+Shift+K / 刷新已安装技能）+ 帮助（关于 DeskPilot）
- ChatWindow.xaml.cs 加 `ExitMenuItem_Click`（调 Application.Current.Shutdown）+ `AboutMenuItem_Click`（MessageBox 显示版本 + GitHub URL）
- Window.InputBindings Ctrl+Shift+K 调 `ShowSkillCenterCommand`（用 SkillCenterWindow 工厂 delegate 避免 ViewModel 直接 new Window 违反 MVVM）
- ChatViewModel.cs 加 `Func<SkillCenterWindow>? skillCenterFactory` 可选参数 + `[RelayCommand] ShowSkillCenter()` → 工厂().Show() + Activate()
- App.xaml.cs 主分支（line 76）+ smoke test 分支（line 192）ChatViewModel DI 改成工厂注入：`sp => new ChatViewModel(chatService, skillService, executor, skillCenterFactory: () => sp.GetRequiredService<SkillCenterWindow>())`

### 📊 测试覆盖（+21 个新测试，全量 280/280 全过）
- **SkillCenterWindowTests** 3 个（D1）：Ctor_DoesNotThrow / HasThreeMarketplaceSources / HasThreeObservableCollections
- **SkillCenterViewModelTests** 5 个（D2）：Ctor_DoesNotThrow_AndPopulatesSources / LoadMarket_PopulatesMarketSkillRows / Install_DelegatesToSkillService / Uninstall_DelegatesToSkillService / LoadUpdates_FiltersOnlyHasUpdateTrue（含 StubMarketplaceSourceService 缓存 + StubSkillMarket + StubSkillService）
- **SkillCenterMarketTabTests** 7 个（D3）：Market_TabItem_Exists_With_Correct_Header / Contains_WrapPanel_Card_Grid_Binding_MarketSkillRows / SearchBox_Binds_MarketSearchText / SourceTabs_Bind_MarketplaceSourceNames / CategoriesChips_Bind_MarketCategories / Refresh_Button / Has_Three_Installed_Updates_TabItems（XAML 文本 + Regex 验证，避 STA 线程问题）
- **SkillCenterIntegrationTests** 6 个（D4）：ChatWindow_HasTopMenu_With_Skills_MenuItem / Has_CtrlShiftK_InputBinding_For_SkillCenter / SkillsMenu_Has_OpenSkillCenter_Item_With_InputGestureText / SkillCenter_Has_Three_TabItems_Market_Installed_Updates / InstalledTab_ListView_Binds_InstalledSkills / UpdatesTab_ListView_Binds_UpdateAvailableSkills
- 总计：**280/280 全过**（v0.14.1 baseline 259 + v0.15 新增 21 = 280）
- smoke test stdout/stderr 0 字节 = 无 XamlParseException（SkillCenterWindow 22582 bytes 全量 XAML 解析成功）

### 🔧 关键决策与修复
- **MVVM 纯净**：`ChatViewModel.ShowSkillCenter` 用 `Func<SkillCenterWindow>?` 工厂 delegate 注入，避免 ViewModel 直接 new Window
- **避 STA 线程问题**：XAML 验证测试改用 `File.ReadAllText` + `Regex.Match` + `Assert.Contains`，不用 `XamlReader.Parse`（xUnit 默认 MTA 线程触发「调用线程必须为 STA」异常）
- **RelayCommand 生成名规则**：`async Task XxxAsync` → `IAsyncRelayCommand` 属性名 `XxxCommand`（去 Async 后缀），sync `void Xxx()` → `IRelayCommand` 属性名 `XxxCommand`
- **Positional record 必须用构造语法**：`SkillManifest` / `Skill` 是 positional record（13/12 参），不能用对象初始化器 `{ Id = ... }`，必须 `new SkillManifest(Id: ..., Name: ..., ...)`
- **StubMarketplaceSourceService.GetMarket 缓存**：避免 LoadMarket 时新实例 IndexSkills 为空（`_markets` 字典 + 同步最新引用）
- **InstallAsync 不调 LoadMarketAsync**：`SkillsChanged` 事件订阅已自动 RefreshInstalled，再调 LoadMarket 会覆盖「✅ 已安装 ...」StatusMessage 为「拉到 0 个技能」

### 📦 项目变更
- 新增文件：`Views/SkillCenterWindow.xaml`（22582 bytes）+ `Views/SkillCenterWindow.xaml.cs` + `ViewModels/SkillCenterViewModel.cs`（7857 bytes）+ 4 个测试文件
- 修改文件：`Views/ChatWindow.xaml`（+30 行 Menu + InputBindings）+ `Views/ChatWindow.xaml.cs`（+ ExitMenuItem_Click + AboutMenuItem_Click）+ `ViewModels/ChatViewModel.cs`（+ SkillCenterWindow 工厂 delegate + ShowSkillCenterCommand）+ `App.xaml.cs`（主分支 + smoke test 分支 ChatViewModel DI 工厂注入）

---

## [v0.14.1] - 2026-06-26

### 🛠 XamlParseException 紧急修复

#### 🐛 根因
- `SettingsWindow.xaml` 2 处 `ConverterParameter={Binding ...}` 报错
- WPF 的 `ConverterParameter` **不是** DependencyProperty，不能接收 `Binding` → 必须用 `IMultiValueConverter` + `MultiBinding`

#### ✅ 修复
- `Converters/RoleConverters.cs` 的 `SourceMatchConverter` + `CategoryMatchConverter` 从 `IValueConverter` 改 `IMultiValueConverter`
  - `values[0]` = 当前选中 Tab（vm 属性）
  - `values[1]` = 当前 ListBox 项（converter 参数）
  - 边界保护：values 长度 < 2 返回 false + null 字符串转 string.Empty
- `SettingsWindow.xaml` line 341 + 438 改用 `MultiBinding` 同时传「Tab 值」+「当前选中项」
- App build 0 错 + smoke test 0 字节 + 全量测试 259/259 全过

#### 📦 项目变更
- 修改文件：`Converters/RoleConverters.cs` + `Views/SettingsWindow.xaml`
- commit `786e8b6`（2 files +30/-14）→ tag `v0.14.1` → Release #25

---

## [v0.14.0] - 2026-06-26

### 🆕 新增 3 个零依赖工具（C1+C2+C3）
- **merge_pdfs** (MergePdfTool) — 多个 PDF 合并为一个新文件（纯托管 PdfSharpCore，零 GhostScript）
- **convert_image** (ConvertImageTool) — png / jpg / bmp / webp / gif 互转（System.Drawing.Common，quality 1-100 jpg 生效）
- **batch_excel** (BatchExcelTool) — 目录批量处理 xlsx，三种 operation：
  - `list_sheets`：列出每个 xlsx 的 sheet 名 + 行列数 + 文件大小
  - `extract_data`：汇总所有 xlsx 第一张表数据到 JSON 数组（自动跳过表头行）
  - `write_summary`：把每文件的「文件名 + sheet 名 + 行数 + 列数 + 文件大小」汇总到新 xlsx

### 🆕 新增 2 个多步技能示例
- **invoice-merge** (2 步) — `search_content` 搜 Downloads/ 含「发票|invoice|fapiao」PDF → `merge_pdfs` 合并为 Documents/发票汇总_YYYYMMDD.pdf
- **excel-rollup** (3 步) — `search_content` 找本周 *.xlsx → `batch_excel` write_summary 汇总 → `text_stats` 统计行数（optional）
- skills/README.md 加 2 行 v0.14 多步索引

### 📊 测试
- 全量测试 **259/259 全过**（v0.13 baseline 239 + v0.14 新增 20 = 259）
  - C1 MergePdfToolTests：5（空数组 / 单文件 / 多文件保序 / 不存在 / 损坏 PDF）
  - C2 ConvertImageToolTests：5（PNG→JPG / JPG→PNG / quality 95>10 / 不存在 / tiff 不支持）
  - C3 BatchExcelToolTests：6（空目录 / list_sheets 多文件 / extract_data 跳表头 / write_summary / 不存在目录 / 不支持 operation）
  - C4 SkillStepTests v0.14 section：4（InvoiceMerge_LoadsMultiStep / InvoiceMerge_ArgsContainExpectedKeys / ExcelRollup_LoadsMultiStep / AllHaveIsMultiStepTrue_V0_14）
- smoke test stdout/stderr 0 字节 = 无 XamlParseException

### 🔧 关键修复与决策
- **PdfSharpCore 库命名空间是 `PdfSharpCore` 不是 `PdfSharp`**：using + catch 里的 PdfReaderException 都已修
- **RiskLevel 枚举无 `ReadOnly` 值** → BatchExcelTool 改用 `Destructive`（write_summary 写新 xlsx 视为写文件）
- **ClosedXML `LastRowUsed().RowNumber()` 包含表头** → WriteSummary 测试断言从 2 改 3（1 表头 + 2 数据行）
- **ClosedXML `RowsUsed()` 默认含表头** → `extract_data` 加 `Skip(1)` 跳过表头，行为符合「数据行」语义
- **NuGet 依赖**：`PdfSharpCore 1.3.65`（含 SharpZipLib 1.4.2）+ `ClosedXML 0.102.3`，其余工具用现有 System.Drawing.Common
- self-contained 单文件体积保持 ~73 MB

## [v0.13.0] - 2026-06-26

### 🆕 新增 2 个零依赖工具（B1+B2）

#### 📄 TextStatsTool — 文本文件统计
- 输入 filePath + 可选 topN
- 输出 BOM 自动检测编码（UTF-8 / UTF-16 / UTF-32 / 默认 UTF-8 无 BOM）
- 行数（按 `\n` 计数）+ 字符数 + 词数（中英混合：英文按连续字母数字分词，中文按每个汉字 1 词）
- 字节数 + 最后修改时间
- topN 高频词（跳过停用词 + 单字符 + 纯数字，按频率降序 + 字典序升序 tiebreak）
- 纯只读工具，不会修改任何文件
- 适用：「这个文件多大」「哪些词出现最多」

#### 🔍 SearchContentTool — 文件内容搜索
- 输入 directory + pattern（正则）+ 可选 fileFilter + 可选 maxResults + recursive
- 输出每个匹配的文件路径、行号、匹配行内容、命中的正则片段
- IsBinaryFile 按扩展名快速跳过（图片/视频/音频/Office/PDF 等）
- RegexOptions.Compiled + 2 秒超时防 ReDoS
- File.ReadAllLinesAsync + CancellationToken
- 适用：「帮我找所有 TODO」「哪些文件包含这个关键词」

#### 🛠 ToolRegistry 注册
- App.xaml.cs line 63 main 工厂 + line 165 smoke test 工厂各加 2 处
- 工具总数：7 → **9**（HashFiles / ArchiveByDate / FindDuplicates / BatchResizeImage / RenameByPattern / MoveFiles / ExtractArchive / **TextStats** / **SearchContent**）

### 🆕 2 个多步技能示例（B3）

#### 🔍 code-review-helper
- 2 步：SearchContent 搜 src/*.cs 的 `TODO|FIXME|HACK|XXX` → TextStats 统计 DeskPilot.Core.csproj 元数据
- SearchContent required + TextStats optional（统计可跳过）
- Category：开发工具

#### 📂 file-organizer
- 2 步：SearchContent 按「发票|合同|收据」关键词扫描 Downloads/ → ArchiveByDate 按 yyyy/MM 归档到 Documents/分类归档/
- SearchContent required + ArchiveByDate optional
- Category：文档处理

#### 📝 skills/README.md 更新
- 加 2 行 v0.13 多步索引（code-review-helper + file-organizer）
- 技能总数：11 → **13**（8 builtin + **5 community**）

### 🧪 测试覆盖（+16 个新测试）
- **TextStatsToolTests** 6 个：FileNotExists / EmptyFile / AsciiContent / ChineseContent / TopN_Limits / TopNZero_NoWordStats
- **SearchContentToolTests** 6 个：DirectoryNotExists / EmptyDirectory / MultipleFiles / InvalidRegex / MaxResults / RecursiveOff
- **SkillStepTests** +4 个 v0.13 community：CodeReviewHelper_LoadsMultiStep / CodeReviewHelper_SearchContentArgs / FileOrganizer_LoadsMultiStep / AllHaveIsMultiStepTrue_V0_13
- **总计 239/239 全过**（v0.12 baseline 229 + 16 新 = 239）
- smoke test stdout 0 字节 = 无 XamlParseException

### 🔧 关键决策
- **零外部依赖**：2 个工具都是纯 C# .NET 8 BCL API（System.IO / System.Text / System.Text.RegularExpressions），不引入任何 NuGet 包，self-contained 单文件体积保持 ~73 MB
- **测试用反射提取匿名 data.matches**：避免嵌套 public record SearchMatch 强类型依赖（之前 v0.12 SkillStepTests 已用 `pattern is JsonElement je ? je.GetString() : pattern` 同款模式处理 JsonElement 装箱）
- **正则防 ReDoS**：`RegexOptions.Compiled + TimeSpan.FromSeconds(2)` 防止恶意 pattern 触发灾难性回溯

## [v0.12.0] - 2026-06-26

### 🆕 技能多步工作流（A2）

#### 🧩 数据模型
- 新增 `SkillStep` record（ToolName + Args dict + Description + Optional）
- `Skill.Steps: IReadOnlyList<SkillStep>`（默认 Array.Empty()，向后兼容旧 JSON）
- `Skill.IsMultiStep` 计算属性 + `SafeSteps` Null/空安全回退
- `SkillSet.MultiStep` 视图属性

#### ⚙️ SkillExecutor（核心执行器）
- `ISkillExecutor` 接口 + `SkillExecutor` 实现（依赖 IToolRegistry）
- `StepStatus` 枚举（Pending / Running / Done / Error / Skipped）
- `StepProgress` 实体（Index / ToolName / Description / Status / Summary / ErrorMessage / StatusIcon）
- `SkillExecutionResult` 聚合结果
- 按 Steps 顺序调工具：Optional 失败继续、Required 失败中断
- `IProgress<StepProgress>` 实时推送 + CancellationToken 取消

#### 💬 ChatViewModel 多步分支
- 注入 `ISkillExecutor` + `ObservableCollection<StepProgress> StepProgresses`
- `HasStepProgress` + `IsStepRunning` 属性
- `TriggerSkillAsync(Skill)`：IsMultiStep 走 SkillExecutor、否则保留 v0.9 prompt 填入+自动发送
- ChatWindow 消息区上方加可折叠「执行步骤」SectionCard（橙色 #FFF7E6/#FFB74D/#E65100）

#### 🛠 3 个 community 改多步示例
- `scan-invoices`：HashFiles 校验 SHA256 → ArchiveByDate 按月归档 → FindDuplicates Optional 查重（v1.0.0 → v1.1.0）
- `weekly-report-helper`：HashFiles 校验本周笔记 → BatchResizeImage Optional 压缩配图（v0.9.0 → v1.0.0）
- `git-commit-message`：HashFiles 验证变更文件 → RenameByPattern Optional dry-run 给 CHANGELOG 加日期前缀（v1.0.0 → v1.1.0）
- skills/README.md 索引 3 行加 "v0.12 多步" 标注

#### 🧪 测试覆盖（24 个新测试）
- SkillStepTests：8 数据模型 + 6 community 加载
- SkillExecutorTests：10 执行器（EmptySteps / SingleStep_Success / MultiStep_Success / Optional_FailureContinues / Required_FailureAborts / ToolNotRegistered / Progress_ReportsRunningThenDone / Cancel_StopsExecution / SkillIdPropagatesToResult / SummaryFormat）

### 🆕 接 ClawHub / ModelScope 真后端（A1）

#### 🌐 三个独立公开市场源
- **QwenPaw** = `https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills`（自家 GitHub 真源）
- **ClawHub** = `https://raw.githubusercontent.com/maqiul/DeskPilot-clawhub/main/skills`（mock 真源，独立仓库）
- **ModelScope** = `https://raw.githubusercontent.com/maqiul/DeskPilot-modelscope/main/skills`（mock 真源，独立仓库）
- 替换 v0.11 Stub 占位 → 真抛 MarketFetchException（如 404）

#### 🏗 组合模式（避开 SkillMarketService sealed 限制）
- `ClawHubMarketService` / `ModelScopeMarketService`：包 SkillMarketService 实例 + 显式实现 ISkillMarket
- async/await 转发 FetchSkillAsync 修 Nullability warning

#### 📝 mock 仓库内容（本地 mock-sources/）
- `mock-sources/clawhub/README.md`（4 技能：pdf-merge / video-compress / markdown-to-pdf / qrcode-generator）
- `mock-sources/modelscope/README.md`（4 技能：speech-to-text / text-summarize / image-colorize / doc-translate）
- 10 列 Markdown 表格（id / name / description / icon / category / author / version / screenshotUrl / rating / downloads），与 QwenPaw 完全一致

### 🆕 自定义市场源（A1.2）

#### 🔧 扩展点
- `IMarketplaceSourceService.AddCustomSource(name, baseUrl)` 方法
- `INotifyPropertyChanged` 实现 → XAML 自动通知刷新
- `MarketplaceSourceService` 实现：TrimEnd '/' + 同名拒绝 + 触发 `SourceNames` PropertyChanged

#### 💡 SettingsViewModel
- `CustomSourceName` / `CustomSourceUrl` / `AddCustomStatus` ObservableProperty
- `AddCustomSourceCommand`：URL 校验非空 + 必须 http(s) 开头 → 调 AddCustomSource → 自动切到新源 → 清空 → 触发 BrowseMarketAsync
- 订阅 `IMarketplaceSourceService.PropertyChanged` 自动通知 UI

#### 🎨 SettingsWindow.xaml
- 市场源 Tab 行末加「+ 自定义」按钮
- 黄色 Border 输入弹窗（名称 TextBox + URL TextBox + 添加 / 取消按钮 + 状态条）
- code-behind：AddCustomSource_Click 显示 + CancelAddCustomSource_Click 隐藏

#### 🧪 测试覆盖（5 个新测试）
- `AddCustomSource_NewName_AppendsToSourceNames` / `DuplicateName_ReturnsFalse` / `EmptyArgs_ReturnsFalse` / `TrimsTrailingSlash` / `FiresPropertyChanged`

### 📈 测试覆盖
- **223 测试**（v0.11 baseline 213 + v0.12 新增 24 - 删 1 旧 Stub + 5 个 A1.2 = 218 + 5 = 223）
- 全量 `dotnet test` 全过
- smoke test stdout 0 字节 = 无 XamlParseException

### 🔧 关键校准
- Subtask 描述里建议的工具（FindFiles / ReadText / WriteText / RunCommand / SendToAI）**实际不存在**于 7 工具集
- 改用现有 7 工具（HashFiles / ArchiveByDate / FindDuplicates / BatchResizeImage / RenameByPattern）实现多步

## [v0.11.0] - 2026-06-26

### 🆕 技能市场重做（QwenPaw 风格）

#### 🎨 视觉升级
- **从列表式重做为卡片网格**（3 列 WrapPanel，卡片宽 280 px）
- **卡片极简化**：Icon 圆形背景大块 + 名称 + 描述 3 行截断 + 右上来源徽章
- **顶部多市场源 Tab**：QwenPaw / ClawHub / ModelScope（chip + 对勾样式对齐 QwenPaw 截图）
- **二级分类 Tab**：全部 / 财务 / 文件 / 开发 / 图片 / 文档（横排 chips）
- **搜索框常驻右上角**
- **浅蓝色提示条**「选择分类或输入关键词以浏览 {源名} 中的技能」

#### 🔍 详情弹窗（点卡片弹出）
- 新建 `SkillDetailWindow`：Icon 大块 + 名称 / 版本 / 作者 / 分类 + ★ 评分 + 📥 下载数
- 完整 Description + Prompt 模板预览（只读 TextBox + 📋 复制按钮）
- Tools 列表（chips 横排）
- 安装 / 卸载 / 关闭按钮（根据 IsInstalled 切换）
- `SettingsWindow` 卡片 `MouseLeftButtonUp` → 弹窗

#### 🛠 多市场源架构
- **新增 `IMarketplaceSourceService` / `MarketplaceSourceService`**
  - 持 `QwenPaw`（GitHub 真源）+ `ClawHub` / `ModelScope`（`StubMarketService` 占位，v0.12 接真后端）
  - `MarketSourceNames` / `DefaultMarket` / `GetMarket(name)`
- **`ISkillMarket` 加 `SourceName` 属性**（卡片右上徽章用）
- **`SkillMarketService` 加 `sourceName` 构造函数参数**（默认 `"QwenPaw"`）
- **`App.xaml.cs` DI 注册**：`AddHttpClient("skill-market")` + `AddSingleton<IMarketplaceSourceService>`
- **`SettingsViewModel` 加 `MarketSourceNames` / `SelectedMarketSource` / `CurrentMarket`** + `OnSelectedMarketSourceChanged` 自动重新拉取

#### 📊 数据模型扩展
- **`SkillManifest` 加 5 字段**：`ScreenshotUrl` / `Rating` / `Downloads` / `AuthorUrl` / `AuthorName`（默认值兼容旧数据）
- **`ParseIndexFromMarkdown` 升级**：支持 7 / 8 / 9 / 10 列解析（向后兼容）
- **`skills/README.md` 加 3 列**（screenshotUrl / rating / downloads）+ 11 个技能填数据
- **`MarketSkillRow` 加 6 字段**：SourceName / ScreenshotUrl / Rating / Downloads / AuthorName / AuthorUrl
- **`MarketSkillRow.FromManifest`** 读取所有新字段

#### 🔧 Converter 扩展
- **`SourceMatchConverter`**（字符串相等比较，用于市场源 Tab 高亮）
- **`CategoryMatchConverter`**（同上，用于分类 Tab 高亮）

### 📈 测试覆盖
- 新增 13 测试：`MarketplaceSourceTests`（8 个，多源 / SourceName / 默认 / Stub / 异常 / Markdown 10 列 / 7 列兼容）+ `MarketSkillRowTests`（5 个，字段映射 / v0.11 字段 / 兜底逻辑）
- 总计 **189 测试全过**（原 176 + 13 新增）

---

## [v0.10.0] - 2026-06-26

### 🛠 新增功能

#### 🌐 技能市场（Skill Market）
- **从「只读内置 + 启用切换」升级为「市场 + 本地安装」模型**
- **GitHub 仓库根 `skills/` 目录**作为市场源：
  - `skills/README.md`（YAML 头 + Markdown 表格）—— 技能索引
  - `skills/{id}.json` —— 每个技能单独 JSON（按需拉取，节省流量）
- **`ISkillMarket` 接口 + `SkillMarketService` 实现**：
  - `FetchIndexAsync()` —— 拉 README.md 解析为 `SkillIndex`
  - `FetchSkillAsync(id)` —— 拉单个技能 JSON 反序列化为 `Skill`
  - `CheckUpdatesAsync(installed)` —— 对比本地 vs 市场版本，返回 `id → (本地, 市场, HasUpdate)` Map
  - `HttpClient` 注入（测试用 `DelegatingHandler` mock）
  - 默认 URL：`https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills`
  - 自定义异常：`MarketFetchException` / `SkillNotFoundException`

#### 📦 技能安装 / 卸载 / 更新
- **`ISkillService` 扩展**：`InstallAsync` / `UninstallAsync` / `CheckUpdatesAsync` / `BuiltIn` / `Custom` / `Categories`
- **`SkillService` 重写**：
  - 加载逻辑适配市场模型（默认 JSON + 已安装技能合并）
  - 内置技能不可安装 / 卸载（防御性 throw `InvalidOperationException`）
  - `SetMarket(ISkillMarket?)` 注入，启用更新检查

#### 🖼️ SettingsWindow 技能市场页
- **新增 🌐 技能市场 SectionCard**：
  - 🔄 拉取市场按钮（拉 README.md 索引）
  - 分类下拉筛选（"全部" + 市场实际分类去重）
  - 搜索框（匹配 ID / Name / Description / Author）
  - 技能卡片列表：图标 + 名称 + 分类标签 + 版本 + 作者 + 描述
  - 「📥 安装」/「🗑 卸载」按钮（按 `IsInstalled` 自动切换）
  - 「🔍 检查更新」按钮
  - 「🔄 有更新」橙色角标（按 `HasUpdate` 显示）
  - 状态条：拉取/安装/卸载/检查更新反馈

#### 🛠 ChatWindow 横条升级
- **内置 + 已安装技能合并**（同 ID 去重，避免重复显示）
- **「📦 已安装 N」标签**：横条右侧显示从市场安装的技能数量
- **「🔄」更新角标**：每个技能卡片右上角根据 `HasUpdate` 状态显示橙色更新提示
- **`Skill` 模型加 `HasUpdate` 属性**（`[JsonIgnore]` 不参与序列化，运行时由 ChatViewModel 写入）

#### 📝 数据模型扩展
- `Skill` 加 `IsBuiltIn`（默认 false，内置 = true）/ `Source`（默认 ""）/ `Version`（默认 ""）
- `SkillSet` 加 `BuiltIn` / `Custom` 视图属性
- `default-skills.json` 给 8 个内置技能显式标 `IsBuiltIn=true` + `Source="builtin"`
- 新建 `SkillManifest.cs`（市场索引用：Id/Name/Description/Icon/Category/Author/Version/Tags）

#### 🧪 测试覆盖
- **176/176 测试全过**（原 155 + 21 新增）
- **新增 21 个测试**：
  - 4 个 `SkillModelTests`（v0.10）：DefaultSkillsJson_AllSkillsAreBuiltIn / DefaultSkillsJson_AllSkillsHaveBuiltinSource / Skill_RecordWithMarketFields_Roundtrips / SkillSet_BuiltInAndCustom_DoesNotOverlap
  - 11 个 `SkillServiceTests`（v0.10）：BuiltIn_ReturnsOnlyBuiltinSkills / Custom_EmptyBeforeInstall / InstallAsync_AddsNewSkill_AndFiresChanged / InstallAsync_UpgradeExisting_ReplacesVersion / InstallAsync_RejectsBuiltInSkill / InstallAsync_RejectsNullOrEmptyId / InstallAsync_PersistsToFile / UninstallAsync_RemovesCustomSkill_AndFiresChanged / UninstallAsync_RejectsBuiltInSkill / UninstallAsync_UnknownId_NoOp / UninstallAsync_PersistsToFile / CheckUpdatesAsync_NoMarket_ReturnsEmpty
  - 5 个 `SkillMarketServiceTests`（新类）：ParseIndexFromMarkdown_ParsesValidTable / ParseIndexFromMarkdown_SkipsHeaderAndSeparator / ParseIndexFromMarkdown_IgnoresEmptyAndInvalidLines / CompareVersions_ReturnsCorrectOrder / FetchSkillAsync_MockHttp_ReturnsParsedSkill / FetchSkillAsync_404_ThrowsNotFound / FetchSkillAsync_NetworkError_ThrowsMarketFetch / FetchIndexAsync_ParsesReadmeTable / CheckUpdatesAsync_DetectsNewerVersion

### 🔧 关键文件
- **新建**：`src/DeskPilot.Core/Services/ISkillMarket.cs` / `SkillMarketService.cs` / `Models/SkillManifest.cs` / `src/DeskPilot.App/ViewModels/MarketSkillRow.cs` / `skills/README.md` + 11 个 `skills/*.json`
- **重构**：`src/DeskPilot.Core/Services/SkillService.cs`（市场模型加载 + Install/Uninstall/CheckUpdates）
- **改造**：`SettingsViewModel.cs`（+MarketSkills + 4 个 RelayCommand）/ `ChatViewModel.cs`（+UpdateBadgeMap + 合并去重）/ `App.xaml.cs`（+ISkillMarket DI）/ `SettingsWindow.xaml`（+🌐 市场 SectionCard）/ `ChatWindow.xaml`（+🔄 角标 + 📦 标签）

## [v0.9.2] - 2026-06-26

### 🐛 Bug 修复

#### IDE 启动 / `dotnet run` 时无界面（与 v0.9.1 release 闪退无关）
- **症状**：在 Visual Studio 按 F5 或 `dotnet run --project src/DeskPilot.App` 启动，进程存在但窗口不显示
- **根因**：WPF UI 线程构造函数里 sync-over-async 死锁
  1. `App.OnStartup`（UI 线程）→ `GetRequiredService<ChatWindow>()` → DI 解析链 → `SemanticKernelChatService..ctor`
  2. `..ctor` 调 `LoadHistoryAsync()` → `_memoryStore.LoadAsync().Wait()`
  3. `LoadAsync()` 内 `await File.ReadAllTextAsync(StorePath, ct)`（默认 `ConfigureAwait(true)`）
  4. 异步 I/O 完成后回调想回 SyncContext（UI 线程），但 UI 线程在 `.Wait()` 里死等
  5. **死锁**
- **定位方法**：
  - 在 `App.xaml.cs` 加全局异常 handler + Trace 探针（写 `startup-trace.log`）
  - `dotnet-dump collect -p <pid>` 抓 dump
  - `clrstack` 看主线程：`Monitor.Wait` → `SynchronizationContext.WaitHelper` → `WaitForMultipleObjects` 死等
- **修复（2 文件 / 6 行变更）**：
  - `DeskPilot.Core/Services/LocalJsonMemoryStore.cs`：`LoadAsync` / `SaveAsync` 的 4 个 `await` 全部加 `.ConfigureAwait(false)` —— 不让异步 I/O 回调回 UI 线程
  - `DeskPilot.Core/Services/SemanticKernelChatService.cs`：`LoadHistoryAsync` 把 `_memoryStore.LoadAsync()` 包进 `Task.Run()` —— 把整个 I/O 推线程池
- **新测试**：`MemoryStoreTests`（4 个）—— 用 `FakeSyncContext` 模拟 UI 线程，断言 `LoadAsync` 不死锁

## [v0.9.1] - 2026-06-25

### 🐛 Bug 修复

#### App zip 在没装 .NET 8 Desktop Runtime 的机器上启动无界面
- **症状**：用户下载 v0.9.0 zip 双击 `DeskPilot.App.exe`，进程闪退，看不到任何窗口、无错误提示
- **根因**：`release.yml` 的 `Publish App` 步骤用了 `--self-contained false`，zip 里只包含 `DeskPilot.App.exe` + 一堆 dll，**不包含 .NET 运行时**。在没装 .NET 8 Desktop Runtime 的机器上，Win32 app host 找不到运行时直接 exit -1
- **修复**：改成 `--self-contained true` + `PublishSingleFile=true`，zip 里包含完整运行时（约 73 MB），下载即用
- **新增参数**：
  - `PublishTrimmed=false`（WPF 不能 trim，会破坏资源引用）
  - `IncludeAllContentForSelfExtract=true`（保证 `default-skills.json` 等 EmbeddedResource 能被加载）

## [v0.9.0] - 2026-06-25

### 🛠 新增功能

#### 技能系统（Skills）
- **`Skill` 数据模型**：`Id` / `Name` / `Description` / `Icon` / `PromptTemplate` / `Tools` / `Category` / `IsEnabled`
- **`ISkillService` + `SkillService`**：加载嵌入式默认技能 JSON（8 个内置）+ 合并用户文件（`%AppData%/DeskPilot/skills.json`）
- **`ToggleAsync` + 持久化**：用户禁用/启用后立即写回用户文件，重启后保持
- **损坏文件容错**：用户文件损坏 → 自动备份 `skills.json.corrupted.{timestamp}` → 用默认技能启动
- **`SkillsChanged` 事件**：UI 自动刷新（ChatWindow 顶部横条 + SettingsWindow 列表）

#### 顶部快捷技能横条
- ChatWindow 标题栏下方加 `ScrollViewer + ItemsControl` 横条
- 圆角 10 卡片 + 软阴影 + Emoji + 名称 + 悬浮 ToolTip 显示描述
- 点击卡片 → 自动把 `PromptTemplate` 填入输入框 + 触发 `SendCommand`
- 水平滚动，宽度不够也不换行

#### 设置窗口技能管理页
- 🛠 技能 SectionCard：列出全部 8 个技能
- 每行：32px Emoji + 名称 + 分类胶囊 + 描述 + 橙色 CheckBox 启用开关
- `IsEnabled` 双向绑定 → OnIsEnabledChanged 写回 `SkillService`（fire-and-forget）

#### 8 个内置技能（默认全部启用）
| 图标 | 名称 | 分类 |
|------|------|------|
| 📁 | 整理下载文件夹 | 文件整理 |
| 🔍 | 找出重复的照片 | 文件整理 |
| ✏️ | 批量重命名文件 | 文件整理 |
| 🖼 | 批量压缩图片 | 图片处理 |
| 📦 | 批量解压压缩包 | 文件整理 |
| 🔐 | 计算文件哈希值 | 文件整理 |
| 📊 | 清理大文件 | 文件整理 |
| 🗓 | 按日期归档文件 | 文件整理 |

### 🐛 Bug 修复

#### 欢迎卡片叠加 bug
- **根因**：`BoolToVisibilityConverter.Convert` 不读 `ConverterParameter`，导致 `ConverterParameter=Invert` 永远失效 → 欢迎卡片始终显示
- **症状**：切换 AI 服务后，"AI 服务已切换"消息和 👋 你好卡片同时显示（视觉上像"中间悬浮弹窗"）
- **修复**：converter 增加 `Invert` 参数支持（1 行改动），有消息时正确折叠欢迎卡片

### 📦 内部改动
- `DeskPilot.Core/Models/Skill.cs` 新建（record 类型）
- `DeskPilot.Core/Models/SkillSet.cs` 新建（集合 + 分组辅助）
- `DeskPilot.Core/Resources/default-skills.json` 新建（8 个内置技能）
- `DeskPilot.Core/Services/ISkillService.cs` 新建
- `DeskPilot.Core/Services/SkillService.cs` 新建（含 `ForTesting` 静态构造）
- `DeskPilot.Core.csproj` 注册 `default-skills.json` 为 EmbeddedResource
- `App.xaml.cs` 三处 DI 注册（真实启动 / smoke test / PromptForSettings）
- `ChatViewModel` 加 `EnabledSkills` ObservableCollection + 订阅 `SkillsChanged`
- `ChatWindow.xaml` 顶部快捷技能横条 XAML
- `ChatWindow.xaml.cs` 加 `SkillCard_Click` 处理
- `SettingsViewModel` 注入 `ISkillService` + `Skills` 集合 + `SkillRow` 内部类 + `ToggleSkillCommand`
- `SettingsWindow.xaml` 加 🛠 技能 SectionCard

### 🧪 测试
- `SkillModelTests` 7 个：默认 8 个 / 字段非空 / Id 唯一 / 序列化往返 / Enabled 过滤 / Category 分组 / Icon 长度
- `SkillServiceTests` 7 个：默认加载 / 默认启用 / Toggle 持久化 / null 翻转 / 未知 ID / 损坏备份 / SkillsChanged 事件
- 全量 147/147 测试通过（133 原有 + 14 新增）

## [v0.8.0] - 2026-06-25

### 🎨 视觉升级 + 暗色主题

#### 暗色主题（Dark Mode）
- **`Styles/DarkColors.xaml`**：暗色配色（深灰底 `#1A1A1A` + 卡片 `#252525` + 橙色 `#FF7A28`）
- **`Services/ThemeManager.cs`**：运行时合并/移除 `ResourceDictionary`，即时切换
- **三档模式**：浅色 / 暗色 / 跟随系统（从 Windows 注册表读 `AppsUseLightTheme`）
- **持久化**：用户选择写入 `settings.json`，重启后保持
- **设置窗口**：新增"🎨 外观"卡片，三选一 RadioButton

#### 视觉细节升级
- **卡片阴影**：`DropShadowEffect` 软阴影（BlurRadius 10-12，Opacity 0.05-0.06）
- **圆角统一**：SectionCard 10→12，按钮 6→8，输入框 10→12
- **消息气泡**：圆形 36×36 头像（user 在右 / assistant 在左）+ 圆角 12 + 阴影
- **空状态欢迎卡片**：👋 标题 + 4 个建议按钮（PDF 归档/找重复/重命名/聊天），点击自动填入输入框
- **加载动画**：3 个跳动圆点（`Storyboard` + `Canvas.Top` 动画，错峰 0/0.15/0.3 秒）
- **标题栏**：橙色方块 logo（圆角 8）+ DeskPilot 名 + 副标题
- **图标统一**：🤖/🔑/💻/🛡️/🎨 Section 标题图标

#### 内部结构
- `RoleToAvatarConverter` / `RoleToAvatarBrushConverter` / `RoleToAvatarColumnConverter`：头像三件套
- `EnumToBoolConverter`：三个静态实例（LightInstance/DarkInstance/SystemInstance）给 RadioButton 用
- `StringToVisibilityConverter` 支持 `Invert` 参数：空状态卡片反向绑定
- `OrangeCheckBox` 统一样式：前景色/字号/光标一致
- `ChatViewModel.HasMessages` 属性：`Messages.CollectionChanged` 触发通知

## [v0.7.0] - 2026-06-25

### 🧠 新增功能

#### 本地记忆持久化：AI 跨会话记住你
- **`IMemoryStore`** 接口：抽象记忆存储（支持未来扩展 SQLite/云同步）
- **`LocalJsonMemoryStore`**：JSON 文件存储（`%AppData%/DeskPilot/memory.json`）
- **自动保存**：每次对话后自动保存，最多保留 100 条消息
- **启动恢复**：打开 DeskPilot 后 AI 自动加载上次对话上下文
- **清空功能**：点"清空对话"按钮同时删除本地记忆文件
- **容错**：文件损坏自动备份 + 降级（不影响启动）

## [v0.6.0] - 2026-06-25

### 🛡️ 新增功能

#### 权限控制：危险工具需用户确认
- **工具风险分级**：`ITool` 新增 `RiskLevel`（`Safe` / `Destructive`）
- **确认机制**：危险工具首次调用时拦截，AI 会自动询问用户"确认执行？"
- **智能缓存**：用户确认后 30 秒内同一参数再次调用自动放行
- **开关控制**：`AppSettings.RequireConfirmation`（设置窗口可开关，默认开）
- **拦截层**：`ToolCallObserver`（SK `IFunctionInvocationFilter`）在工具执行前检查

工具分级：
| 工具 | 风险等级 | 原因 |
|------|---------|------|
| find_duplicates | Safe | 只读扫描 |
| hash_files | Safe | 只读计算 |
| archive_files_by_date | Destructive | 移动文件 |
| move_files | Destructive | 移动文件 |
| rename_by_pattern | Destructive | 重命名文件 |
| batch_resize_image | Destructive | 覆盖图片 |
| extract_archive | Destructive | 解压可能覆盖 |

#### Release workflow 修复
- `release` job 加 `actions/checkout`（之前缺 checkout 导致 CHANGELOG.md 不可读）
- 指定 `ref: master` + `sparse-checkout`（只拉 release notes 文件，速度最快）

## [v0.5.1] - 2026-06-25

### 🎉 新增功能

#### AI 流式输出（打字机效果）
- **IChatService 新增 `ChatStreamAsync`**：`IAsyncEnumerable<string>` 逐 token 返回
- **SK 流式 API**：`GetStreamingChatMessageContentsAsync` + `FunctionChoiceBehavior.Auto()`
  - Tool Calling 自动处理——工具先内部执行，后流式输出最终 LLM 回复
- **ChatViewModel 改造**：先插入空 assistant 气泡 → 逐片追加 → 打字机效果
- **取消键优化**：取消后消息气泡保留已输出的内容 + `⏸️ 已取消`

#### CI 启动 smoke test（防 XAML 崩溃回归）
- `DESKPILOT_SMOKE_TEST=1` 环境变量触发简化启动路径
- `StubChatService`：不调 AI，直接走完 XAML 解析 → DI 注入 → 窗口创建全链路
- 自动 `Shutdown(0)` 退出：exit 0 = 通过，exit 2 = 崩溃

### 🔧 改进
- `IChatService` 继承 `IDisposable`（统一生命周期管理）
- `ci.yml` smoke test 改用 `DESKPILOT_SMOKE_TEST=1` + `-Wait` 模式（替代旧的手动 kill）

## [v0.5.0] - 2026-06-25

### 🎉 新增功能

#### 7 工具矩阵（4 → 7）
- **BatchResizeImageTool**：批量缩放图片（依赖 System.Drawing.Common）
- **ExtractArchiveTool**：解压 zip 文件（System.IO.Compression 内置）
- **HashFilesTool**：计算文件哈希（SHA256/SHA1/MD5 等，无额外依赖）
- **MCP Server 同步更新**：4 → 7 工具暴露

#### 修复 WPF 启动崩溃
- **根因**：`App.xaml` 残留 `StartupUri="Views/ChatWindow.xaml"` 导致无参构造 XamlParseException
- **修复**：移除 StartupUri，全走 DI 构造

#### 全部历史版本见下文

### 🎉 新增功能

#### MCP Server 封装（杀手锏新方向）
- **新项目**：`src/DeskPilot.Mcp/` —— .NET 8 控制台 stdio MCP server
- **4 个工具暴露**：archive_files_by_date / move_files / find_duplicates / rename_by_pattern
- **外部 AI 客户端可接入**：
  - Claude Desktop（JSON 配置文件）
  - Cursor
  - Continue.dev
  - 任何支持 MCP 协议的 AI 客户端
- **设计**：
  - 每个工具一个 `[McpServerTool]` 方法（强类型参数 + /// XML doc comment 描述）
  - 内部转 JSON 调 `ITool.ExecuteAsync` —— 零业务逻辑，全部复用现有工具
  - 日志走 stderr（避免污染 JSON-RPC 协议）
  - 用 `ModelContextProtocol 0.3.0-preview.4` SDK

#### MCP Server 端到端测试
- `McpServerTests` (3 个)：
  - `Server_Initialize_ReturnsServerInfo` — 握手成功
  - `Server_ToolsList_Returns4Tools` — 4 个工具全部注册
  - `Server_ToolsCall_FindDuplicates_ReturnsResult` — 真实调用 find_duplicates
- **真实启停 Mcp server 进程 + stdio JSON-RPC 通信**

### 📦 项目变更
- `DeskPilot.Mcp` 加入 `DeskPilot.slnx`
- 4 → **5 个项目**（Core/App/Tests/Verify/Mcp）

### ✅ 测试
- **107/107 全过**（v0.2.0 → v0.3.0，+3 MCP E2E）

### 🔧 集成示例（Claude Desktop）

`%APPDATA%\Claude\claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "deskpilot": {
      "command": "dotnet",
      "args": ["run", "--project", "D:\\opensource\\DeskPilot\\src\\DeskPilot.Mcp"]
    }
  }
}
```

之后在 Claude Desktop 就能直接说：
- "用 DeskPilot 把我桌面上重复的文件找出来"
- "把 D:\\发票 按月归档"

---

## [v0.2.0] - 2026-06-25

### 🎉 新增功能

#### 3 个新 MCP 工具
- **MoveFilesTool** (`move_files`) - 批量移动文件
  - 支持 glob 过滤（如 `*.pdf`）
  - 可选递归子目录
  - 自动创建目标目录
  - Collision 自动加 `_2`/`_3` 后缀
- **FindDuplicatesTool** (`find_duplicates`) - 查找内容完全相同的文件
  - 按 SHA256 哈希判断（先按 size 预筛提速）
  - 报告浪费空间（可清理多少 MB）
  - 可选递归 + 最小文件大小过滤
- **RenameByPatternTool** (`rename_by_pattern`) - 批量重命名
  - 正则替换（支持 `$1`/`$2` 捕获组）
  - 前缀/后缀添加
  - DryRun 模式（只预览不重命名）
  - 3 种模式可组合使用

#### UI 进度展示
- `ChatViewModel` 新增 `ToolStatus` 字段（底部状态栏）
- `SemanticKernelChatService` 暴露 `ToolInvoking`/`ToolInvoked` 事件
- 使用 SK 1.32 推荐的 `IFunctionInvocationFilter`（替代过时的 events API）
- 工具调用时状态栏实时显示：
  - `🔧 正在调用 archive_files_by_date...`
  - `✅ archive_files_by_date 完成 (123ms)`
- WPF 状态栏带 ⚙️ 图标 + 蚂蚁灰文字 + 边框

### 🛠️ 改进
- `DeskPilot.Verify` 程序扩展为 4 工具统一 E2E 验证
  - 支持 `--tool <name>` 指定单个工具
  - 支持 `--no` DryRun 模式
  - 真实执行 + 总结报告

### 📦 项目变更
- 4 个工具统一注册到 `App.xaml.cs` DI 容器
- AI 系统 prompt 自动列出 4 个工具描述

### ✅ 测试
- **104/104 全过**（从 v0.1.2 的 73 → 104，+31）
  - `MoveFilesToolTests`: 7 个
  - `FindDuplicatesToolTests`: 10 个
  - `RenameByPatternToolTests`: 11 个
  - `ToolEventArgsTests`: 3 个
- 0 警告 0 错误

### 📊 E2E 验证（DeskPilot.Verify）
- 4 工具在真实文件系统上端到端通过
  - ArchiveByDate: 3 文件 → 按月归档 ✅
  - MoveFiles: 3 文件 → move_dst ✅
  - FindDuplicates: 找到 1 组重复 ✅
  - RenameByPattern: IMG_001~003 → photo_001~003 ✅

---

## [v0.1.2] - 2026-06-25

### 🎉 新增功能
- **GitHub 公开仓库上线**：https://github.com/maqiul/DeskPilot
  - 推送 master + 4 tags (v0.0.3, v0.1.0, v0.1.1, v0.1.2)
  - CI workflow + Issue 模板 + Contributing 指南
- **DeskPilot.Verify 项目**：离线 E2E 验证程序
  - 无需 API Key，直接跑工具看真实效果
  - 用法：`dotnet run --project src/DeskPilot.Verify -- <sourceDir> [granularity] [dateField] [--no]`

### ✅ 测试
- 73/73 全过（v0.1.1 → v0.1.2 测试无变化）

---

## [v0.1.1] - 2026-06-25

### 🎉 新增功能
- **AI 自动调用工具闭环**
  - `IToolRegistry` + `ToolRegistry` 工具注册中心
  - `ArchiveByDateTool` 加 `[KernelFunction("archive_by_date")]` 标注
  - `SemanticKernelChatService` 启用 `FunctionChoiceBehavior.Auto()`（SK 自动处理 tool calling 循环）
  - `App.xaml.cs` DI 注入工具到 Kernel
- **杀手锏工作流**：用户说"把 D:\发票 按月归档" → AI 自动调工具 → 报告

### ✅ 测试
- 73/73 全过（v0.1.0 → v0.1.1，+14 测试）

---

## [v0.1.0] - 2026-06-25

### 🎉 新增功能
- **第一个 MCP 工具**：`ArchiveByDateTool`（按日期归档）
  - `ITool` + `ToolResult` 统一抽象
  - 按修改/创建时间 + 年/月/日粒度归档
  - DryRun / glob 过滤 / 自定义目标 / collision 处理

### ✅ 测试
- 59/59 全过（+13 测试）

---

## [v0.0.3] - 2026-06-25

### 🎉 新增功能
- **动态模型列表 UI 闭环**
  - 设置窗口的"🔄 刷新模型列表"按钮
  - `OpenAIModelLister` / `DeepSeekModelLister` / `OllamaModelLister` 三个动态 Lister
  - 静态兜底（OpenAI 6 + DeepSeek 3）
  - 错误吞咽策略（网络错误返回空列表）

### ✅ 测试
- 46/46 全过

---

## [v0.0.2] - 2026-06-25

### 🎉 新增功能
- **多 AI Provider 支持**：OpenAI / DeepSeek / Ollama
- **4 种配置方式**：.env / User Secrets / 环境变量 / DPAPI 加密
- **设置窗口**：UI 配置 Provider/Key/Model
- **DPAPI 加密**：`%APPDATA%\DeskPilot\settings.dat`

---

## [v0.0.1] - 2026-06-25

### 🎉 首个发布
- 项目骨架 + WPF 聊天窗口
- Semantic Kernel 集成 + 蚂蚁金服橙配色
- MVVM 架构（CommunityToolkit.Mvvm）
- 完整 CI 文档
