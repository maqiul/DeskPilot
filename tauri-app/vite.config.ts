import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

// Tauri 2.x 推荐配置：固定端口 + 关闭热重载 host 检查
export default defineConfig({
  plugins: [vue()],
  clearScreen: false,
  server: {
    port: 1420,
    strictPort: true,
    watch: {
      ignored: ["**/src-tauri/**"],
    },
  },
});
