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
    private ISkillService? _skillService;
    private ISkillExecutor? _skillExecutor;
    private CancellationTokenSource? _cts;

    public ChatViewModel(IChatService chatService, ISkillService? skillService = null, ISkillExecutor? skillExecutor = null)
    {
        _chatService = chatService;
        _skillService = skillService;
        _skillExecutor = skillExecutor;
        Messages = new ObservableCollection<ChatMessage>();
        Messages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMessages));
        EnabledSkills = new ObservableCollection<Skill>();
        StepProgresses = new ObservableCollection<StepProgress>();
        UpdateBadgeMap();
        RefreshSkills();
        if (_skillService != null) _skillService.SkillsChanged += (_, _) => RefreshSkills();
        HookToolEvents(chatService);
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
}