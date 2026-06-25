using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskPilot.Core.Services;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace DeskPilot.App.ViewModels;

/// <summary>
/// 单条聊天消息。
/// </summary>
public partial class ChatMessage : ObservableObject
{
    [ObservableProperty] private string _role = string.Empty;
    [ObservableProperty] private string _content = string.Empty;

    public ChatMessage() { }
    public ChatMessage(string role, string content)
    {
        _role = role;
        _content = content;
    }
}

/// <summary>
/// 聊天窗口的视图模型。
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private IChatService _chatService;
    private CancellationTokenSource? _cts;

    public ChatViewModel(IChatService chatService)
    {
        _chatService = chatService;
        Messages = new ObservableCollection<ChatMessage>
        {
            new("assistant", "你好！我是 DeskPilot 桌面 AI 助手 ✈️\n请告诉我你想做什么，比如：\n• 帮我整理桌面文件\n• 把这个 Excel 按部门拆分\n• 解释这段代码")
        };
        HookToolEvents(chatService);
    }

    /// <summary>
    /// 切换 AI 服务实例（设置窗口保存后调用）。
    /// </summary>
    public void ResetChatService(IChatService newService)
    {
        UnhookToolEvents(_chatService);
        _chatService = newService;
        HookToolEvents(newService);
        Messages.Add(new ChatMessage("assistant", "🔄 AI 服务已切换，欢迎继续对话！"));
    }

    /// <summary>
    /// 订阅 SK 的工具调用事件，把工具状态实时推给 UI。
    /// </summary>
    private void HookToolEvents(IChatService svc)
    {
        if (svc is SemanticKernelChatService sk)
        {
            sk.ToolInvoking += (_, e) =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    ToolStatus = $"🔧 正在调用 {e.ToolName}...";
                });
            };
            sk.ToolInvoked += (_, e) =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    var icon = e.Success ? "✅" : "❌";
                    ToolStatus = $"{icon} {e.ToolName} 完成（{e.ElapsedMs}ms）";
                });
            };
        }
    }

    private void UnhookToolEvents(IChatService svc)
    {
        // 简单做法：不再 unhook，因为 reset 后旧 svc 会被 GC 回收
        // 事件源是 svc 内部的 Kernel，新 svc 会重新订阅自己的 Kernel
    }

    public ObservableCollection<ChatMessage> Messages { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _userInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isBusy;

    /// <summary>
    /// 工具状态：调用中 / 完成 / 失败。显示在状态栏。
    /// </summary>
    [ObservableProperty]
    private string _toolStatus = string.Empty;

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(UserInput);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var prompt = UserInput.Trim();
        UserInput = string.Empty;

        Messages.Add(new ChatMessage("user", prompt));
        IsBusy = true;

        _cts = new CancellationTokenSource();
        ToolStatus = "💭 思考中...";
        try
        {
            var reply = await _chatService.ChatAsync(prompt, _cts.Token);
            Messages.Add(new ChatMessage("assistant", reply));
            if (string.IsNullOrEmpty(ToolStatus) || ToolStatus.StartsWith("💭"))
                ToolStatus = string.Empty;
        }
        catch (System.Exception ex)
        {
            Messages.Add(new ChatMessage("assistant", $"❌ 出错了：{ex.Message}"));
            ToolStatus = $"❌ 异常：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void Clear()
    {
        Messages.Clear();
        Messages.Add(new ChatMessage("assistant", "对话已清空。有什么可以帮你的？"));
        ToolStatus = string.Empty;
    }
}