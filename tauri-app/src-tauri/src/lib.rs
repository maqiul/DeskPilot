use serde::{Deserialize, Serialize};
use std::process::{Command, Stdio};

const SIDECAR_URL: &str = "http://localhost:5180";
const SIDECAR_PORT: &str = "5180";
const SIDECAR_TRIPLE: &str = "x86_64-pc-windows-msvc";

#[derive(Serialize, Deserialize, Debug, Clone)]
struct ChatResponse {
    reply: String,
    success: bool,
    version: Option<String>,
}

/// v0.0.3: 调用 .NET 8 Sidecar 发送聊天消息。
/// Sidecar 启动 .NET Minimal API 服务（http://localhost:5180/api/chat）。
/// MVP 阶段只发一条 prompt → .NET 同步返回 reply（未来升级 SSE 流式）。
#[tauri::command]
async fn send_chat(prompt: String) -> Result<String, String> {
    let url = format!("{}/api/chat?prompt={}", SIDECAR_URL, urlencoding::encode(&prompt));

    let client = reqwest::Client::new();
    let response = client
        .get(&url)
        .send()
        .await
        .map_err(|e| format!("调用 .NET Sidecar 失败：{}。请确认 Sidecar 已启动（地址 {}）", e, SIDECAR_URL))?;

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

/// v0.0.3: Tauri 启动时自动拉起 .NET Sidecar（绕过 tauri-plugin-shell 的 .dll 后缀 bug）。
///
/// 二进制路径：从 `Cargo workspace` 推理到 `binaries/deskpilot-server-x86_64-pc-windows-msvc.exe`，
/// 该目录包含完整的 .NET self-contained 运行时 + 所有依赖 .dll。
fn start_sidecar() -> Result<(), String> {
    // 当前 exe: target/release/deskpilot-v2.exe
    // binaries 目录: src-tauri/binaries/
    let tauri_exe = std::env::current_exe()
        .map_err(|e| format!("获取当前 Tauri exe 路径失败：{}", e))?;

    // 从 target/release/ 回到 src-tauri/binaries/
    let sidecar_dir = tauri_exe
        .parent()                         // target/release/
        .and_then(|p| p.parent())          // target/
        .and_then(|p| p.parent())          // src-tauri/
        .ok_or("无法解析 src-tauri 目录")?
        .join("binaries");

    // Tauri sidecar 名: deskpilot-server + triple suffix
    let exe_path = sidecar_dir.join(format!("deskpilot-server-{}.exe", SIDECAR_TRIPLE));

    if !exe_path.exists() {
        return Err(format!(
            ".NET Sidecar 找不到：{}。请重新执行 'dotnet publish D:\\opensource\\DeskPilot\\src\\DeskPilot.Server -c Release -r win-x64 --self-contained true -o src-tauri/binaries'",
            exe_path.display()
        ));
    }

    let mut child = Command::new(&exe_path)
        .arg(format!("--urls=http://localhost:{}", SIDECAR_PORT))
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .map_err(|e| format!("启动 .NET Sidecar 失败：{} ({:?})", e, exe_path))?;

    println!("✅ .NET Sidecar 已启动 (PID {})：{}", child.id(), SIDECAR_URL);

    // 转储 .NET 输出到 stderr（开发模式可见 + 不阻塞）
    if let Some(stderr) = child.stderr.take() {
        std::thread::spawn(move || {
            use std::io::{BufRead, BufReader};
            let reader = BufReader::new(stderr);
            for line in reader.lines().flatten() {
                eprintln!("[.NET] {}", line);
            }
        });
    }

    Ok(())
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .setup(|_app| {
            // v0.0.3: Tauri 启动时自动拉 .NET Sidecar
            match start_sidecar() {
                Ok(_) => {
                    // v0.0.6: 后台线程等 .NET 起来（最多 5 秒 retry）
                    std::thread::spawn(|| {
                        let client = reqwest::blocking::Client::builder()
                            .timeout(std::time::Duration::from_secs(2))
                            .build()
                            .unwrap_or_else(|_| reqwest::blocking::Client::new());
                        for i in 1..=10 {
                            std::thread::sleep(std::time::Duration::from_millis(500));
                            // v0.1.13: 升级为深度探活（/api/health 检查 ToolRegistry + HistoryStore）
                            let health_result = client
                                .get(format!("{}/api/health", SIDECAR_URL))
                                .send()
                                .ok()
                                .and_then(|r| r.json::<serde_json::Value>().ok());
                            if let Some(json) = health_result {
                                let status = json.get("status").and_then(|v| v.as_str()).unwrap_or("");
                                if status == "ready" || status == "degraded" {
                                    let tools = json.pointer("/checks/toolRegistry/count")
                                        .and_then(|v| v.as_u64())
                                        .unwrap_or(0);
                                    let hist_ok = json.pointer("/checks/historyStore/ok")
                                        .and_then(|v| v.as_bool())
                                        .unwrap_or(false);
                                    println!(
                                        "✅ Sidecar 深度健康检查通过（第 {} 次，status={}，tools={}，history_ok={}）：{}",
                                        i, status, tools, hist_ok, SIDECAR_URL
                                    );
                                    return;
                                }
                            }
                        }
                        eprintln!(
                            "⚠️ Sidecar 10 次重试后仍未响应。前端 send_chat 可能失败。"
                        );
                    });
                }
                Err(e) => {
                    eprintln!("⚠️ .NET Sidecar 启动失败：{}。前端调 send_chat 会失败。", e);
                }
            }
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![send_chat])
        .run(tauri::generate_context!())
        .expect("Tauri 启动失败");
}
