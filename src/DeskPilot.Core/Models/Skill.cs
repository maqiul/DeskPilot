using System.Text.Json.Serialization;

namespace DeskPilot.Core.Models;

/// <summary>
/// 技能：预设的 prompt 模板 + 可选工具声明 + 一键触发。
/// 用户点击后，PromptTemplate 会被填入聊天输入框并自动发送，
/// AI 收到 prompt 后会按 Tools 中声明的工具组合完成任务。
/// v0.10: 增加 IsBuiltIn/IsInstalled/Source/Version 支持技能市场。
/// </summary>
/// <param name="Id">唯一 ID（如 "organize-downloads"）</param>
/// <param name="Name">显示名（如 "整理下载文件夹"）</param>
/// <param name="Description">简短说明（鼠标悬浮时显示）</param>
/// <param name="Icon">Emoji 图标（单字符，如 "📁"）</param>
/// <param name="PromptTemplate">触发后填入输入框的 prompt 文本</param>
/// <param name="Tools">依赖的工具名列表（用于 AI 上下文，非强制约束）</param>
/// <param name="Category">分类（"文件整理" / "图片处理" / "..."），用于 UI 分组</param>
/// <param name="IsEnabled">用户是否启用（默认 true）</param>
/// <param name="IsBuiltIn">v0.10: 是否内置技能（内置不可删除）</param>
/// <param name="Source">v0.10: 技能来源（"builtin" / "market:{author}"）</param>
/// <param name="Version">v0.10: 技能版本（market 字段，builtin 留空）</param>
public sealed record Skill(
    string Id,
    string Name,
    string Description,
    string Icon,
    string PromptTemplate,
    IReadOnlyList<string> Tools,
    string Category = "通用",
    bool IsEnabled = true,
    bool IsBuiltIn = false,
    string Source = "",
    string Version = "")
{
    /// <summary>v0.10: 是否有可用更新（UI 角标）。运行时由 ChatViewModel 写入，不参与 JSON 序列化。</summary>
    [JsonIgnore]
    public bool HasUpdate { get; set; }
}

/// <summary>
/// 技能集合（含内置默认技能）。可序列化到 JSON。
/// </summary>
public sealed class SkillSet
{
    /// <summary>所有技能（已合并用户禁用状态）</summary>
    public List<Skill> Skills { get; set; } = new();

    /// <summary>获取所有启用的技能</summary>
    public IEnumerable<Skill> Enabled => Skills.Where(s => s.IsEnabled);

    /// <summary>v0.10: 内置技能（不可删除）</summary>
    public IEnumerable<Skill> BuiltIn => Skills.Where(s => s.IsBuiltIn);

    /// <summary>v0.10: 自定义/市场安装的技能（可删除）</summary>
    public IEnumerable<Skill> Custom => Skills.Where(s => !s.IsBuiltIn);

    /// <summary>按分类分组（分类名 → 该分类下的技能）</summary>
    public IEnumerable<IGrouping<string, Skill>> GroupedByCategory =>
        Enabled.GroupBy(s => s.Category);
}
