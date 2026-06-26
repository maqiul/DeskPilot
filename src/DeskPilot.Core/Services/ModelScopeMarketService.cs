using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.Core.Models;

namespace DeskPilot.Core.Services;

/// <summary>v0.12 A1.1: ModelScope 真后端市场服务。
/// 内部组合一个 SkillMarketService 走 ModelScope BaseUrl（maqiul 名下独立公开 GitHub 仓库 DeskPilot-modelscope 的 skills 目录）。
/// ModelScope 官方无 skill 索引 API，本服务用 mock 仓库 + 相同 README 表格格式提供"可用"技能。
/// 与 QwenPaw/ClawHub 源完全隔离，可独立更新。
/// 后续可替换为 ModelScope 官方 API（若有）或保持 mock 仓库。</summary>
public sealed class ModelScopeMarketService : ISkillMarket
{
    public const string DefaultBaseUrl = "https://raw.githubusercontent.com/maqiul/DeskPilot-modelscope/main/skills";

    public string BaseUrl { get; }
    public string SourceName => MarketplaceSourceService.ModelScopeName;

    private readonly SkillMarketService _inner;

    public ModelScopeMarketService(HttpClient http, string baseUrl = DefaultBaseUrl)
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
