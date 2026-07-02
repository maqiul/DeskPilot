using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace DeskPilot.App.Services;

/// <summary>
/// v0.18.0: 系统托盘 NotifyIcon 包装服务
/// 提供最小化到托盘 + 双击恢复窗口 + 右键菜单退出
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly Window _mainWindow;
    private bool _disposed;

    public TrayIconService(Window mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Visible = false,
            Text = "DeskPilot"
        };

        _notifyIcon.MouseDoubleClick += (_, _) => RestoreMainWindow();

        // 右键菜单
        var menu = new WinForms.ContextMenuStrip();
        var showItem = new WinForms.ToolStripMenuItem("显示主窗口");
        showItem.Click += (_, _) => RestoreMainWindow();
        var exitItem = new WinForms.ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(showItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        _notifyIcon.ContextMenuStrip = menu;
    }

    /// <summary>
    /// 显示托盘图标（最小化时调用）
    /// </summary>
    public void Show()
    {
        if (_disposed) return;
        _notifyIcon.Visible = true;
    }

    /// <summary>
    /// 隐藏托盘图标（退出时调用）
    /// </summary>
    public void Hide()
    {
        if (_disposed) return;
        _notifyIcon.Visible = false;
    }

    /// <summary>
    /// 从托盘恢复主窗口
    /// </summary>
    private void RestoreMainWindow()
    {
        if (_disposed) return;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
        _mainWindow.Activate();
    }

    /// <summary>
    /// 退出应用
    /// </summary>
    private void ExitApplication()
    {
        if (_disposed) return;
        _notifyIcon.Visible = false;
        Application.Current.Shutdown(0);
    }

    /// <summary>
    /// 加载应用图标（从嵌入资源或系统默认）
    /// </summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            // 尝试从应用目录加载 deskpilot.ico
            var iconPath = Path.Combine(AppContext.BaseDirectory, "deskpilot.ico");
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }
        }
        catch
        {
            // 加载失败时使用系统默认图标
        }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}