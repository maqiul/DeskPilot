# DeskPilot v2 (Tauri 2.x + Vue 3 + .NET 8 Sidecar)

> v0.0.1 骨架。WPF v0.28.1 的 Tauri 重写起点。
> 老仓 [DeskPilot](https://github.com/maqiul/DeskPilot) 保留在 `master` 分支（v0.28.1 是 WPF 最后版本）。

## 🎯 设计

| 层 | 技术 |
|---|---|
| 桌面壳 | Tauri 2.x（Rust）|
| 前端 | Vue 3 + Vite + TypeScript |
| 后端 | .NET 8 Minimal API（**侧车进程** Sidecar，保留 18 工具 + SemanticKernel + 335 测试）|
| 进程通信 | Tauri ↔ .NET 走 HTTP（`http://localhost:5180`）|

**关键决策**：**.NET 后端 = 保留 WPF 仓所有业务代码 + 暴露 HTTP API**。Tauri 纯前端。
详见 [docs/architecture.md](./docs/architecture.md)。

## 📂 目录

```
tauri-app/
├── src/                  # Vue 3 前端
│   ├── App.vue          # MVP 聊天窗口
│   ├── main.ts          # Vue 入口
├── src-tauri/           # Tauri 2.x Rust 后端
│   ├── src/
│   │   ├── main.rs      # Tauri 入口
│   │   └── lib.rs       # Tauri 命令（send_chat + start_dotnet_sidecar）
│   ├── Cargo.toml
│   ├── tauri.conf.json
│   └── capabilities/
├── package.json
├── vite.config.ts
└── index.html
```

## 🚀 MVP 范围（v0.0.1）

- [x] Tauri 2.x 窗口（900x640）
- [x] Vue 3 聊天窗口 UI
- [x] 编译通过（Tauri Rust 13.8MB exe + 前端 65KB gzipped）
- [ ] .NET 8 Sidecar 进程（HTTP API）
- [ ] end-to-end 验证（Vue ↔ Tauri ↔ .NET 流式输出）

## 🛠️ 开发命令

```bash
# 装前端依赖
cd tauri-app && npm install

# 跑开发模式（前端 HMR + Tauri 窗口）
npm run tauri dev

# 编译 release exe
npm run tauri build
```

## 🛑 路线图

| 版本 | 内容 |
|---|---|
| v0.0.1（当前）| Tauri + Vue 骨架编译通过 |
| v0.0.2 | .NET 8 Sidecar Minimal API + 流式输出 |
| v0.0.3 | 复用 WPF v0.28.1 的 18 工具 + SK |
| v0.1.0 | 第一个可用 Tauri 桌面助手 |
| ... | 逐步迁移 v0.28.1 → v2.x 的所有功能 |
