<script setup lang="ts">
import { ref } from "vue";

interface ChatMessage {
  role: "user" | "assistant";
  content: string;
}

const messages = ref<ChatMessage[]>([]);
const userInput = ref("");
const isBusy = ref(false);

// v0.0.5: 直接 fetch .NET Sidecar SSE 流式端点（不走 Tauri command 简化路径）
// http://localhost:5180/api/chat/stream?prompt=...
const SIDE_STREAM = "http://localhost:5180/api/chat/stream";

async function sendMessage() {
  const prompt = userInput.value.trim();
  if (!prompt || isBusy.value) return;

  // v0.0.5: 先 push user prompt，再 push 占位 assistant message（用于流式填充）
  messages.value.push({ role: "user", content: prompt });
  userInput.value = "";
  isBusy.value = true;

  const idx = messages.value.length;
  messages.value.push({ role: "assistant", content: "" });

  try {
    const response = await fetch(`${SIDE_STREAM}?prompt=${encodeURIComponent(prompt)}`);
    if (!response.ok || !response.body) {
      throw new Error(`HTTP ${response.status}`);
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";

    // SSE 协议：data: {...}\n\n
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });

      // 按 \n\n 切分消息
      let idx;
      while ((idx = buffer.indexOf("\n\n")) !== -1) {
        const rawEvent = buffer.slice(0, idx);
        buffer = buffer.slice(idx + 2);

        // 解析 data: 前缀
        const lines = rawEvent.split("\n");
        for (const line of lines) {
          if (line.startsWith("data:")) {
            const data = line.slice(5).trim();
            if (data === "[DONE]") continue;
            try {
              const obj = JSON.parse(data);
              if (typeof obj.chunk === "string") {
                // 追加到 assistant 消息
                messages.value[idx] = {
                  role: "assistant",
                  content: messages.value[idx].content + obj.chunk
                };
              }
            } catch (e) {
              // ignore JSON parse errors
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
</script>

<template>
  <div class="app">
    <header>
      <h1>🛩 DeskPilot v2 (Tauri MVP · 流式)</h1>
      <p>.NET 8 Sidecar + Vue 3 + Tauri 2.x · SSE Stream</p>
    </header>
    <main>
      <div class="messages">
        <div v-for="(m, i) in messages" :key="i" :class="['msg', m.role]">
          <strong>{{ m.role === "user" ? "你" : "AI" }}：</strong>{{ m.content }}
        </div>
      </div>
      <form @submit.prevent="sendMessage" class="input-row">
        <input
          v-model="userInput"
          :disabled="isBusy"
          placeholder="输入消息，回车发送"
        />
        <button type="submit" :disabled="isBusy">
          {{ isBusy ? "流式中..." : "发送" }}
        </button>
      </form>
    </main>
  </div>
</template>

<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
.app { display: flex; flex-direction: column; height: 100vh; background: #f5f6fa; }
header { background: #ff6a00; color: white; padding: 16px 20px; }
header h1 { font-size: 18px; }
header p { font-size: 12px; opacity: 0.9; margin-top: 4px; }
main { flex: 1; display: flex; flex-direction: column; padding: 16px; gap: 12px; overflow: hidden; }
.messages { flex: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 8px; }
.msg { padding: 10px 14px; border-radius: 12px; max-width: 80%; line-height: 1.5; word-break: break-word; white-space: pre-wrap; }
.msg.user { background: #ff6a00; color: white; align-self: flex-end; }
.msg.assistant { background: white; color: #333; align-self: flex-start; border: 1px solid #e0e0e0; }
.input-row { display: flex; gap: 8px; }
.input-row input { flex: 1; padding: 10px 14px; border: 1px solid #ddd; border-radius: 8px; font-size: 14px; }
.input-row button { padding: 10px 20px; background: #ff6a00; color: white; border: none; border-radius: 8px; cursor: pointer; font-size: 14px; }
.input-row button:disabled { opacity: 0.5; cursor: not-allowed; }
</style>