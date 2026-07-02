using DeskPilot.App.Services;
using DeskPilot.App.ViewModels;
using DeskPilot.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskPilot.App.Views;

public partial class ChatWindow : Window
{
    private readonly ChatViewModel _viewModel;
    // v0.18.0: 系统托盘 - 可选注入，关闭时最小化到托盘
    private TrayIconService? _trayIcon;

    public ChatWindow(ChatViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Messages.CollectionChanged += (_, _) => ScrollToBottom();
        Closing += ChatWindow_Closing;
    }

    /// <summary>
    /// v0.18.0: 由 App.xaml.cs 注入托盘服务
    /// </summary>
    public void SetTrayIcon(TrayIconService trayIcon)
    {
        _trayIcon = trayIcon;
    }

    /// <summary>
    /// v0.18.0: 关闭时最小化到托盘而不是退出
    /// 用户通过托盘菜单"退出"才会真正结束进程
    /// </summary>
    private void ChatWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_trayIcon != null)
        {
            e.Cancel = true;
            Hide();
            _trayIcon.Show();
        }
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            if (MessagesScrollViewer.Content is ItemsControl items)
            {
                if (VisualTreeHelper.GetChild(items, 0) is Decorator border)
                {
                    if (border.Child is ScrollViewer inner)
                    {
                        inner.ScrollToEnd();
                        return;
                    }
                }
            }
            MessagesScrollViewer.ScrollToEnd();
        }));
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            if (DataContext is ChatViewModel vm && vm.SendCommand.CanExecute(null))
            {
                vm.SendCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = App.Services.GetRequiredService<SettingsWindow>();
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }

    private void Suggestion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string text })
        {
            _viewModel.UserInput = text;
            if (_viewModel.SendCommand.CanExecute(null))
                _viewModel.SendCommand.Execute(null);
        }
    }

    /// <summary>v0.9: 顶部快捷技能横条点击 — 填入输入框并自动发送。v0.12: 改走 TriggerSkillAsync，多步走 SkillExecutor，单步保留原行为。</summary>
    private async void SkillCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: Skill skill })
        {
            await _viewModel.TriggerSkillAsync(skill);
        }
    }

    /// <summary>v0.15: 顶部菜单「文件 → 退出」点击 — 关闭整个应用。</summary>
    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    /// <summary>v0.22.0: 顶部菜单「文件 → 导出对话」点击 — 弹出 SaveFileDialog 让用户选位置，导出为 Markdown。</summary>
    private void ExportMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"deskpilot-{System.DateTime.Now:yyyyMMdd-HHmmss}.md",
            DefaultExt = ".md",
            Filter = "Markdown 文件 (*.md)|*.md|所有文件 (*.*)|*.*",
            Title = "导出对话为 Markdown"
        };
        if (dlg.ShowDialog() == true)
        {
            _viewModel.ExportToMarkdownCommand.Execute(dlg.FileName);
        }
    }

    /// <summary>v0.15: 顶部菜单「帮助 → 关于」点击 — 显示 DeskPilot 版本信息。</summary>
    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.15.0";
        MessageBox.Show(
            $"DeskPilot 桌面 AI 助手\n\n版本：v{version}\n\n开源项目：https://github.com/maqiul/DeskPilot\n\n专注办公场景的桌面 AI 助手。",
            "关于 DeskPilot",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}