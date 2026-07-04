<script setup lang="ts">
import { ref, onMounted } from "vue";

interface ChatMessage {
  role: "user" | "assistant";
  content: string;
  timestamp?: number; // v0.1.12: 消息时间戳（毫秒）
}

interface ToolDescriptor {
  name: string;
  description: string;
  kernelFunctionCount: number;
  risk: string;
}

const messages = ref<ChatMessage[]>([]);
const userInput = ref("");
const isBusy = ref(false);
const tools = ref<ToolDescriptor[]>([]);
const toolArgsInput = ref(""); // 当前选中 Tool 的 JSON 参数输入框
const selectedTool = ref<string>("");
const pendingTool = ref<string>(""); // 二次确认中的 Tool 名
const toolResult = ref<any>(null);
const toolError = ref<string>("");
const isToolBusy = ref(false);

// v0.1.2: 历史面板
interface HistoryEntry {
  timestamp: string;
  toolName: string;
  argsJson: string;
  success: boolean;
  summary: string;
  errorMessage: string | null;
}
const history = ref<HistoryEntry[]>([]);
const showHistory = ref(false);
const appendResultToChat = ref(true); // v0.1.8: Tool 结果自动回填聊天
const toolSearchKeyword = ref(""); // v0.1.9: Tool 搜索过滤（同 WPF v0.24）
const showClearAllConfirm = ref(false); // v0.1.11: 清空全部二次确认
const isHistoryLoading = ref(false);
const selectedHistoryIdx = ref<number>(-1);

const SIDE_BASE = "http://localhost:5180";
const SIDE_STREAM = `${SIDE_BASE}/api/chat/stream`;
const SIDE_TOOLS_LIST = `${SIDE_BASE}/api/tools/list`;
const SIDE_HISTORY = `${SIDE_BASE}/api/tools/history`;

// v0.1.0: 服务端 /api/tools/list 返回 risk 字段，前端去掉硬编码 Set
// Destructive Tool 二次确认从每个 tool 的 risk 字段判断
function isDestructive(t: ToolDescriptor): boolean {
  return t.risk === "Destructive";
}

// 当前选中 Tool 的 risk（"Safe" / "Destructive"）
function selectedToolRisk(): string {
  const t = tools.value.find(x => x.name === selectedTool.value);
  return t?.risk ?? "Safe";
}

// 执行按钮（Destructive 弹二次确认，其他直接调）
function executeSelected() {
  const name = selectedTool.value;
  const risk = selectedToolRisk();
  if (risk === "Destructive") {
    // 二次确认流：先关参数模态，弹确认框
    pendingTool.value = name;
    selectedTool.value = "";
  } else {
    invokeTool(name);
  }
}

onMounted(async () => {
  await refreshToolList();
});

async function refreshToolList() {
  try {
    const resp = await fetch(SIDE_TOOLS_LIST);
    const data = await resp.json();
    tools.value = data.tools ?? [];
  } catch (e: any) {
    toolError.value = `加载工具列表失败：${e.message ?? e}`;
  }
}

async function sendMessage() {
  const prompt = userInput.value.trim();
  if (!prompt || isBusy.value) return;
  messages.value.push({ role: "user", content: prompt, timestamp: Date.now() });
  userInput.value = "";
  isBusy.value = true;
  const idx = messages.value.length;
  messages.value.push({ role: "assistant", content: "", timestamp: Date.now() });

  try {
    const response = await fetch(`${SIDE_STREAM}?prompt=${encodeURIComponent(prompt)}`);
    if (!response.ok || !response.body) {
      throw new Error(`HTTP ${response.status}`);
    }
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      let sepIdx;
      while ((sepIdx = buffer.indexOf("\n\n")) !== -1) {
        const rawEvent = buffer.slice(0, sepIdx);
        buffer = buffer.slice(sepIdx + 2);
        const lines = rawEvent.split("\n");
        for (const line of lines) {
          if (line.startsWith("data:")) {
            const data = line.slice(5).trim();
            if (data === "[DONE]") continue;
            try {
              const obj = JSON.parse(data);
              if (typeof obj.chunk === "string") {
                messages.value[idx] = {
                  role: "assistant",
                  content: messages.value[idx].content + obj.chunk
                };
              }
            } catch {
              // ignore parse errors
            }
          }
        }
      }
    }
  } catch (e: any) {
    messages.value[idx] = {
      role: "assistant",
      content: `❌ 错误：${e.message ?? e}`
    };
  } finally {
    isBusy.value = false;
  }
}

function startInvoke(name: string) {
  selectedTool.value = name;
  toolArgsInput.value = ""; // 重置参数
  toolResult.value = null;
  toolError.value = "";
}

function cancelInvoke() {
  selectedTool.value = "";
  toolArgsInput.value = "";
}

// v0.1.3: 导出 Tool 结果为 Markdown
// v0.1.5: 多格式导出（MD / JSON / CSV）
function exportMarkdown() { exportResult("md"); }
function exportJson() { exportResult("json"); }
function exportCsv() { exportResult("csv"); }

function exportResult(format: "md" | "json" | "csv") {
  const r = toolResult.value;
  if (!r) return;
  const name = r.toolName ?? selectedTool.value ?? "tool";
  const ts = new Date().toLocaleString("zh-CN", { hour12: false });
  const stamp = Date.now();

  let content = "";
  let mime = "text/plain;charset=utf-8";
  let ext = format;

  if (format === "md") {
    const lines: string[] = [];
    lines.push(`# DeskPilot Tool 调用报告`);
    lines.push(``);
    lines.push(`- **工具名**: \`${name}\``);
    lines.push(`- **调用时间**: ${ts}`);
    lines.push(`- **状态**: ${r.success ? "✅ 成功" : "❌ 失败"}`);
    lines.push(`- **耗时**: ${r.durationMs ?? "-"} ms`);
    lines.push(``);
    if (r.summary) {
      lines.push(`## Summary`);
      lines.push(``);
      lines.push(r.summary);
      lines.push(``);
    }
    if (r.data !== undefined && r.data !== null) {
      lines.push(`## Data`);
      lines.push(``);
      lines.push("```json");
      lines.push(JSON.stringify(r.data, null, 2));
      lines.push("```");
    }
    content = lines.join("\n");
    mime = "text/markdown;charset=utf-8";
  } else if (format === "json") {
    const payload = {
      toolName: name,
      timestamp: ts,
      success: r.success,
      durationMs: r.durationMs ?? null,
      summary: r.summary,
      data: r.data ?? null,
      errorMessage: r.errorMessage ?? null
    };
    content = JSON.stringify(payload, null, 2);
    mime = "application/json;charset=utf-8";
  } else if (format === "csv") {
    const rows: string[][] = [
      ["字段", "值"],
      ["toolName", name],
      ["timestamp", ts],
      ["success", String(r.success)],
      ["durationMs", String(r.durationMs ?? "")],
      ["summary", r.summary ?? ""],
      ["errorMessage", r.errorMessage ?? ""],
      ["dataJson", r.data !== undefined && r.data !== null ? JSON.stringify(r.data) : ""]
    ];
    content = rows
      .map(row => row.map(cell => {
        const s = String(cell);
        if (s.includes(",") || s.includes('"') || s.includes("\n")) {
          return '"' + s.replace(/"/g, '""') + '"';
        }
        return s;
      }).join(","))
      .join("\n");
    mime = "text/csv;charset=utf-8";
    ext = "csv";
  }

  const blob = new Blob([content], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `deskpilot-${name}-${stamp}.${ext}`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

async function confirmInvoke() {
  const name = pendingTool.value;
  pendingTool.value = "";
  await invokeTool(name);
}

async function directInvoke() {
  const name = selectedTool.value;
  cancelInvoke();
  await invokeTool(name);
}

async function invokeTool(name: string) {
  if (isToolBusy.value) return;
  isToolBusy.value = true;
  toolError.value = "";
  toolResult.value = null;

  let argsJson = toolArgsInput.value.trim();
  if (!argsJson) argsJson = "{}";

  try {
    const resp = await fetch(`${SIDE_BASE}/api/tools/execute?name=${encodeURIComponent(name)}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: argsJson
    });
    const data = await resp.json();
    if (data.success) {
      toolResult.value = data;
    } else {
      toolError.value = data.error ?? data.summary ?? "执行失败";
    }
  } catch (e: any) {
    toolError.value = `请求失败：${e.message ?? e}`;
  } finally {
    isToolBusy.value = false;
    cancelInvoke();
    // v0.1.2: 调用完自动刷新历史（v0.1.4: reset=true 重置）
    if (showHistory.value) {
      loadHistory(true);
    }
    // v0.1.8: 成功结果回填到聊天历史（开关由 appendResultToChat 控制）
    if (appendResultToChat.value && toolResult.value) {
      messages.value.push({
        role: "assistant",
        content: formatResultForChat(toolResult.value),
        timestamp: Date.now()
      });
    }
  }
}

// v0.1.8: 把 Tool 结果格式化为聊天内容（人读 + JSON 代码块）
function formatResultForChat(r: any): string {
  const name = r.toolName ?? "tool";
  const lines: string[] = [];
  lines.push(`🛠 **${name}** 调用结果：`);
  lines.push(``);
  if (r.summary) {
    lines.push(r.summary);
    lines.push(``);
  }
  if (r.data !== undefined && r.data !== null) {
    lines.push("```json");
    lines.push(JSON.stringify(r.data, null, 2));
    lines.push("```");
  }
  return lines.join("\n");
}

// v0.1.10: 删除单条聊天消息（呼应 v0.27 WPF 风格）
function removeMessage(idx: number) {
  if (idx < 0 || idx >= messages.value.length) return;
  messages.value.splice(idx, 1);
}

// v0.1.12: 格式化消息时间戳（HH:mm:SS），缺省显示空串
function formatChatTimestamp(ts?: number): string {
  if (!ts) return "";
  const d = new Date(ts);
  const hh = String(d.getHours()).padStart(2, "0");
  const mm = String(d.getMinutes()).padStart(2, "0");
  const ss = String(d.getSeconds()).padStart(2, "0");
  return `${hh}:${mm}:${ss}`;
}

// v0.1.11: 一键清空全部聊天（带二次确认）
function clearAllMessages() {
  messages.value = [];
  showClearAllConfirm.value = false;
}

function requestClearAll() {
  if (messages.value.length === 0) return;
  showClearAllConfirm.value = true;
}

function cancelClearAll() {
  showClearAllConfirm.value = false;
}

// v0.1.2: 加载历史
async function loadHistory(reset = true) {
  isHistoryLoading.value = true;
  try {
    const resp = await fetch(`${SIDE_HISTORY}?limit=50`);
    const data = await resp.json();
    const newEntries = data.entries ?? [];
    if (reset) {
      history.value = newEntries;
    } else {
      history.value = [...history.value, ...newEntries];
    }
  } catch (e: any) {
    toolError.value = `加载历史失败：${e.message ?? e}`;
  } finally {
    isHistoryLoading.value = false;
  }
}

// v0.1.4: 分页加载更早的历史（取最早一条的 timestamp 作为 before）
async function loadMoreHistory() {
  if (isHistoryLoading.value || history.value.length === 0) return;
  isHistoryLoading.value = true;
  try {
    const earliestTs = history.value[history.value.length - 1].timestamp;
    const resp = await fetch(`${SIDE_HISTORY}?limit=50&before=${encodeURIComponent(earliestTs)}`);
    const data = await resp.json();
    const newEntries = data.entries ?? [];
    if (newEntries.length === 0) {
      hasMoreHistory.value = false;
    } else {
      history.value = [...history.value, ...newEntries];
    }
  } catch (e: any) {
    toolError.value = `加载历史失败：${e.message ?? e}`;
  } finally {
    isHistoryLoading.value = false;
  }
}

const hasMoreHistory = ref(true);

function toggleHistory() {
  showHistory.value = !showHistory.value;
  if (showHistory.value && history.value.length === 0) {
    loadHistory(true);
  }
}

function selectHistoryEntry(idx: number) {
  selectedHistoryIdx.value = selectedHistoryIdx.value === idx ? -1 : idx;
}

function formatTimestamp(ts: string): string {
  try {
    const d = new Date(ts);
    return d.toLocaleString("zh-CN", { hour12: false });
  } catch {
    return ts;
  }
}

// v0.1.9: Tool 搜索过滤（按 name + description 子串匹配，大小写不敏感）
import { computed } from "vue";
const filteredTools = computed(() => {
  const kw = toolSearchKeyword.value.trim().toLowerCase();
  if (!kw) return tools.value;
  return tools.value.filter(t =>
    t.name.toLowerCase().includes(kw) ||
    t.description.toLowerCase().includes(kw)
  );
});
</script>

<template>
  <div class="app">
    <header>
      <h1>🛩 DeskPilot v2 (Tauri · 工具面板)</h1>
      <p>.NET 8 Sidecar · Vue 3 · Tauri 2.x · Sidecar expose 15 Core Tools</p>
    </header>
    <main>
      <!-- 左：聊天区（v0.0.5 保留） -->
      <section class="chat-pane">
        <div class="chat-header">
          <span class="msg-count">{{ messages.length }} 条消息</span>
          <!-- v0.1.11: 一键清空 -->
          <button class="clear-all" @click="requestClearAll" :disabled="messages.length === 0" title="清空全部聊天">🧹 清空</button>
        </div>
        <!-- v0.1.11: 二次确认模态框 -->
        <div v-if="showClearAllConfirm" class="confirm-overlay" @click.self="cancelClearAll">
          <div class="confirm-modal">
            <h3>清空全部聊天消息？</h3>
            <p>将删除所有 {{ messages.length }} 条消息，不可恢复。</p>
            <div class="confirm-actions">
              <button class="cancel" @click="cancelClearAll">取消</button>
              <button class="confirm" @click="clearAllMessages">确认清空</button>
            </div>
          </div>
        </div>
        <div class="messages">
          <div v-for="(m, i) in messages" :key="i" :class="['msg', m.role]">
            <strong>{{ m.role === "user" ? "你" : "AI" }}：</strong>{{ m.content }}
            <!-- v0.1.12: 消息时间戳（HH:mm:SS 灰色右下角） -->
            <span class="msg-timestamp">{{ formatChatTimestamp(m.timestamp) }}</span>
            <!-- v0.1.10: 单条删除按钮（hover 显示） -->
            <button class="msg-delete" @click="removeMessage(i)" title="删除这条消息">×</button>
          </div>
          <div v-if="messages.length === 0" class="empty-hint">
            👈 试试右侧工具面板，例如调用 <code>text_stats</code> 统计 README.md
          </div>
        </div>
        <form @submit.prevent="sendMessage" class="input-row">
          <input v-model="userInput" :disabled="isBusy" placeholder="输入消息，回车发送" />
          <button type="submit" :disabled="isBusy">
            {{ isBusy ? "流式中..." : "发送" }}
          </button>
        </form>
      </section>

      <!-- 右：工具面板（v0.0.9 新增） -->
      <aside class="tool-pane">
        <div class="tool-header">
          <h2>🛠 工具面板</h2>
          <span class="tool-count">{{ filteredTools.length }} / {{ tools.length }} 个</span>
          <button class="refresh" @click="refreshToolList" :disabled="isToolBusy">🔄</button>
          <label class="append-toggle" title="工具结果自动回填聊天">
            <input type="checkbox" v-model="appendResultToChat" />
            <span class="append-icon">{{ appendResultToChat ? '💬' : '💭' }}</span>
          </label>
          <button class="history-btn" @click="toggleHistory">📚</button>
        </div>

        <!-- v0.1.9: Tool 搜索过滤（v0.24 WPF 风格） -->
        <input
          v-model="toolSearchKeyword"
          class="tool-search"
          placeholder="🔍 搜索 Tool (name / description)"
        />

        <div v-if="filteredTools.length === 0 && toolSearchKeyword" class="tool-empty">
          无匹配 Tool
        </div>

        <div class="tool-list">
          <div
            v-for="t in filteredTools"
            :key="t.name"
            :class="['tool-card', { destructive: isDestructive(t) }]"
          >
            <div class="tool-card-head">
              <span class="tool-name">{{ t.name }}</span>
              <span v-if="isDestructive(t)" class="risk-badge">⚠️ Destructive</span>
              <span v-else class="risk-badge safe">✓ Safe</span>
            </div>
            <p class="tool-desc">{{ t.description }}</p>
            <button
              class="invoke-btn"
              :disabled="isToolBusy"
              @click="startInvoke(t.name)"
            >
              调用
            </button>
          </div>
        </div>

        <!-- 参数输入浮层 -->
        <div v-if="selectedTool" class="invoke-modal" @click.self="cancelInvoke">
          <div class="invoke-box">
            <h3>调用 {{ selectedTool }}</h3>
            <label>参数 JSON（留空 = {}）：</label>
            <textarea v-model="toolArgsInput" rows="6" placeholder='例如：{"filePath":"D:\\test.md"}' />
            <div class="invoke-actions">
              <button class="cancel" @click="cancelInvoke">取消</button>
              <button class="primary" @click="executeSelected" :disabled="isToolBusy">
                ⚡ 执行
              </button>
            </div>
          </div>
        </div>

        <!-- Destructive 二次确认 -->
        <div v-if="pendingTool" class="invoke-modal" @click.self="pendingTool=''">
          <div class="invoke-box danger">
            <h3>⚠️ 危险操作确认</h3>
            <p>工具 <code>{{ pendingTool }}</code> 是 <strong>Destructive</strong> 工具，<br/>执行后会真实写入/修改/删除文件，且不可撤销。</p>
            <p class="hint">如果只是预览，请先关闭，查阅工具的 <code>dryRun</code> 参数。</p>
            <div class="invoke-actions">
              <button class="cancel" @click="pendingTool=''">取消</button>
              <button class="danger-btn" @click="confirmInvoke" :disabled="isToolBusy">
                我已确认，执行
              </button>
            </div>
          </div>
        </div>

        <!-- 结果展示 -->
        <div v-if="toolResult" class="result-panel success">
          <div class="result-head">
            <strong>✅ 调用成功</strong>
            <div class="export-bar">
              <button class="export-md" @click="exportMarkdown" title="导出为 Markdown">📥 MD</button>
              <button class="export-md" @click="exportJson" title="导出为 JSON">📥 JSON</button>
              <button class="export-md" @click="exportCsv" title="导出为 CSV">📥 CSV</button>
            </div>
            <button class="close" @click="toolResult=null">×</button>
          </div>
          <pre v-if="toolResult.summary" class="summary">{{ toolResult.summary }}</pre>
          <details v-if="toolResult.data">
            <summary>📊 data</summary>
            <pre>{{ JSON.stringify(toolResult.data, null, 2) }}</pre>
          </details>
        </div>
        <div v-if="toolError" class="result-panel error">
          <div class="result-head">
            <strong>❌ 调用失败</strong>
            <button class="close" @click="toolError=''">×</button>
          </div>
          <pre>{{ toolError }}</pre>
        </div>

        <!-- v0.1.2: 历史面板抽屉 -->
        <div v-if="showHistory" class="history-panel">
          <div class="history-head">
            <h3>📚 调用历史</h3>
            <button class="refresh" @click="loadHistory(true)" :disabled="isHistoryLoading">🔄</button>
            <button class="close" @click="showHistory=false">×</button>
          </div>
          <div v-if="isHistoryLoading" class="history-loading">加载中...</div>
          <div v-else-if="history.length === 0" class="history-empty">
            暂无调用记录
          </div>
          <div v-else class="history-list">
            <div
              v-for="(h, idx) in history"
              :key="idx"
              :class="['history-item', { failed: !h.success, expanded: selectedHistoryIdx === idx }]"
              @click="selectHistoryEntry(idx)"
            >
              <div class="history-item-head">
                <span :class="['history-status', h.success ? 'ok' : 'fail']">
                  {{ h.success ? '✅' : '❌' }}
                </span>
                <span class="history-name">{{ h.toolName }}</span>
                <span class="history-time">{{ formatTimestamp(h.timestamp) }}</span>
              </div>
              <div v-if="selectedHistoryIdx === idx" class="history-item-detail">
                <div v-if="h.summary" class="history-summary">{{ h.summary }}</div>
                <details>
                  <summary>📋 args</summary>
                  <pre>{{ h.argsJson }}</pre>
                </details>
                <details v-if="h.errorMessage">
                  <summary>⚠️ error</summary>
                  <pre>{{ h.errorMessage }}</pre>
                </details>
              </div>
            </div>
          </div>
          <!-- v0.1.4: 加载更早分页按钮 -->
          <div v-if="hasMoreHistory" class="history-more">
            <button @click="loadMoreHistory" :disabled="isHistoryLoading">
              {{ isHistoryLoading ? "加载中..." : "📥 加载更早" }}
            </button>
          </div>
          <div v-else class="history-end">— 已加载全部历史 —</div>
        </div>
      </aside>
    </main>
  </div>
</template>

<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
.app { display: flex; flex-direction: column; height: 100vh; background: #f5f6fa; }
header { background: #ff6a00; color: white; padding: 14px 20px; }
header h1 { font-size: 18px; }
header p { font-size: 12px; opacity: 0.9; margin-top: 4px; }
main { flex: 1; display: flex; gap: 12px; padding: 12px; overflow: hidden; }

/* 左聊天区 */
.chat-pane { flex: 7; display: flex; flex-direction: column; background: white; border-radius: 12px; padding: 12px; overflow: hidden; }
.messages { flex: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 8px; padding: 4px; }
.msg { padding: 10px 14px; border-radius: 12px; max-width: 80%; line-height: 1.5; word-break: break-word; white-space: pre-wrap; position: relative; }
.msg-delete { position: absolute; top: 4px; right: 6px; background: transparent; border: none; color: #999; font-size: 14px; line-height: 1; cursor: pointer; padding: 2px 6px; border-radius: 4px; opacity: 0; transition: opacity 0.15s, background 0.15s, color 0.15s; }
.msg:hover .msg-delete { opacity: 1; }
.msg-delete:hover { background: #ffebee; color: #c62828; }

/* v0.1.12: 消息时间戳 */
.msg-timestamp { display: block; font-size: 10px; color: #999; margin-top: 4px; font-family: monospace; }
.msg.user { background: #ff6a00; color: white; align-self: flex-end; }
.msg.assistant { background: #f5f6fa; color: #333; align-self: flex-start; border: 1px solid #e0e0e0; }
.empty-hint { align-self: center; color: #888; padding: 40px; text-align: center; }
.empty-hint code { background: #f0f0f0; padding: 2px 6px; border-radius: 4px; font-family: monospace; }

/* v0.1.11: 聊天头部 + 一键清空 */
.chat-header { display: flex; align-items: center; justify-content: space-between; padding: 6px 8px; border-bottom: 1px solid #eee; margin-bottom: 6px; }
.msg-count { font-size: 11px; color: #999; padding: 2px 6px; background: #f5f6fa; border-radius: 10px; }
.clear-all { background: white; color: #c62828; border: 1px solid #ffcdd2; border-radius: 4px; padding: 2px 10px; cursor: pointer; font-size: 12px; }
.clear-all:hover:not(:disabled) { background: #ffebee; }
.clear-all:disabled { opacity: 0.4; cursor: not-allowed; }

/* v0.1.11: 二次确认模态 */
.confirm-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.4); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.confirm-modal { background: white; border-radius: 12px; padding: 20px 24px; max-width: 360px; box-shadow: 0 12px 48px rgba(0,0,0,0.25); }
.confirm-modal h3 { font-size: 16px; margin-bottom: 10px; color: #333; }
.confirm-modal p { font-size: 13px; color: #666; margin-bottom: 16px; }
.confirm-actions { display: flex; gap: 8px; justify-content: flex-end; }
.confirm-actions button { border: none; border-radius: 6px; padding: 6px 16px; cursor: pointer; font-size: 13px; }
.confirm-actions .cancel { background: #f0f0f0; color: #333; }
.confirm-actions .cancel:hover { background: #e0e0e0; }
.confirm-actions .confirm { background: #c62828; color: white; font-weight: bold; }
.confirm-actions .confirm:hover { background: #b71c1c; }

.input-row { display: flex; gap: 8px; margin-top: 8px; }
.input-row input { flex: 1; padding: 10px 14px; border: 1px solid #ddd; border-radius: 8px; font-size: 14px; }
.input-row button { padding: 10px 20px; background: #ff6a00; color: white; border: none; border-radius: 8px; cursor: pointer; font-size: 14px; }
.input-row button:disabled { opacity: 0.5; cursor: not-allowed; }

/* 右工具面板 */
.tool-pane { flex: 3; display: flex; flex-direction: column; background: white; border-radius: 12px; padding: 12px; overflow: hidden; }
.tool-header { display: flex; align-items: center; gap: 8px; padding-bottom: 8px; border-bottom: 1px solid #eee; }
.tool-header h2 { font-size: 16px; flex: 1; }
.tool-count { font-size: 12px; color: #888; padding: 2px 8px; background: #f5f6fa; border-radius: 10px; }

/* v0.1.9: Tool 搜索过滤输入 */
.tool-search { width: 100%; padding: 6px 10px; border: 1px solid #ddd; border-radius: 6px; font-size: 12px; margin: 6px 0; box-sizing: border-box; outline: none; }
.tool-search:focus { border-color: #ff6a00; }
.tool-empty { padding: 20px; text-align: center; color: #999; font-size: 13px; background: #f5f6fa; border-radius: 6px; margin: 8px 0; }
.refresh { background: none; border: 1px solid #ddd; border-radius: 6px; padding: 4px 10px; cursor: pointer; }
.tool-list { flex: 1; overflow-y: auto; padding: 8px 0; display: flex; flex-direction: column; gap: 8px; }
.tool-card { padding: 10px; border: 1px solid #e0e0e0; border-radius: 8px; background: #fafafa; }
.tool-card.destructive { border-color: #ff9800; background: #fff8e1; }
.tool-card-head { display: flex; align-items: center; gap: 6px; margin-bottom: 6px; }
.tool-name { font-family: monospace; font-size: 13px; font-weight: bold; flex: 1; }
.risk-badge { font-size: 10px; padding: 2px 6px; border-radius: 4px; }
.risk-badge.safe { background: #c8e6c9; color: #2e7d32; }
.risk-badge:not(.safe) { background: #ffccbc; color: #bf360c; }
.tool-desc { font-size: 11px; color: #666; line-height: 1.4; margin-bottom: 8px; max-height: 50px; overflow-y: auto; }
.invoke-btn { width: 100%; padding: 6px; background: #ff6a00; color: white; border: none; border-radius: 6px; cursor: pointer; font-size: 13px; }
.invoke-btn:disabled { opacity: 0.5; cursor: not-allowed; }

/* 浮层 */
.invoke-modal { position: fixed; inset: 0; background: rgba(0,0,0,0.4); display: flex; align-items: center; justify-content: center; z-index: 100; }
.invoke-box { background: white; border-radius: 12px; padding: 20px; min-width: 380px; max-width: 90vw; box-shadow: 0 8px 32px rgba(0,0,0,0.2); }
.invoke-box.danger { border: 2px solid #ff9800; }
.invoke-box h3 { margin-bottom: 12px; color: #333; }
.invoke-box p { font-size: 14px; color: #555; margin-bottom: 8px; }
.invoke-box p.hint { font-size: 12px; color: #888; background: #f5f6fa; padding: 8px; border-radius: 6px; }
.invoke-box label { display: block; font-size: 12px; color: #666; margin-bottom: 4px; }
.invoke-box textarea { width: 100%; padding: 8px; border: 1px solid #ddd; border-radius: 6px; font-family: monospace; font-size: 12px; resize: vertical; }
.invoke-actions { display: flex; gap: 8px; margin-top: 12px; justify-content: flex-end; }
.invoke-actions button { padding: 8px 16px; border-radius: 6px; cursor: pointer; font-size: 14px; border: 1px solid #ddd; background: white; }
.invoke-actions .primary { background: #ff6a00; color: white; border: none; }
.invoke-actions .danger-btn { background: #ff5252; color: white; border: none; }

/* 结果 */
.result-panel { padding: 10px; border-radius: 8px; margin-top: 8px; max-height: 200px; overflow-y: auto; font-size: 12px; }
.result-panel.success { background: #e8f5e9; border: 1px solid #a5d6a7; }
.result-panel.error { background: #ffebee; border: 1px solid #ef9a9a; }
.result-head { display: flex; justify-content: space-between; margin-bottom: 6px; }
.result-head .close { background: none; border: none; cursor: pointer; font-size: 16px; padding: 0 4px; }
.summary { white-space: pre-wrap; word-break: break-word; }
.result-panel details { margin-top: 6px; }
.result-panel details pre { font-size: 11px; padding: 6px; background: rgba(0,0,0,0.05); border-radius: 4px; max-height: 150px; overflow-y: auto; }

/* v0.1.3: 导出 MD 按钮 */
/* v0.1.5: 多格式导出按钮组 */
.export-bar { display: inline-flex; gap: 4px; margin-left: auto; }
.export-md { background: #ff6a00; color: white; border: none; border-radius: 4px; padding: 2px 10px; cursor: pointer; font-size: 12px; font-weight: bold; }
.export-md:hover { background: #e55e00; }
.result-head { display: flex; align-items: center; gap: 8px; }

/* v0.1.2: 历史按钮 + 历史面板 */
.history-btn { background: none; border: 1px solid #ddd; border-radius: 6px; padding: 4px 10px; cursor: pointer; font-size: 14px; }

/* v0.1.8: Tool 结果回填聊天开关 */
.append-toggle { display: inline-flex; align-items: center; gap: 2px; cursor: pointer; font-size: 14px; padding: 4px 6px; border-radius: 6px; border: 1px solid #ddd; user-select: none; background: none; }
.append-toggle:hover { background: #f5f5f5; }
.append-toggle input { margin: 0; cursor: pointer; }
.append-icon { font-size: 14px; line-height: 1; }
.history-panel { position: absolute; right: 12px; top: 60px; bottom: 12px; width: 360px; background: white; border: 1px solid #ddd; border-radius: 12px; box-shadow: 0 8px 32px rgba(0,0,0,0.15); display: flex; flex-direction: column; overflow: hidden; z-index: 50; }
.tool-pane { position: relative; }
.history-head { display: flex; align-items: center; gap: 8px; padding: 10px 12px; border-bottom: 1px solid #eee; background: #fafafa; }
.history-head h3 { font-size: 14px; flex: 1; }
.history-head .refresh { background: none; border: 1px solid #ddd; border-radius: 4px; padding: 2px 8px; cursor: pointer; font-size: 12px; }
.history-head .close { background: none; border: none; cursor: pointer; font-size: 18px; padding: 0 4px; }
.history-loading, .history-empty { padding: 20px; text-align: center; color: #888; font-size: 13px; }
.history-list { flex: 1; overflow-y: auto; padding: 4px; }
.history-item { padding: 8px 10px; border-radius: 6px; margin-bottom: 4px; cursor: pointer; background: #f5f6fa; border: 1px solid transparent; }
.history-item:hover { background: #fff3e0; }
.history-item.failed { background: #ffebee; }
.history-item.expanded { background: #fff8e1; border-color: #ff6a00; }
.history-item-head { display: flex; align-items: center; gap: 6px; font-size: 12px; }
.history-status { font-size: 13px; }
.history-name { font-family: monospace; font-weight: bold; flex: 1; color: #333; }
.history-time { font-size: 10px; color: #888; }
.history-item-detail { margin-top: 6px; font-size: 11px; }
.history-summary { background: white; padding: 6px 8px; border-radius: 4px; margin-bottom: 6px; word-break: break-word; white-space: pre-wrap; }
.history-item-detail details { margin-top: 4px; }
.history-item-detail details pre { font-size: 10px; padding: 6px; background: rgba(0,0,0,0.05); border-radius: 4px; max-height: 100px; overflow-y: auto; white-space: pre-wrap; word-break: break-all; }

/* v0.1.4: 分页加载更早 */
.history-more { padding: 8px 12px; text-align: center; }
.history-more button { background: white; border: 1px solid #ddd; border-radius: 6px; padding: 6px 16px; cursor: pointer; font-size: 12px; color: #555; }
.history-more button:hover:not(:disabled) { border-color: #ff6a00; color: #ff6a00; }
.history-more button:disabled { opacity: 0.5; cursor: not-allowed; }
.history-end { padding: 12px; text-align: center; color: #aaa; font-size: 11px; }
</style>
