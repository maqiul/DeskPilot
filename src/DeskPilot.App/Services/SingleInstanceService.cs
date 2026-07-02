using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;

namespace DeskPilot.App.Services;

/// <summary>
/// v0.19.0: 单实例 Mutex 服务
/// 防止 DeskPilot 多开：第二次启动时激活已有窗口并退出
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Global\\DeskPilot.SingleInstance.Mutex.v1";
    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    private readonly Mutex? _mutex;
    private readonly bool _isFirstInstance;

    public SingleInstanceService()
    {
        _mutex = new Mutex(initiallyOwned: true, name: MutexName, out var createdNew);
        _isFirstInstance = createdNew;
    }

    /// <summary>
    /// 当前进程是否为第一个实例
    /// </summary>
    public bool IsFirstInstance => _isFirstInstance;

    /// <summary>
    /// 激活另一个实例的主窗口（第二次启动时调用）
    /// 通过遍历进程找到 Mutex 同名进程 + Win32 ShowWindow API 恢复窗口
    /// </summary>
    public void ActivateExistingInstance()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName(currentProcess.ProcessName))
            {
                if (process.Id == currentProcess.Id) continue;
                var handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero) continue;

                // Win32 ShowWindow API 恢复 + 激活窗口
                ShowWindow(handle, SW_RESTORE);
                ShowWindow(handle, SW_SHOW);
                SetForegroundWindow(handle);
                return;
            }
        }
        catch
        {
            // 激活失败不影响退出流程
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public void Dispose()
    {
        if (_mutex == null) return;
        if (_isFirstInstance)
        {
            try { _mutex.ReleaseMutex(); } catch { /* ignored */ }
        }
        _mutex.Dispose();
    }
}