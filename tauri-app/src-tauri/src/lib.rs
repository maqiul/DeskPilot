use serde::{Deserialize, Serialize};
use tauri_plugin_shell::ShellExt;

#[derive(Serialize, Deserialize, Debug, Clone)]
struct ChatResponse {
    reply: String,
    success: bool,
}

/// v0.0.1: 调用 .NET 8 Sidecar 发送聊天消息。
/// Sidecar 启动 .NET Minimal API 服务（http://localhost:5180/api/chat）。
/// MVP 阶段只发一条 prompt → .NET 同步返回 reply（未来升级 SSE 流式）。
#[tauri::command]
async fn send_chat(prompt: String) -> Result<String, String> {
    // 调用本地 .NET 8 HTTP API（Sidecar 启动时分配 5180 端口）
    let url = format!("http://localhost:5180/api/chat?prompt={}", urlencoding::encode(&prompt));

    let client = reqwest::Client::new();
    let response = client
        .get(&url)
        .send()
        .await
        .map_err(|e| format!("调用 .NET 失败：{}", e))?;

    if !response.status().is_success() {
        return Err(format!(".NET 返回错误：{}", response.status()));
    }

    let body: ChatResponse = response
        .json()
        .await
        .map_err(|e| format!("解析 .NET 响应失败：{}", e))?;

    if !body.success {
        return Err(format!(".NET 报告失败：{}", body.reply));
    }

    Ok(body.reply)
}

/// v0.0.1: 启动 .NET 8 Sidecar 进程。
/// Tauri 启动时自动调用本命令（前后端 RPC 桥）。
#[tauri::command]
async fn start_dotnet_sidecar(app: tauri::AppHandle) -> Result<String, String> {
    use tauri_plugin_shell::process::CommandEvent;

    let sidecar_command = app.shell().sidecar("deskpilot-server").map_err(|e| {
        format!("Sidecar 配置错误：{}。请确认 tauri.conf.json 配置了 externalBin 指向 deskpilot-server.exe", e)
    })?;

    let (mut rx, _child) = sidecar_command
        .args(["--urls", "http://localhost:5180"])
        .spawn()
        .map_err(|e| format!("启动 .NET Sidecar 失败：{}", e))?;

    // 异步读取 .NET 输出（不阻塞）
    tauri::async_runtime::spawn(async move {
        while let Some(event) = rx.recv().await {
            match event {
                CommandEvent::Stdout(line) => println!("[.NET] {}", String::from_utf8_lossy(&line)),
                CommandEvent::Stderr(line) => eprintln!("[.NET ERR] {}", String::from_utf8_lossy(&line)),
                _ => {}
            }
        }
    });

    Ok("Sidecar 已启动".to_string())
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .invoke_handler(tauri::generate_handler![send_chat, start_dotnet_sidecar])
        .run(tauri::generate_context!())
        .expect("Tauri 启动失败");
}
