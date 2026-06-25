using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.Core.Models;

namespace DeskPilot.Core.Services;

/// <summary>
/// 技能服务：加载/保存用户技能状态（启用/禁用）、提供启用技能列表。
/// 数据源：嵌入式默认技能 JSON + 用户文件（%AppData%/DeskPilot/skills.json）。
/// </summary>
public interface ISkillService
{
    /// <summary>当前所有技能（默认 + 用户禁用状态已合并）。</summary>
    IReadOnlyList<Skill> All { get; }

    /// <summary>当前所有启用的技能。</summary>
    IReadOnlyList<Skill> Enabled { get; }

    /// <summary>技能变更事件（加载/切换启用后触发）。</summary>
    event System.EventHandler? SkillsChanged;

    /// <summary>从默认 JSON + 用户文件加载（首次启动会创建用户文件）。</summary>
    Task LoadAsync(CancellationToken ct = default);

    /// <summary>切换技能启用状态并立即保存。null = 切换当前状态。</summary>
    Task ToggleAsync(string skillId, bool? enable = null, CancellationToken ct = default);

    /// <summary>按 ID 查找技能。找不到返回 null。</summary>
    Skill? FindById(string id);
}
