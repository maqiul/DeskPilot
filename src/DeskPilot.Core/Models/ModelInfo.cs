namespace DeskPilot.Core.Models;

/// <summary>
/// AI 模型信息。
/// </summary>
/// <param name="Id">模型 ID（用于 API 调用）</param>
/// <param name="DisplayName">显示名（用于 UI）</param>
/// <param name="OwnedBy">模型所有者（"openai" / "deepseek" / 用户名 / null）</param>
/// <param name="IsLocal">是否本地模型（Ollama 标记）</param>
public sealed record ModelInfo(
    string Id,
    string? DisplayName = null,
    string? OwnedBy = null,
    bool IsLocal = false)
{
    /// <summary>供 UI 展示的友好名称（DisplayName 优先，否则用 Id）</summary>
    public string EffectiveDisplayName => DisplayName ?? Id;
}