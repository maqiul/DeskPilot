// 防止 Windows 隐藏控制台窗口
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    deskpilot_v2_lib::run()
}
