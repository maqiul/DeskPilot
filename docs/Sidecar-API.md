# DeskPilot Sidecar HTTP API 参考

> **版本**：v0.1.5（对应 `feature/v2-tauri` 分支）  
> **服务**：`DeskPilot.Server` (.NET 8 Minimal API)  
> **默认端口**：`http://localhost:5180`（通过 CLI `--urls=http://localhost:xxxx` 可覆盖）  
> **来源**：DeskPilot Tauri sidecar

---

## 📋 目录

1. [健康检查](#1-健康检查)
2. [聊天流式输出](#2-聊天流式输出)
3. [聊天 SSE 流式输出](#3-聊天-sse-流式输出)
4. [工具注册清单](#4-工具注册清单)
5. [工具执行](#5-工具执行)
6. [工具调用历史](#6-工具调用历史)
7. [共享类型](#7-共享类型)
8. [错误约定](#8-错误约定)

---

## 1. 健康检查

端点根路径，返回服务基本信息。

| 字段 | 值 |
|---|---|
| **方法** | `GET` |
| **路径** | `/` |
| **鉴权** | 无 |

### 请求

无参数、无 body。

### 响应（200）

```json
{
  "service": "DeskPilot.Server",
  "version": "v0.1.5",
  "status": "running"
}
```

| 字段 | 类型 | 含义 |
|---|---|---|
| `service` | string | 服务名（固定） |
| `version` | string | 当前版本号 |
| `status` | string | 固定 `running` |

### 用途

Tauri setup() 启动后每 5 秒（v0.0.6）调用，验证 sidecar 起来后接入聊天。

---

## 2. 聊天流式输出

非流式版本。`/api/chat/stream`（v0.0.4 之后）才是 SSE 流式，本端点保留以便老客户端兼容。

| 字段 | 值 |
|---|---|
| **方法** | `GET` |
| **路径** | `/api/chat` |
| **鉴权** | 无 |

### 查询参数

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `prompt` | string | 是 | 用户输入文本 |

### 响应（200）

```json
{
  "reply": "完整回复",
  "success": true,
  "version": "v0.1.5"
}
```

| 字段 | 类型 | 含义 |
|---|---|---|
| `reply` | string | 完整 LLM 回复（拼接 ChatStreamAsync chunks） |
| `success` | bool | true 成功 / false 失败 |
| `version` | string | 服务版本号 |

### 示例

```bash
curl "http://localhost:5180/api/chat?prompt=你好"
```

---

## 3. 聊天 SSE 流式输出

推荐使用。`Server-Sent Events` 协议，逐 token 推送到客户端。

| 字段 | 值 |
|---|---|
| **方法** | `GET` |
| **路径** | `/api/chat/stream` |
| **Content-Type** | `text/event-stream` |
| **鉴权** | 无 |

### 查询参数

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `prompt` | string | 是 | 用户输入文本 |

### 响应（200）

每个 SSE 事件格式：

```
data: {"chunk":"你"}
data: {"chunk":"好"}
...
data: {"chunk":"！"}
data: [DONE]
```

| chunk 字段 | 类型 | 含义 |
|---|---|---|
| `chunk` | string | 单个 token 增量文本 |
| `[DONE]` | 哨兵 | 流结束 |

### 示例（前端）

```javascript
const response = await fetch(`${SIDE_STREAM}?prompt=${encodeURIComponent(prompt)}`);
const reader = response.body.getReader();
const decoder = new TextDecoder();
let buffer = "";
while (true) {
  const { done, value } = await reader.read();
  if (done) break;
  buffer += decoder.decode(value, { stream: true });
  // 解析 SSE 事件边界 "\n\n" + 每行 "data:" 前缀
}
```

### cURL

```bash
curl -N "http://localhost:5180/api/chat/stream?prompt=讲个笑话"
```

---

## 4. 工具注册清单

列出当前已注册的所有 Tool（`IToolRegistry` 扫描结果）。

| 字段 | 值 |
|---|---|
| **方法** | `GET` |
| **路径** | `/api/tools/list` |
| **鉴权** | 无 |

### 响应（200）

```json
{
  "count": 15,
  "tools": [
    {
      "name": "hash_files",
      "description": "计算文件哈希值（SHA256/MD5/SHA1）",
      "kernelFunctionCount": 1,
      "risk": "Safe"
    },
    {
      "name": "move_files",
      "description": "移动文件",
      "kernelFunctionCount": 1,
      "risk": "Destructive"
    }
  ]
}
```

| 字段 | 类型 | 含义 |
|---|---|---|
| `count` | int | 工具总数 |
| `tools[].name` | string | 工具名（唯一） |
| `tools[].description` | string | 工具描述 |
| `tools[].kernelFunctionCount` | int | 内部 KernelFunction 数量（v0.0.2 接 SK 后） |
| `tools[].risk` | string | **v0.1.0** 风险等级 `Safe` 或 `Destructive` |

### 工具分类（v0.1.0 实测 15 个）

**Safe（6 个）**：`hash_files` / `text_stats` / `search_content` / `find_duplicates` / `extract_archive`（注：开发标 Destructive）/ ...

**Destructive（9 个）**：`archive_files_by_date` / `batch_excel` / `batch_resize_image` / `convert_image` / `crop_image` / `merge_pdfs` / `move_files` / `rename_by_exif` / `rename_by_pattern` / `rotate_image`（注：`extract_archive` 也归此类）

> 💡 `extract_archive` 类内定义 `RiskLevel.Destructive`（默认 `move` 行为，开发规范），前端按 `risk === "Destructive"` 弹二次确认。

---

## 5. 工具执行

执行一个具体的 Tool，传入 JSON 参数。

| 字段 | 值 |
|---|---|
| **方法** | `POST` |
| **路径** | `/api/tools/execute?name=<toolName>` |
| **Content-Type** | `application/json` |
| **鉴权** | 无 |

### 查询参数

| 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `name` | string | 是 | 工具名（从 `/api/tools/list` 取） |

### Body

Tool 的入参 JSON object（不同 Tool 不同字段）：

**示例 1: hash_files**
```json
{
  "directory": "D:\\opensource\\DeskPilot",
  "algorithm": "sha256",
  "pattern": "*.md"
}
```

**示例 2: text_stats**
```json
{
  "filePath": "D:\\opensource\\DeskPilot\\README.md"
}
```

**body 为空 = `{}` 走 Tool 默认值**。

### 响应（200）

成功：
```json
{
  "success": true,
  "summary": "✅ SHA256 计算完成：9 成功，0 失败（扫描 9）",
  "data": { "results": [...] },
  "error": null
}
```

失败（Tool 不存在）：
```json
{
  "success": false,
  "error": "Tool 'foo_bar' 不存在。可用工具：hash_files, text_stats, ..."
}
```

失败（执行抛异常）：
```json
{
  "success": false,
  "error": "异常消息"
}
```

| 字段 | 类型 | 含义 |
|---|---|---|
| `success` | bool | true 成功 / false 失败 |
| `summary` | string | 人读总结（成功时） |
| `data` | object / array / null | Tool 业务数据 |
| `error` | string / null | 错误消息（失败时） |

### ⚠️ v0.1.1 重要变更

**Tool 调用历史永久记录**（无论成功失败，包括 Tool 不存在分支）：  
所有调用 → `ToolHistoryStore.Add(...)` → `/api/tools/history` 可查。

### 示例

```bash
# 使用 curl
curl -X POST "http://localhost:5180/api/tools/execute?name=text_stats" \
  -H "Content-Type: application/json" \
  --data-raw '{"filePath":"D:\\opensource\\DeskPilot\\README.md"}'
```

```javascript
// 前端
const resp = await fetch(`${SIDE_BASE}/api/tools/execute?name=${encodeURIComponent(name)}`, {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(args)
});
```

---

## 6. 工具调用历史

查询最近 N 条 Tool 调用记录（持久化到 `%LOCALAPPDATA%\DeskPilot\tool-history.json`）。

| 字段 | 值 |
|---|---|
| **方法** | `GET` |
| **路径** | `/api/tools/history` |
| **鉴权** | 无 |

### 查询参数

| 参数 | 类型 | 必填 | 默认 | 说明 |
|---|---|---|---|---|
| `limit` | int | 否 | 50 | 返回条数（1-100）|
| `before` | string (ISO 8601) | 否 | null | **v0.1.4** cursor，返回早于该时间的记录 |

### 响应（200）

```json
{
  "count": 2,
  "entries": [
    {
      "timestamp": "2026-07-03T09:32:37.8730749Z",
      "toolName": "hash_files",
      "argsJson": "{\"directory\":\"...\",\"algorithm\":\"sha256\"}",
      "success": true,
      "summary": "✅ SHA256 计算完成：9 成功，0 失败（扫描 9）",
      "errorMessage": null
    }
  ]
}
```

| 字段 | 类型 | 含义 |
|---|---|---|
| `count` | int | 本次返回的条数 |
| `entries[].timestamp` | string (UTC) | 调用时间（ISO 8601）|
| `entries[].toolName` | string | 工具名 |
| `entries[].argsJson` | string | 原 args JSON 字符串 |
| `entries[].success` | bool | true 成功 / false 失败 |
| `entries[].summary` | string | 人读总结（成功）|
| `entries[].errorMessage` | string / null | 错误消息（失败）|

### 示例

**首次查询（最新 50 条）**
```bash
curl "http://localhost:5180/api/tools/history"
```

**分页（取早于某时间）**
```bash
curl "http://localhost:5180/api/tools/history?limit=20&before=2026-07-03T09:33:00Z"
```

**前端 loadMoreHistory**：
```javascript
async function loadMoreHistory() {
  const earliestTs = history.value[history.value.length - 1].timestamp;
  const resp = await fetch(`${SIDE_HISTORY}?limit=50&before=${encodeURIComponent(earliestTs)}`);
  const data = await resp.json();
  history.value = [...history.value, ...(data.entries ?? [])];
}
```

### 持久化

- 文件位置：`%LOCALAPPDATA%\DeskPilot\tool-history.json`
- 容量：最近 100 条（环形队列）
- 格式：JSON
- 服务重启后从磁盘加载

---

## 7. 共享类型

### ToolHistoryEntry（后端持久化结构）

```csharp
public sealed class ToolHistoryEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string ToolName { get; init; } = string.Empty;
    public string ArgsJson { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}
```

### ToolDescriptor（前端展示）

```typescript
interface ToolDescriptor {
  name: string;
  description: string;
  kernelFunctionCount: number;
  risk: string; // "Safe" | "Destructive"
}
```

---

## 8. 错误约定

所有端点都返回 `200 OK`，错误信息用 **JSON body** 表达：

| 场景 | HTTP | body success | body error |
|---|---|---|---|
| 成功 | 200 | true | null |
| Tool 不存在 | 200 | false | "Tool 'X' 不存在。可用工具：..." |
| Tool 执行异常 | 200 | false | 异常消息 |
| 参数错误（缺参） | 400 | - | ASP.NET Core 默认错误 |
| 服务挂掉 | 500 | - | ASP.NET Core 默认错误 |

**历史 200** 失败响应：客户端代码走 `if (!data.success) { toolError.value = data.error }` 模式。

---

## 📝 变更记录

| 版本 | 日期 | 变更 |
|---|---|---|
| v0.0.2 | 2026-07 | 新增基础端点：`/` `/api/chat` |
| v0.0.4 | 2026-07 | 新增 `/api/chat/stream`（SSE）|
| v0.0.5 | 2026-07 | Frontend `fetch + ReadableStream` 消费 SSE |
| v0.0.6 | 2026-07 | 健康检查用于 Tauri setup() 重试机制 |
| v0.0.7 | 2026-07 | 新增 `/api/tools/list` `/api/tools/execute`（5 Safe Tool）|
| v0.0.8 | 2026-07 | 扩到 15 个全量 Core Tool |
| v0.0.9 | 2026-07 | Frontend 工具面板 + Destructive 二次确认 |
| v0.1.0 | 2026-07-02 | `ToolDescriptor.risk` 字段（去前端硬编码）|
| v0.1.1 | 2026-07-02 | `/api/tools/history` + ToolHistoryStore JSON 持久化 |
| v0.1.4 | 2026-07-02 | `/api/tools/history` `before` 参数（cursor 分页）|
| v0.1.5 | 2026-07-02 | 多格式导出（前端 `/api/export` 不依赖，纯 Blob）|

---

## 🛠 本地开发

### 启动 sidecar

```bash
# 默认端口 5180
cd tauri-app/src-tauri/binaries
./deskpilot-server-x86_64-pc-windows-msvc.exe

# 自定义端口
./deskpilot-server-x86_64-pc-windows-msvc.exe --urls=http://localhost:5181
```

### 重新编译

```bash
# Sidecar (self-contained)
"C:\Users\maqiu\dotnet-sdk-8-latest\dotnet.exe" publish src\DeskPilot.Server\DeskPilot.Server.csproj \
  -c Release -r win-x64 --self-contained true \
  -o tauri-app/src-tauri/binaries
```

### Tauri 端

sidecar 二进制命名规范必须保留 `<name>-<triple>.exe` 形式（如 `deskpilot-server-x86_64-pc-windows-msvc.exe`），Tauri 2.x `cargo build` 自动从 `binaries/` 复制到 `target/release/`。
