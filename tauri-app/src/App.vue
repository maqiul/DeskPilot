<script setup lang="ts">
import { ref } from "vue";
import { invoke } from "@tauri-apps/api/core";

interface ChatMessage {
  role: "user" | "assistant";
  content: string;
}

const messages = ref<ChatMessage[]>([]);
const userInput = ref("");
const isBusy = ref(false);

// 调用 .NET 8 Minimal API Sidecar（http://localhost:5180/api/chat/stream）
async function sendMessage() {
  const prompt = userInput.value.trim();
  if (!prompt || isBusy.value) return;

  messages.value.push({ role: "user", content: prompt });
  userInput.value = "";
  isBusy.value = true;

  try {
    // v0.0.1: Tauri invoke 调 .NET 命令（先把对话发到 .NET）
    const reply = await invoke<string>("send_chat", { prompt });
    messages.value.push({ role: "assistant", content: reply });
  } catch (e: any) {
    messages.value.push({ role: "assistant", content: `❌ 错误：${e}` });
  } finally {
    isBusy.value = false;
  }
}
</script>

<template>
  <div class="app">
    <header>
      <h1>🛩 DeskPilot v2 (Tauri MVP)</h1>
      <p>.NET 8 Sidecar + Vue 3 + Tauri 2.x</p>
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
          {{ isBusy ? "发送中..." : "发送" }}
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
.msg { padding: 10px 14px; border-radius: 12px; max-width: 80%; line-height: 1.5; }
.msg.user { background: #ff6a00; color: white; align-self: flex-end; }
.msg.assistant { background: white; color: #333; align-self: flex-start; border: 1px solid #e0e0e0; }
.input-row { display: flex; gap: 8px; }
.input-row input { flex: 1; padding: 10px 14px; border: 1px solid #ddd; border-radius: 8px; font-size: 14px; }
.input-row button { padding: 10px 20px; background: #ff6a00; color: white; border: none; border-radius: 8px; cursor: pointer; font-size: 14px; }
.input-row button:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
