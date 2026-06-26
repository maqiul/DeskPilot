using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.Core.Models;

namespace DeskPilot.Core.Services;

/// <summary>v0.12 A1.1: ClawHub 真后端市场服务。
/// 内部组合一个 SkillMarketService 走 ClawHub BaseUrl（maqiul 名下独立公开 GitHub 仓库 DeskPilot-clawhub 的 skills 目录）。
/// 与 QwenPaw/ModelScope 源完全隔离，可独立更新。
/// 后续可替换为 openclaw/clawhub 官方 GraphQL API 或 ClawHub 公开 REST API。</summary>
public sealed class ClawHubMarketService : ISkillMarket
{
    public const string DefaultBaseUrl = "https://raw.githubusercontent.com/maqiul/DeskPilot-clawhub/main/skills";

    public string BaseUrl { get; }
    public string SourceName => MarketplaceSourceService.ClawHubName;

    private readonly SkillMarketService _inner;

    public ClawHubMarketService(HttpClient http, string baseUrl = DefaultBaseUrl)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _inner = new SkillMarketService(http, BaseUrl, SourceName);
    }

    public Task<SkillIndex> FetchIndexAsync(CancellationToken ct = default)
        => _inner.FetchIndexAsync(ct);

    public async Task<Skill?> FetchSkillAsync(string id, CancellationToken ct = default)
        => await _inner.FetchSkillAsync(id, ct).ConfigureAwait(false);

    public Task<IReadOnlyDictionary<string, SkillUpdateInfo>> CheckUpdatesAsync(IEnumerable<Skill> installed, CancellationToken ct = default)
        => _inner.CheckUpdatesAsync(installed, ct);
}
