using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.Core.Models;

namespace DeskPilot.Core.Services;

/// <summary>
/// 技能服务：管理本地技能库（内置 + 市场安装）。
/// 数据源：嵌入式默认技能 JSON（builtin）+ 用户文件（%AppData%/DeskPilot/skills.json，存所有已安装技能）。
/// v0.10: 支持从市场安装 / 卸载 / 检查更新。
/// </summary>
public interface ISkillService
{
    /// <summary>当前所有技能（默认 + 用户安装/修改 已合并）。</summary>
    IReadOnlyList<Skill> All { get; }

    /// <summary>当前所有启用的技能。</summary>
    IReadOnlyList<Skill> Enabled { get; }

    /// <summary>v0.10: 内置技能（不可卸载，IsBuiltIn=true）。</summary>
    IReadOnlyList<Skill> BuiltIn { get; }

    /// <summary>v0.10: 用户安装的技能（可卸载，IsBuiltIn=false）。</summary>
    IReadOnlyList<Skill> Custom { get; }

    /// <summary>技能变更事件（加载/切换启用/安装/卸载后触发）。</summary>
    event System.EventHandler? SkillsChanged;

    /// <summary>从默认 JSON + 用户文件加载（首次启动会创建用户文件）。</summary>
    Task LoadAsync(CancellationToken ct = default);

    /// <summary>切换技能启用状态并立即保存。null = 切换当前状态。</summary>
    Task ToggleAsync(string skillId, bool? enable = null, CancellationToken ct = default);

    /// <summary>v0.10: 安装技能（从市场拉取 → 写本地 → 触发 SkillsChanged）。</summary>
    /// <param name="skill">从 SkillMarketService.FetchSkillAsync 拉到的技能。</param>
    Task InstallAsync(Skill skill, CancellationToken ct = default);

    /// <summary>v0.10: 卸载技能（拒绝内置技能，抛 InvalidOperationException）。</summary>
    Task UninstallAsync(string skillId, CancellationToken ct = default);

    /// <summary>v0.10: 检查已安装技能是否有更新。需要 ISkillMarket 注入。</summary>
    Task<IReadOnlyDictionary<string, SkillUpdateInfo>> CheckUpdatesAsync(CancellationToken ct = default);

    /// <summary>按 ID 查找技能。找不到返回 null。</summary>
    Skill? FindById(string id);
}
