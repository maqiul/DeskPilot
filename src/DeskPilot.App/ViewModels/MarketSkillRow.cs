using CommunityToolkit.Mvvm.ComponentModel;
using DeskPilot.Core.Models;
using DeskPilot.Core.Services;

namespace DeskPilot.App.ViewModels;

/// <summary>
/// v0.11: 市场技能卡片行（用于 SettingsWindow 技能市场页 WrapPanel 卡片网格）。
/// 卡片显示：Icon + Name + Description(3 行截断) + SourceName 徽章。
/// 详情弹窗显示：Author + Version + Rating + Downloads + ScreenshotUrl + Prompt + Tools。
/// </summary>
public partial class MarketSkillRow : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = "🧩";
    public string Category { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public string AuthorUrl { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;

    // v0.11: 来源徽章（卡片右上角）+ 详情弹窗用字段
    public string SourceName { get; init; } = string.Empty;
    public string ScreenshotUrl { get; init; } = string.Empty;
    public double Rating { get; init; }
    public int Downloads { get; init; }

    [ObservableProperty] private bool _isInstalled;
    [ObservableProperty] private bool _hasUpdate;
    [ObservableProperty] private string _installedVersion = string.Empty;
    [ObservableProperty] private string _latestVersion = string.Empty;

    public static MarketSkillRow FromManifest(SkillManifest m, ISkillService? svc)
    {
        var local = svc?.FindById(m.Id);
        var isInstalled = local != null;
        var localVersion = local?.Version ?? string.Empty;
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
            AuthorName = string.IsNullOrEmpty(m.AuthorName) ? m.Author : m.AuthorName,
            AuthorUrl = m.AuthorUrl,
            Version = m.Version,
            SourceName = string.IsNullOrEmpty(m.Author) ? "QwenPaw" : m.Author,
            ScreenshotUrl = m.ScreenshotUrl,
            Rating = m.Rating,
            Downloads = m.Downloads,
            IsInstalled = isInstalled,
            InstalledVersion = localVersion,
            LatestVersion = m.Version,
            HasUpdate = hasUpdate
        };
    }
}