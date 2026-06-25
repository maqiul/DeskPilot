using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskPilot.App.Models;
using DeskPilot.App.Services;
using DeskPilot.Core.Models;
using DeskPilot.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeskPilot.App.ViewModels;

/// <summary>
/// 设置窗口的视图模型。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IModelListerFactory _modelListerFactory;
    private readonly Action? _closeWindow;

    /// <summary>
    /// 生产构造（带默认 WPF 关闭逻辑）。
    /// </summary>
    public SettingsViewModel(ISettingsService settingsService)
        : this(settingsService, CloseWindowViaDispatcher) { }

    /// <summary>
    /// 生产构造：注入 IModelListerFactory（DI 容器提供）。
    /// </summary>
    public SettingsViewModel(ISettingsService settingsService, IModelListerFactory modelListerFactory)
        : this(settingsService, modelListerFactory, CloseWindowViaDispatcher) { }

    /// <summary>
    /// 测试友好的构造：允许注入自定义的"关闭窗口"回调。
    /// </summary>
    public SettingsViewModel(ISettingsService settingsService, Action? closeWindow)
        : this(settingsService, new NullModelListerFactory(), closeWindow) { }

    /// <summary>
    /// 完整构造（测试/生产都用这个）。
    /// </summary>
    public SettingsViewModel(
        ISettingsService settingsService,
        IModelListerFactory modelListerFactory,
        Action? closeWindow)
    {
        _settingsService = settingsService;
        _modelListerFactory = modelListerFactory;
        _closeWindow = closeWindow;

        // 加载现有设置（只调一次 Load，避免重复 IO）
        var current = _settingsService.Load();
        _initialCachedModels = current.CachedModels ?? new Dictionary<string, List<string>>();

        Provider = current.Provider;
        OpenAiApiKey = current.OpenAiApiKey;
        OpenAiModel = current.OpenAiModel;
        DeepSeekApiKey = current.DeepSeekApiKey;
        DeepSeekModel = current.DeepSeekModel;
        OllamaEndpoint = current.OllamaEndpoint;
        OllamaModel = current.OllamaModel;
        RequireConfirmation = current.RequireConfirmation;

        // 初始化可用模型列表（先静态兜底）
        LoadModelsForProvider(Provider);
    }

    private Dictionary<string, List<string>> _initialCachedModels = new();

    // ===== Provider 切换 =====
    [ObservableProperty]
    private AiProvider _provider = AiProvider.OpenAI;

    partial void OnProviderChanged(AiProvider value)
    {
        OnPropertyChanged(nameof(IsOpenAiSelected));
        OnPropertyChanged(nameof(IsDeepSeekSelected));
        OnPropertyChanged(nameof(IsOllamaSelected));
        OnPropertyChanged(nameof(ShowApiKey));
        OnPropertyChanged(nameof(ShowApiEndpoint));
        OnPropertyChanged(nameof(CurrentModelList));
        LoadModelsForProvider(value);
    }

    public bool IsOpenAiSelected => Provider == AiProvider.OpenAI;
    public bool IsDeepSeekSelected => Provider == AiProvider.DeepSeek;
    public bool IsOllamaSelected => Provider == AiProvider.Ollama;

    /// <summary>
    /// 是否需要 API Key（Ollama 不需要）。
    /// </summary>
    public bool ShowApiKey => !IsOllamaSelected;

    /// <summary>
    /// Ollama 需要自定义 endpoint，其他 Provider endpoint 写死。
    /// </summary>
    public bool ShowApiEndpoint => IsOllamaSelected;

    // ===== OpenAI =====
    [ObservableProperty] private string _openAiApiKey = string.Empty;
    [ObservableProperty] private string _openAiModel = "gpt-4o-mini";

    // ===== DeepSeek =====
    [ObservableProperty] private string _deepSeekApiKey = string.Empty;
    [ObservableProperty] private string _deepSeekModel = "deepseek-chat";

    // ===== Ollama =====
    [ObservableProperty] private string _ollamaEndpoint = "http://localhost:11434";
    [ObservableProperty] private string _ollamaModel = "qwen2.5:7b";

    // ===== Key 可见性切换 =====
    [ObservableProperty] private bool _isApiKeyVisible;

    // ===== 权限控制 =====
    [ObservableProperty] private bool _requireConfirmation = true;

    // ===== 保存状态 =====
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasStatus;
    [ObservableProperty] private bool _isError;

    // ===== 模型列表 =====
    /// <summary>
    /// 当前 Provider 的可用模型（下拉框数据源）。
    /// </summary>
    public ObservableCollection<ModelOption> CurrentModelList { get; } = new();

    /// <summary>
    /// 当前选中的 Provider 名称（用于调试/显示）。
    /// </summary>
    public string CurrentProviderName => Provider.ToString();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRefreshEnabled))]
    private bool _isRefreshing;

    public bool IsRefreshEnabled => !IsRefreshing;

    private CancellationTokenSource? _refreshCts;

    [RelayCommand]
    private void ToggleApiKeyVisibility()
    {
        IsApiKeyVisible = !IsApiKeyVisible;
    }

    /// <summary>
    /// 加载当前 Provider 的可用模型（优先级：缓存 → 静态 fallback）。
    /// </summary>
    private void LoadModelsForProvider(AiProvider provider)
    {
        CurrentModelList.Clear();

        var providerKey = provider.ToString();

        // 1. 优先用缓存（如果用户之前刷过）
        if (_initialCachedModels.TryGetValue(providerKey, out var cached) && cached.Count > 0)
        {
            foreach (var id in cached)
                CurrentModelList.Add(new ModelOption { Id = id, DisplayName = id });
            return;
        }

        // 2. fallback 到静态目录
        var fallback = AiModelCatalog.FallbackFor(providerKey);
        foreach (var m in fallback)
            CurrentModelList.Add(ModelOption.FromCore(m));
    }

    /// <summary>
    /// 异步从 Provider 拉取最新模型列表。
    /// </summary>
    [RelayCommand]
    public async Task RefreshModelsAsync()
    {
        if (IsRefreshing) return;

        IsRefreshing = true;
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var ct = _refreshCts.Token;

        try
        {
            ShowInfo($"🔄 正在从 {Provider} 拉取模型列表...");

            var lister = _modelListerFactory.GetLister(Provider);
            var apiKey = Provider switch
            {
                AiProvider.OpenAI => OpenAiApiKey,
                AiProvider.DeepSeek => DeepSeekApiKey,
                _ => null
            };
            var endpoint = Provider == AiProvider.Ollama ? OllamaEndpoint : null;

            var models = await lister.ListAsync(apiKey, endpoint, ct).ConfigureAwait(true);

            if (ct.IsCancellationRequested) return;

            if (models.Count == 0)
            {
                ShowError($"⚠️ 拉取失败（网络错误或鉴权失败），已保留当前列表");
                return;
            }

            // 合并 fallback（保留用户可能需要的旧模型）
            var merged = AiModelCatalog.MergeWithFallback(Provider.ToString(), models);

            CurrentModelList.Clear();
            foreach (var m in merged)
                CurrentModelList.Add(ModelOption.FromCore(m));

            // 持久化缓存到 settings（不动 Provider/Key 等其他字段）
            var settings = _settingsService.Load();
            settings.CachedModels[Provider.ToString()] = models.Select(m => m.Id).ToList();
            _settingsService.Save(settings);

            ShowSuccess($"✅ 已刷新 {merged.Count} 个模型（{Provider}）");
        }
        catch (OperationCanceledException)
        {
            // 用户切了 Provider，忽略
        }
        catch (Exception ex)
        {
            ShowError($"❌ 刷新失败：{ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// 构造 AppSettings（把 UI 字段归一化）。
    /// </summary>
    public AppSettings BuildSettings()
    {
        return new AppSettings
        {
            Provider = Provider,
            OpenAiApiKey = OpenAiApiKey?.Trim() ?? string.Empty,
            OpenAiModel = string.IsNullOrWhiteSpace(OpenAiModel) ? "gpt-4o-mini" : OpenAiModel.Trim(),
            DeepSeekApiKey = DeepSeekApiKey?.Trim() ?? string.Empty,
            DeepSeekModel = string.IsNullOrWhiteSpace(DeepSeekModel) ? "deepseek-chat" : DeepSeekModel.Trim(),
            OllamaEndpoint = string.IsNullOrWhiteSpace(OllamaEndpoint) ? "http://localhost:11434" : OllamaEndpoint.Trim(),
            OllamaModel = string.IsNullOrWhiteSpace(OllamaModel) ? "qwen2.5:7b" : OllamaModel.Trim(),
            CachedModels = _settingsService.Load().CachedModels ?? new Dictionary<string, List<string>>(),
            RequireConfirmation = RequireConfirmation
        };
    }

    /// <summary>
    /// 校验当前设置是否完整（不抛异常，返回 bool + 错误信息）。
    /// </summary>
    public bool Validate(out string error)
    {
        if (Provider != AiProvider.Ollama)
        {
            var key = Provider == AiProvider.OpenAI ? OpenAiApiKey : DeepSeekApiKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                error = $"{Provider} 需要填写 API Key";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    [RelayCommand]
    private void Save()
    {
        if (!Validate(out var error))
        {
            ShowError("❌ " + error);
            return;
        }

        var settings = BuildSettings();
        _settingsService.Save(settings);

        // 通知 App 重建 AI 服务
        ChatServiceChanged?.Invoke(this, settings);

        ShowSuccess($"✅ 设置已保存（{Provider}）");

        // 800ms 后关闭窗口
        _ = DelayedCloseAsync(800);
    }

    [RelayCommand]
    private void Cancel()
    {
        _closeWindow?.Invoke();
    }

    private async Task DelayedCloseAsync(int ms)
    {
        await Task.Delay(ms);
        _closeWindow?.Invoke();
    }

    private void ShowSuccess(string msg)
    {
        StatusMessage = msg;
        HasStatus = true;
        IsError = false;
    }

    private void ShowError(string msg)
    {
        StatusMessage = msg;
        HasStatus = true;
        IsError = true;
    }

    private void ShowInfo(string msg)
    {
        StatusMessage = msg;
        HasStatus = true;
        IsError = false;
    }

    /// <summary>
    /// 设置保存后触发，让 App 重建 IChatService。
    /// </summary>
    public event EventHandler<AppSettings>? ChatServiceChanged;

    /// <summary>
    /// 生产环境默认的"关闭窗口"实现：通过 WPF Dispatcher 关闭 SettingsWindow。
    /// 测试时可注入空 Action 跳过此步。
    /// </summary>
    private static void CloseWindowViaDispatcher()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var win = System.Windows.Application.Current?.Windows
                .OfType<Views.SettingsWindow>()
                .FirstOrDefault();
            win?.Close();
        });
    }
}

/// <summary>
/// 模型列表工厂接口：按 Provider 路由到具体 Lister。
/// 由 DI 容器注入生产实现，测试可注入 Stub。
/// </summary>
public interface IModelListerFactory
{
    IModelLister GetLister(AiProvider provider);
}

/// <summary>
/// 占位实现（无网络访问能力，用于测试或纯静态场景）。
/// </summary>
public sealed class NullModelListerFactory : IModelListerFactory
{
    public IModelLister GetLister(AiProvider provider)
        => throw new NotSupportedException(
            "未配置 IModelListerFactory，请通过 DI 注入真实实现（参见 App.xaml.cs）。");
}