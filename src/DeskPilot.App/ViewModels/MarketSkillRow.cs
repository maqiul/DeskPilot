using CommunityToolkit.Mvvm.ComponentModel;
using DeskPilot.Core.Models;
using DeskPilot.Core.Services;

namespace DeskPilot.App.ViewModels;

/// <summary>
/// v0.10: 市场技能卡片行（用于 SettingsWindow 技能市场页 ListView 数据源）。
/// 包含图标/名称/作者/描述/分类/版本，以及 IsInstalled/HasUpdate 状态。
/// </summary>
public partial class MarketSkillRow : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = "🧩";
    public string Category { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;

    [ObservableProperty] private bool _isInstalled;
    [ObservableProperty] private bool _hasUpdate;
    [ObservableProperty] private string _installedVersion = string.Empty;
    [ObservableProperty] private string _latestVersion = string.Empty;

    public static MarketSkillRow FromManifest(SkillManifest m, ISkillService? svc)
    {
        var isInstalled = svc?.FindById(m.Id) != null;
        var localVersion = svc?.FindById(m.Id)?.Version ?? string.Empty;
        var hasUpdate = isInstalled && !string.IsNullOrWhiteSpace(localVersion)
            && SkillMarketService.CompareVersions(localVersion, m.Version) < 0;
        return new MarketSkillRow
        {
            Id = m.Id,
            Name = m.Name,
            Description = m.Description,
            Icon = string.IsNullOrEmpty(m.Icon) ? "🧩" : m.Icon,
            Category = m.Category,
            Author = m.Author,
            Version = m.Version,
            IsInstalled = isInstalled,
            InstalledVersion = localVersion,
            LatestVersion = m.Version,
            HasUpdate = hasUpdate
        };
    }
}