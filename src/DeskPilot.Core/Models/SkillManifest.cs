using System.Collections.Generic;
using System.Linq;

namespace DeskPilot.Core.Models;

/// <summary>
/// v0.11: 技能市场索引条目（QwenPaw / ClawHub / ModelScope 等多源）。
/// 不含 PromptTemplate 全文（按需 FetchSkillAsync 拉取），
/// 节省索引加载流量。
/// </summary>
/// <param name="Id">唯一 ID（如 "organize-downloads"）</param>
/// <param name="Name">显示名</param>
/// <param name="Description">简短说明（卡片显示用，建议 ≤80 字）</param>
/// <param name="Icon">Emoji 图标</param>
/// <param name="Category">分类（用于 UI 分组/筛选）</param>
/// <param name="Author">作者署名（如 "maqiul" / "community"）</param>
/// <param name="Version">语义化版本（如 "1.0.0"）</param>
/// <param name="Tags">标签（用于搜索）</param>
/// <param name="ScreenshotUrl">v0.11: 截图缩略图 URL（详情弹窗用大图）</param>
/// <param name="Rating">v0.11: 评分 0-5（来自市场统计）</param>
/// <param name="Downloads">v0.11: 下载/安装次数（来自市场统计）</param>
/// <param name="AuthorUrl">v0.11: 作者主页 URL</param>
/// <param name="AuthorName">v0.11: 作者显示名（区别于短 ID）</param>
public sealed record SkillManifest(
    string Id,
    string Name,
    string Description,
    string Icon,
    string Category,
    string Author,
    string Version,
    IReadOnlyList<string> Tags,
    string ScreenshotUrl = "",
    double Rating = 0,
    int Downloads = 0,
    string AuthorUrl = "",
    string AuthorName = "");

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