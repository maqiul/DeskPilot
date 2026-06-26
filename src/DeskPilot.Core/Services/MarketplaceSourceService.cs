using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.Core.Models;

namespace DeskPilot.Core.Services;

/// <summary>
/// v0.11: 多市场源服务。
/// 持 QwenPaw / ClawHub / ModelScope 三个 ISkillMarket 实例，
/// SettingsViewModel 切换 Tab 时调 GetMarket(name).FetchIndexAsync。
/// 当前实现：
///   - QwenPaw  → 自家 GitHub raw（DeskPilot/skills）— 真源
///   - ClawHub / ModelScope → stub 返回内置示例数据（先不接真后端，v0.12 再做）
/// </summary>
public interface IMarketplaceSourceService
{
    /// <summary>所有可用市场源名（"QwenPaw"/"ClawHub"/"ModelScope"）。</summary>
    IReadOnlyList<string> SourceNames { get; }

    /// <summary>按名取市场服务。</summary>
    ISkillMarket GetMarket(string sourceName);

    /// <summary>默认源（"QwenPaw"）。</summary>
    ISkillMarket DefaultMarket { get; }
}

public sealed class MarketplaceSourceService : IMarketplaceSourceService
{
    public const string QwenPawName = "QwenPaw";
    public const string ClawHubName = "ClawHub";
    public const string ModelScopeName = "ModelScope";

    private readonly Dictionary<string, ISkillMarket> _markets;

    public MarketplaceSourceService(IHttpClientFactory httpFactory)
    {
        _markets = new Dictionary<string, ISkillMarket>
        {
            [QwenPawName] = new SkillMarketService(
                httpFactory.CreateClient("skill-market"),
                "https://raw.githubusercontent.com/maqiul/DeskPilot/main/skills",
                QwenPawName),
            [ClawHubName] = new StubMarketService(
                "https://clawhub.example.com/skills",
                ClawHubName,
                ClawHubName),
            [ModelScopeName] = new StubMarketService(
                "https://modelscope.example.com/skills",
                ModelScopeName,
                ModelScopeName),
        };
    }

    public IReadOnlyList<string> SourceNames => _markets.Keys.ToList();

    public ISkillMarket GetMarket(string sourceName)
        => _markets.TryGetValue(sourceName, out var m)
            ? m
            : throw new MarketFetchException($"未知市场源：{sourceName}");

    public ISkillMarket DefaultMarket => _markets[QwenPawName];
}

/// <summary>v0.11: 占位市场服务（ClawHub / ModelScope 暂未接真后端）。</summary>
internal sealed class StubMarketService : ISkillMarket
{
    public string BaseUrl { get; }
    public string SourceName { get; }
    private readonly string _displayName;

    public StubMarketService(string baseUrl, string sourceName, string displayName)
    {
        BaseUrl = baseUrl;
        SourceName = sourceName;
        _displayName = displayName;
    }

    public Task<SkillIndex> FetchIndexAsync(CancellationToken ct = default)
        => Task.FromResult(new SkillIndex
        {
            Skills =
            {
                new SkillManifest(
                    Id: $"{_displayName.ToLower()}-demo-1",
                    Name: $"{_displayName} 示例技能",
                    Description: $"这是 {_displayName} 市场占位示例，v0.12 接真后端后会替换。",
                    Icon: "🧪",
                    Category: "示例",
                    Author: "stub",
                    Version: "0.1.0",
                    Tags: new[] { "demo", "stub" },
                    ScreenshotUrl: "",
                    Rating: 0,
                    Downloads: 0),
            },
        });

    public Task<Skill> FetchSkillAsync(string id, CancellationToken ct = default)
        => throw new SkillNotFoundException(id);

    public Task<IReadOnlyDictionary<string, SkillUpdateInfo>> CheckUpdatesAsync(
        IEnumerable<Skill> installed, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, SkillUpdateInfo>>(
            new Dictionary<string, SkillUpdateInfo>());
}