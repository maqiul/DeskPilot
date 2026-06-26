using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.Core.Models;

namespace DeskPilot.Core.Services;

/// <summary>
/// v0.10: 技能市场服务。
/// 数据源：GitHub 仓库根的 skills/ 目录（公开仓库，零成本）。
/// - 索引：skills/README.md（YAML 头 + Markdown 表格）
/// - 技能：skills/{id}.json
/// </summary>
public interface ISkillMarket
{
    /// <summary>市场源 URL（如 raw.githubusercontent.com/maqiul/DeskPilot/main/skills）。</summary>
    string BaseUrl { get; }

    /// <summary>拉取技能索引（解析 README.md）。失败抛 MarketFetchException。</summary>
    Task<SkillIndex> FetchIndexAsync(CancellationToken ct = default);

    /// <summary>拉取单个技能完整 JSON（不命中抛 SkillNotFoundException）。</summary>
    Task<Skill> FetchSkillAsync(string id, CancellationToken ct = default);

    /// <summary>检查已安装技能是否有更新（对比本地 Version 与市场清单 Version）。</summary>
    /// <returns>id → (本地版本, 市场版本, 是否可更新)</returns>
    Task<IReadOnlyDictionary<string, SkillUpdateInfo>> CheckUpdatesAsync(
        IEnumerable<Skill> installed, CancellationToken ct = default);
}

/// <summary>v0.10: 单个技能更新信息。</summary>
public sealed record SkillUpdateInfo(
    string Id,
    string InstalledVersion,
    string LatestVersion,
    bool HasUpdate);

/// <summary>市场拉取失败异常。</summary>
public sealed class MarketFetchException : System.Exception
{
    public MarketFetchException(string message) : base(message) { }
    public MarketFetchException(string message, System.Exception inner) : base(message, inner) { }
}

/// <summary>技能在市场索引中找不到。</summary>
public sealed class SkillNotFoundException : System.Exception
{
    public string SkillId { get; }
    public SkillNotFoundException(string id) : base($"技能 '{id}' 不在市场中")
        => SkillId = id;
}
