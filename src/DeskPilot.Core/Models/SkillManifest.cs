using System.Collections.Generic;

namespace DeskPilot.Core.Models;

/// <summary>
/// v0.10: 技能市场索引条目（GitHub skills/README.md 维护）。
/// 不含 PromptTemplate 全文（按需 FetchSkillAsync 拉取），
/// 节省索引加载流量。
/// </summary>
/// <param name="Id">唯一 ID（如 "organize-downloads"）</param>
/// <param name="Name">显示名</param>
/// <param name="Description">简短说明</param>
/// <param name="Icon">Emoji 图标</param>
/// <param name="Category">分类（用于 UI 分组/筛选）</param>
/// <param name="Author">作者署名（如 "maqiul" / "community"）</param>
/// <param name="Version">语义化版本（如 "1.0.0"）</param>
/// <param name="Tags">标签（用于搜索）</param>
public sealed record SkillManifest(
    string Id,
    string Name,
    string Description,
    string Icon,
    string Category,
    string Author,
    string Version,
    IReadOnlyList<string> Tags);

/// <summary>
/// 市场索引（多技能条目集合）。
/// </summary>
public sealed class SkillIndex
{
    public List<SkillManifest> Skills { get; set; } = new();

    /// <summary>按分类去重。</summary>
    public IEnumerable<string> Categories => Skills.Select(s => s.Category).Distinct().OrderBy(c => c);

    /// <summary>按 ID 查找。</summary>
    public SkillManifest? FindById(string id) => Skills.FirstOrDefault(s => s.Id == id);
}
