using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskPilot.Core.Models;
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
    /// <summary>v0.25.0: 消息时间戳（默认 UtcNow，转本地时区显示）。</summary>
    [ObservableProperty] private DateTime _timestamp = DateTime.UtcNow;

    public ChatMessage() { }
    public ChatMessage(string role, string content)
    {
        _role = role;
        _content = content;
        _timestamp = DateTime.UtcNow;
    }

    /// <summary>v0.25.0: 本地时区时间（UI 绑定用，避免 XAML 时区转换）。</summary>
    public string LocalTimeText => Timestamp.ToLocalTime().ToString("HH:mm:ss");
}

/// <summary>
/// 聊天窗口的视图模型。
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private IChatService _chatService;
    private ISkillService? _skillService;
    private ISkillExecutor? _skillExecutor;
    private CancellationTokenSource? _cts;
    // v0.15: 技能中心窗口工厂 delegate（避免 ViewModel 直接 new Window 违反 MVVM）
    private readonly System.Func<DeskPilot.App.Views.SkillCenterWindow>? _skillCenterFactory;

    /// <summary>v0.20.0: 窗口标题（含版本号）— 绑定到 ChatWindow.Title。</summary>
    public string WindowTitle
    {
        get
        {
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return ver != null
                ? $"DeskPilot 桌面 AI 助手 v{ver.Major}.{ver.Minor}.{ver.Build}"
                : "DeskPilot 桌面 AI 助手";
        }
    }

    public ChatViewModel(IChatService chatService, ISkillService? skillService = null, ISkillExecutor? skillExecutor = null, System.Func<DeskPilot.App.Views.SkillCenterWindow>? skillCenterFactory = null)
    {
        _chatService = chatService;
        _skillService = skillService;
        _skillExecutor = skillExecutor;
        _skillCenterFactory = skillCenterFactory;
        Messages = new ObservableCollection<ChatMessage>();
        Messages.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasMessages));
            // v0.24.0: 消息集合变化时通知过滤结果重新计算
            OnPropertyChanged(nameof(FilteredMessages));
            OnPropertyChanged(nameof(MatchCountText));
        };
        EnabledSkills = new ObservableCollection<Skill>();
        StepProgresses = new ObservableCollection<StepProgress>();
        UpdateBadgeMap();
        RefreshSkills();
        if (_skillService != null) _skillService.SkillsChanged += (_, _) => RefreshSkills();
        HookToolEvents(chatService);
    }

    /// <summary>v0.15: 打开独立技能中心窗口（Ctrl+Shift+K / Menu 「技能 → 打开技能中心」触发）。
    /// 用工厂 delegate 避免 ViewModel 直接 new Window（保持 MVVM 纯净）。</summary>
    [RelayCommand]
    private void ShowSkillCenter()
    {
        if (_skillCenterFactory == null) return;
        var win = _skillCenterFactory();
        win.Show();
        win.Activate();
    }

    /// <summary>v0.9: 启用的技能列表（顶部快捷横条数据源）。v0.10: 内置+已安装合并去重。</summary>
    public ObservableCollection<Skill> EnabledSkills { get; }

    /// <summary>v0.10: 有可用更新的技能 ID 集合（XAML 用 HasUpdate 触发 🔄 角标）。</summary>
    public System.Collections.Generic.HashSet<string> UpdatedSkillIds { get; } = new();

    /// <summary>v0.10: 是否显示"📦 已安装 N"小标签（横条标题旁）。</summary>
    public bool HasInstalledSkills => _skillService != null && _skillService.Custom.Count > 0;
    public int InstalledSkillCount => _skillService?.Custom.Count ?? 0;

    private async void UpdateBadgeMap()
    {
        // fire-and-forget：异步拉取更新状态，写入 UpdatedSkillIds
        if (_skillService == null) return;
        try
        {
            var updates = await _skillService.CheckUpdatesAsync().ConfigureAwait(true);
            UpdatedSkillIds.Clear();
            foreach (var kv in updates)
                if (kv.Value.HasUpdate) UpdatedSkillIds.Add(kv.Key);
            OnPropertyChanged(nameof(UpdatedSkillIds));
            // 强制刷新横条（让 HasUpdate 触发重新绑定）
            RefreshSkills();
        }
        catch
        {
            // 静默失败：拉取更新失败不影响横条显示
        }
    }

    private void RefreshSkills()
    {
        EnabledSkills.Clear();
        if (_skillService == null) return;
        // v0.10: 内置 + 已安装技能合并（同 ID 跳过） + 写入 HasUpdate 角标
        var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var s in _skillService.Enabled)
        {
            if (!seen.Add(s.Id)) continue;
            var withBadge = s with { HasUpdate = UpdatedSkillIds.Contains(s.Id) };
            EnabledSkills.Add(withBadge);
        }
        OnPropertyChanged(nameof(HasInstalledSkills));
        OnPropertyChanged(nameof(InstalledSkillCount));
    }

    /// <summary>
    /// 是否有消息：用于切换"空状态欢迎卡片"和"消息列表"。
    /// </summary>
    public bool HasMessages => Messages.Count > 0;

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

    /// <summary>v0.12: 多步执行进度（聊天窗口内嵌的 SectionCard 数据源）。</summary>
    public ObservableCollection<StepProgress> StepProgresses { get; }

    /// <summary>v0.12: 是否有正在展示的步骤进度（用于切换 SectionCard 可见性）。</summary>
    public bool HasStepProgress => StepProgresses.Count > 0;
    public bool IsStepRunning { get; set; }

    /// <summary>v0.12: 触发技能 — 多步走 SkillExecutor，单步保留 v0.9 行为（填入 PromptTemplate 后自动发送）。</summary>
    public async Task TriggerSkillAsync(Skill skill, CancellationToken ct = default)
    {
        if (skill == null) return;

        // v0.12: 多步技能 → 执行器路径
        if (skill.IsMultiStep && _skillExecutor != null)
        {
            StepProgresses.Clear();
            OnPropertyChanged(nameof(HasStepProgress));
            IsStepRunning = true;

            var progress = new Progress<StepProgress>(p =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    int idx = StepProgresses.IndexOf(StepProgresses.FirstOrDefault(x => x.Index == p.Index));
                    if (idx >= 0) StepProgresses[idx] = p;
                    else StepProgresses.Add(p);
                    OnPropertyChanged(nameof(HasStepProgress));
                });
            });

            try
            {
                var result = await _skillExecutor.ExecuteAsync(skill, progress, ct).ConfigureAwait(true);
                Messages.Add(new ChatMessage("assistant", result.Summary));
            }
            catch (System.Exception ex)
            {
                Messages.Add(new ChatMessage("assistant", $"❌ 技能 {skill.Name} 执行失败：{ex.Message}"));
            }
            finally
            {
                IsStepRunning = false;
                OnPropertyChanged(nameof(IsStepRunning));
            }
            return;
        }

        // v0.9 单步（PromptTemplate）路径：填入输入框 + 自动 send
        if (!string.IsNullOrWhiteSpace(skill.PromptTemplate))
        {
            UserInput = skill.PromptTemplate;
            if (SendCommand.CanExecute(null))
                await SendAsync();
        }
    }

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

        // 先插入空 assistant 气泡，流式追加内容
        var assistantMsg = new ChatMessage("assistant", "");
        Messages.Add(assistantMsg);

        try
        {
            await foreach (var chunk in _chatService.ChatStreamAsync(prompt, _cts.Token))
            {
                assistantMsg.Content += chunk;
            }
            if (string.IsNullOrEmpty(ToolStatus) || ToolStatus.StartsWith("💭"))
                ToolStatus = string.Empty;
        }
        catch (System.Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                if (assistantMsg.Content.Length == 0)
                    assistantMsg.Content = "⏸️ 已取消";
                else
                    assistantMsg.Content += "\n\n⏸️ 已取消";
                ToolStatus = string.Empty;
            }
            else
            {
                assistantMsg.Content = $"❌ 出错了：{ex.Message}";
                ToolStatus = $"❌ 异常：{ex.Message}";
            }
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

    /// <summary>v0.27.0: 从对话中删除单条消息（按 ReferenceEquals 找到原对象）。</summary>
    [RelayCommand]
    private void DeleteMessage(ChatMessage? message)
    {
        if (message == null) return;
        // 不用 IndexOf(message)：SearchKeyword 过滤时 FilteredMessages 是临时 List，索引不对应原 Messages
        for (int i = 0; i < Messages.Count; i++)
        {
            if (ReferenceEquals(Messages[i], message))
            {
                Messages.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>v0.26.0: 复制单条消息内容到剪贴板。</summary>
    [RelayCommand]
    private void CopyMessage(ChatMessage? message)
    {
        if (message == null || string.IsNullOrEmpty(message.Content)) return;
        try
        {
            System.Windows.Clipboard.SetText(message.Content);
            ToolStatus = "📋 已复制到剪贴板";
        }
        catch
        {
            // 剪贴板可能被其他程序占用，静默失败
            ToolStatus = "❌ 复制失败";
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Messages.Clear();
        Messages.Add(new ChatMessage("assistant", "对话已清空。有什么可以帮你的？"));
        ToolStatus = string.Empty;

        // v0.7: 同时清空本地持久化记忆
        if (_chatService is SemanticKernelChatService sk)
            sk.ClearMemory();
    }

    /// <summary>v0.22.0: 导出对话为 Markdown 文件。</summary>
    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    /// <summary>v0.24.0: 搜索结果消息集合（基于 SearchKeyword 过滤 Messages）。</summary>
    public System.Collections.Generic.List<ChatMessage> FilteredMessages
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchKeyword))
                return Messages.ToList();
            return Messages.Where(m => m.Content.Contains(SearchKeyword, System.StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    /// <summary>v0.24.0: 匹配结果统计文本（如 "3 / 10" 或 ""）。</summary>
    public string MatchCountText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchKeyword)) return string.Empty;
            return $"{FilteredMessages.Count} / {Messages.Count}";
        }
    }

    partial void OnSearchKeywordChanged(string value)
    {
        // v0.24.0: 关键词变化时通知 FilteredMessages 和 MatchCountText 重新计算
        OnPropertyChanged(nameof(FilteredMessages));
        OnPropertyChanged(nameof(MatchCountText));
    }
    [RelayCommand]
    private void ExportToMarkdown(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# DeskPilot 对话记录");
            sb.AppendLine();
            sb.AppendLine($"*导出时间：{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}*");
            sb.AppendLine();
            foreach (var msg in Messages)
            {
                var role = msg.Role == "user" ? "👤 用户" : "🤖 AI";
                sb.AppendLine($"## {role}");
                sb.AppendLine();
                sb.AppendLine(msg.Content);
                sb.AppendLine();
            }
            System.IO.File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
            ToolStatus = $"✅ 已导出 {Messages.Count} 条消息到 {System.IO.Path.GetFileName(filePath)}";
        }
        catch (System.Exception ex)
        {
            ToolStatus = $"❌ 导出失败：{ex.Message}";
        }
    }
}