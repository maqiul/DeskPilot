using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskPilot.Core.Models;
using DeskPilot.Core.Services;

namespace DeskPilot.App.ViewModels;

/// <summary>v0.15 D2: 技能中心 ViewModel 业务逻辑。承载市场 / 已安装 / 更新 3 个 Tab 的真实数据 + 命令。
/// 依赖注入：IMarketplaceSourceService（多源市场）+ ISkillService（本地技能库）。
/// D1 骨架已建，D2 补 LoadMarketAsync / LoadInstalledAsync / LoadUpdatesAsync / InstallAsync / UninstallAsync 命令。</summary>
public partial class SkillCenterViewModel : ObservableObject
{
    private readonly IMarketplaceSourceService _marketplaceSources;
    private readonly ISkillService _skillService;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private string _selectedMarketSource = "QwenPaw";

    [ObservableProperty]
    private string _marketCategory = "全部";

    [ObservableProperty]
    private string _marketSearchText = "";

    [ObservableProperty]
    private bool _isLoadingMarket;

    /// <summary>v0.15 D2: 市场源名称列表（来自 IMarketplaceSourceService.SourceNames）。</summary>
    public ObservableCollection<string> MarketplaceSourceNames { get; } = new();

    /// <summary>v0.15 D2: 分类 chips（全部 + SkillManifest.Category 常见值）。</summary>
    public ObservableCollection<string> MarketCategories { get; } = new() { "全部", "财务办公", "文件整理", "开发工具", "图片处理", "文档处理", "办公自动化", "示例" };

    /// <summary>v0.15 D2: 市场技能行集合（D3 用 MarketSkillRow 类型）。</summary>
    public ObservableCollection<MarketSkillRow> MarketSkillRows { get; } = new();

    /// <summary>v0.15 D2: 已安装技能列表（builtin + custom 合并）。</summary>
    public ObservableCollection<Skill> InstalledSkills { get; } = new();

    /// <summary>v0.15 D2: 有更新的技能列表（来自 ISkillService.CheckUpdatesAsync 过滤 HasUpdate=true）。</summary>
    public ObservableCollection<SkillUpdateInfo> UpdateAvailableSkills { get; } = new();

    public SkillCenterViewModel(IMarketplaceSourceService marketplaceSources, ISkillService skillService)
    {
        _marketplaceSources = marketplaceSources;
        _skillService = skillService;

        // 加载市场源名称
        foreach (var name in _marketplaceSources.SourceNames)
            MarketplaceSourceNames.Add(name);

        // 默认源 = 第一个
        if (MarketplaceSourceNames.Count > 0)
            SelectedMarketSource = MarketplaceSourceNames[0];

        StatusMessage = $"🛠 技能中心已打开（{MarketplaceSourceNames.Count} 个市场源 + {MarketplaceSourceNames.Count} 个分类）";

        // 订阅技能变更事件
        _skillService.SkillsChanged += (_, _) => RefreshInstalled();
    }

    /// <summary>v0.15 D2: 加载市场技能（按当前 SelectedMarketSource + MarketCategory + MarketSearchText 过滤）。</summary>
    [RelayCommand]
    public async Task LoadMarketAsync(CancellationToken ct = default)
    {
        if (IsLoadingMarket) return;
        IsLoadingMarket = true;
        StatusMessage = $"🔄 正在从 {SelectedMarketSource} 拉取技能...";
        try
        {
            var market = _marketplaceSources.GetMarket(SelectedMarketSource);
            var index = await market.FetchIndexAsync(ct).ConfigureAwait(true);

            MarketSkillRows.Clear();
            foreach (var manifest in index.Skills)
            {
                // 分类过滤
                if (MarketCategory != "全部" && manifest.Category != MarketCategory) continue;
                // 搜索过滤（Name + Description + Id 包含关键词）
                if (!string.IsNullOrWhiteSpace(MarketSearchText))
                {
                    var text = MarketSearchText.ToLowerInvariant();
                    if (!(manifest.Name?.ToLowerInvariant().Contains(text) == true
                       || manifest.Description?.ToLowerInvariant().Contains(text) == true
                       || manifest.Id?.ToLowerInvariant().Contains(text) == true))
                        continue;
                }
                MarketSkillRows.Add(MarketSkillRow.FromManifest(manifest, _skillService));
            }

            StatusMessage = $"✅ 从 {SelectedMarketSource} 拉到 {MarketSkillRows.Count} 个技能（{index.Skills.Count} 个总数）";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 加载市场失败：{ex.Message}";
        }
        finally
        {
            IsLoadingMarket = false;
        }
    }

    /// <summary>v0.15 D2: 刷新已安装列表（builtin + custom 合并，按 ID 排序）。</summary>
    [RelayCommand]
    public void LoadInstalled()
    {
        RefreshInstalled();
    }

    private void RefreshInstalled()
    {
        InstalledSkills.Clear();
        var all = _skillService.All.OrderBy(s => s.Id).ToList();
        foreach (var s in all) InstalledSkills.Add(s);
        StatusMessage = $"📦 已安装 {InstalledSkills.Count} 个技能（{_skillService.BuiltIn.Count} 内置 + {_skillService.Custom.Count} 用户）";
    }

    /// <summary>v0.15 D2: 加载有更新的技能列表（调用 ISkillService.CheckUpdatesAsync → 过滤 HasUpdate=true）。</summary>
    [RelayCommand]
    public async Task LoadUpdatesAsync(CancellationToken ct = default)
    {
        StatusMessage = "🔄 正在检查更新...";
        try
        {
            var updates = await _skillService.CheckUpdatesAsync(ct).ConfigureAwait(true);
            UpdateAvailableSkills.Clear();
            foreach (var kv in updates.Where(kv => kv.Value.HasUpdate))
                UpdateAvailableSkills.Add(kv.Value);
            StatusMessage = UpdateAvailableSkills.Count > 0
                ? $"🔄 发现 {UpdateAvailableSkills.Count} 个技能可更新"
                : "✅ 所有技能都是最新版本";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 检查更新失败：{ex.Message}";
        }
    }

    /// <summary>v0.15 D2: 安装技能（拉取完整 Skill JSON → 委托给 ISkillService.InstallAsync）。</summary>
    [RelayCommand]
    public async Task InstallAsync(string skillId, CancellationToken ct = default)
    {
        try
        {
            StatusMessage = $"📥 正在安装 {skillId}...";
            var market = _marketplaceSources.GetMarket(SelectedMarketSource);
            var skill = await market.FetchSkillAsync(skillId, ct).ConfigureAwait(true);
            await _skillService.InstallAsync(skill, ct).ConfigureAwait(true);
            StatusMessage = $"✅ 已安装 {skillId}（{skill.Version}）";
            // 不刷新市场列表（SkillsChanged 已触发 RefreshInstalled 刷新已安装 Tab）
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 安装失败：{ex.Message}";
        }
    }

    /// <summary>v0.15 D2: 卸载技能（拒绝 builtin，委托给 ISkillService.UninstallAsync）。</summary>
    [RelayCommand]
    public async Task UninstallAsync(string skillId, CancellationToken ct = default)
    {
        try
        {
            StatusMessage = $"🗑 正在卸载 {skillId}...";
            await _skillService.UninstallAsync(skillId, ct).ConfigureAwait(true);
            StatusMessage = $"✅ 已卸载 {skillId}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 卸载失败：{ex.Message}";
        }
    }

    /// <summary>v0.15 D2: 一键更新某个技能（拉市场最新 → InstallAsync 覆盖）。</summary>
    [RelayCommand]
    public async Task UpdateSkillAsync(SkillUpdateInfo info, CancellationToken ct = default)
    {
        if (info == null || !info.HasUpdate) return;
        try
        {
            StatusMessage = $"🔄 正在更新 {info.Id} ({info.InstalledVersion} → {info.LatestVersion})...";
            var market = _marketplaceSources.GetMarket(SelectedMarketSource);
            var skill = await market.FetchSkillAsync(info.Id, ct).ConfigureAwait(true);
            await _skillService.InstallAsync(skill, ct).ConfigureAwait(true);
            StatusMessage = $"✅ 已更新 {info.Id} → {info.LatestVersion}";
            await LoadUpdatesAsync(ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 更新失败：{ex.Message}";
        }
    }
}