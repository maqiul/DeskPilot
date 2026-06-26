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
    private readonly ISkillService? _skillService;
    private readonly ISkillMarket? _skillMarket;
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
        : this(settingsService, modelListerFactory, null, null, CloseWindowViaDispatcher) { }

    /// <summary>
    /// 测试友好的构造：允许注入自定义的"关闭窗口"回调。
    /// </summary>
    public SettingsViewModel(ISettingsService settingsService, Action? closeWindow)
        : this(settingsService, new NullModelListerFactory(), null, null, closeWindow) { }

    /// <summary>
    /// 完整构造（测试/生产都用这个）。v0.10: 加 ISkillMarket 注入（可空，向后兼容）。
    /// </summary>
    public SettingsViewModel(
        ISettingsService settingsService,
        IModelListerFactory modelListerFactory,
        ISkillService? skillService,
        ISkillMarket? skillMarket,
        Action? closeWindow)
    {
        _settingsService = settingsService;
        _modelListerFactory = modelListerFactory;
        _skillService = skillService;
        _skillMarket = skillMarket;
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
        Theme = current.Theme;

        // 初始化可用模型列表（先静态兜底）
        LoadModelsForProvider(Provider);

        // 初始化技能列表（v0.9）
        LoadSkills();
        if (_skillService != null) _skillService.SkillsChanged += (_, _) => LoadSkills();
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

    // ===== 主题（v0.8）=====
    [ObservableProperty] private AppTheme _theme = AppTheme.Light;

    partial void OnThemeChanged(AppTheme value)
    {
        // 即时应用主题，不需重启
        ThemeManager.ApplyTheme(value);
    }

    // ===== 技能（v0.9）=====
    /// <summary>所有技能（设置窗口技能管理页数据源）。</summary>
    public ObservableCollection<SkillRow> Skills { get; } = new();

    /// <summary>是否启用技能管理（无 ISkillService 时隐藏整张卡片）。</summary>
    public bool HasSkillService => _skillService != null;

    // ===== v0.10: 技能市场 =====
    /// <summary>市场索引（拉取后的全量市场技能，用于浏览 + 安装）。</summary>
    public ObservableCollection<MarketSkillRow> MarketSkills { get; } = new();

    /// <summary>分类下拉选项（"全部" + 市场实际分类去重）。</summary>
    public ObservableCollection<string> MarketCategories { get; } = new();

    /// <summary>是否启用技能市场（无 ISkillMarket 时隐藏整张卡片）。</summary>
    public bool HasSkillMarket => _skillMarket != null;

    [ObservableProperty] private string _marketCategory = "全部";
    [ObservableProperty] private string _marketSearch = string.Empty;
    [ObservableProperty] private string _marketStatus = "点击右侧 🔄 按钮拉取市场最新技能";
    [ObservableProperty] private bool _isMarketLoading;
    [ObservableProperty] private bool _isMarketError;

    partial void OnMarketCategoryChanged(string value) => ApplyMarketFilter();
    partial void OnMarketSearchChanged(string value) => ApplyMarketFilter();

    private List<MarketSkillRow> _allMarketSkills = new();

    /// <summary>应用筛选（分类 + 搜索）。</summary>
    private void ApplyMarketFilter()
    {
        MarketSkills.Clear();
        IEnumerable<MarketSkillRow> q = _allMarketSkills;
        if (!string.IsNullOrWhiteSpace(MarketCategory) && MarketCategory != "全部")
            q = q.Where(s => string.Equals(s.Category, MarketCategory, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(MarketSearch))
        {
            var kw = MarketSearch.Trim();
            q = q.Where(s =>
                s.Id.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                s.Author.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }
        foreach (var s in q) MarketSkills.Add(s);
    }

    [RelayCommand]
    private async Task BrowseMarketAsync()
    {
        if (_skillMarket == null) return;
        if (IsMarketLoading) return;

        IsMarketLoading = true;
        IsMarketError = false;
        MarketStatus = "🔄 正在拉取市场索引...";
        try
        {
            var index = await _skillMarket.FetchIndexAsync().ConfigureAwait(true);
            _allMarketSkills = index.Skills
                .Select(m => MarketSkillRow.FromManifest(m, _skillService))
                .ToList();

            // 刷新分类
            MarketCategories.Clear();
            MarketCategories.Add("全部");
            foreach (var c in _allMarketSkills.Select(s => s.Category).Distinct().OrderBy(c => c))
                MarketCategories.Add(c);
            MarketCategory = "全部";

            ApplyMarketFilter();
            MarketStatus = $"✅ 已加载 {_allMarketSkills.Count} 个市场技能";
        }
        catch (Exception ex)
        {
            IsMarketError = true;
            MarketStatus = $"❌ 拉取失败：{ex.Message}";
        }
        finally
        {
            IsMarketLoading = false;
        }
    }

    [RelayCommand]
    private async Task InstallMarketSkillAsync(MarketSkillRow? row)
    {
        if (row == null || _skillMarket == null || _skillService == null) return;

        try
        {
            var skill = await _skillMarket.FetchSkillAsync(row.Id).ConfigureAwait(true);
            await _skillService.InstallAsync(skill).ConfigureAwait(true);

            // 更新 row 状态
            row.IsInstalled = true;
            row.InstalledVersion = skill.Version;
            row.HasUpdate = false;

            MarketStatus = $"✅ 已安装技能：{row.Name} v{skill.Version}";
        }
        catch (Exception ex)
        {
            MarketStatus = $"❌ 安装失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UninstallMarketSkillAsync(MarketSkillRow? row)
    {
        if (row == null || _skillService == null) return;

        try
        {
            await _skillService.UninstallAsync(row.Id).ConfigureAwait(true);

            // 如果是市场里的技能，恢复为"未安装"
            var orig = _allMarketSkills.FirstOrDefault(s => s.Id == row.Id);
            if (orig != null) orig.IsInstalled = false;

            // 同步刷新 Skill 列表
            LoadSkills();
            MarketStatus = $"✅ 已卸载技能：{row.Name}";
        }
        catch (Exception ex)
        {
            MarketStatus = $"❌ 卸载失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CheckMarketUpdatesAsync()
    {
        if (_skillService == null) return;

        try
        {
            MarketStatus = "🔄 正在检查更新...";
            var updates = await _skillService.CheckUpdatesAsync().ConfigureAwait(true);
            int count = 0;
            foreach (var row in _allMarketSkills)
            {
                if (updates.TryGetValue(row.Id, out var info))
                {
                    row.HasUpdate = info.HasUpdate;
                    row.InstalledVersion = info.InstalledVersion;
                    row.LatestVersion = info.LatestVersion;
                    if (info.HasUpdate) count++;
                }
            }
            ApplyMarketFilter();
            MarketStatus = count > 0 ? $"🔄 发现 {count} 个技能有可用更新" : "✅ 全部已是最新";
        }
        catch (Exception ex)
        {
            MarketStatus = $"❌ 检查更新失败：{ex.Message}";
        }
    }

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
            RequireConfirmation = RequireConfirmation,
            Theme = Theme
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

    // ===== 技能（v0.9）=====
    /// <summary>从 SkillService 刷新技能列表到 UI 集合。</summary>
    private void LoadSkills()
    {
        Skills.Clear();
        if (_skillService == null) return;
        foreach (var s in _skillService.All)
        {
            Skills.Add(new SkillRow(s, _skillService));
        }
    }

    [RelayCommand]
    private async Task ToggleSkillAsync(SkillRow? row)
    {
        if (row == null || _skillService == null) return;
        await _skillService.ToggleAsync(row.Id);
        // ToggleAsync 内部已触发 SkillsChanged → LoadSkills 自动刷新
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

/// <summary>
/// v0.9: 技能行（设置窗口技能管理页用）。
/// 把 Skill 模型包成可绑定的视图模型，IsEnabled 双向绑定 + 写回 SkillService。
/// </summary>
public partial class SkillRow : ObservableObject
{
    private readonly ISkillService _svc;

    public SkillRow(Skill skill, ISkillService svc)
    {
        Id = skill.Id;
        Name = skill.Name;
        Description = skill.Description;
        Icon = skill.Icon;
        PromptTemplate = skill.PromptTemplate;
        Category = skill.Category;
        ToolsText = skill.Tools.Count == 0 ? "无依赖工具" : "工具: " + string.Join(", ", skill.Tools);
        Source = skill.IsBuiltIn ? "内置" : $"市场 · {skill.Source}";
        Version = string.IsNullOrWhiteSpace(skill.Version) ? "—" : skill.Version;
        _isEnabled = skill.IsEnabled;
        _isBuiltIn = skill.IsBuiltIn;
        _svc = svc;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Icon { get; }
    public string PromptTemplate { get; }
    public string Category { get; }
    public string ToolsText { get; }
    public string Source { get; }
    public string Version { get; }

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _isBuiltIn;

    partial void OnIsEnabledChanged(bool value)
    {
        // 写回 SkillService（fire-and-forget，错误由内部日志兜底）
        _ = _svc.ToggleAsync(Id, enable: value);
    }
}