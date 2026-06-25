using DeskPilot.Core.Models;

namespace DeskPilot.Core.Services;

/// <summary>
/// 静态模型目录：作为 fallback（网络失败/首次启动时使用）。
/// 维护简单列表，用户可手动输入未列出的模型 ID。
/// </summary>
public static class AiModelCatalog
{
    /// <summary>
    /// 取得指定 Provider 的 fallback 模型列表。
    /// </summary>
    public static IReadOnlyList<ModelInfo> FallbackFor(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "openai" => OpenAiFallback,
            "deepseek" => DeepSeekFallback,
            "ollama" => Array.Empty<ModelInfo>(), // 本地模型只能动态拉
            _ => Array.Empty<ModelInfo>()
        };
    }

    /// <summary>
    /// 合并 fallback + 动态拉取的列表（去重，动态优先）。
    /// </summary>
    public static IReadOnlyList<ModelInfo> MergeWithFallback(
        string providerName,
        IReadOnlyList<ModelInfo> live)
    {
        var fallback = FallbackFor(providerName);
        if (live.Count == 0) return fallback;

        // 动态列表在前（用户最近用过的），fallback 在后（兜底）
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<ModelInfo>();
        foreach (var m in live)
        {
            if (seen.Add(m.Id)) merged.Add(m);
        }
        foreach (var m in fallback)
        {
            if (seen.Add(m.Id)) merged.Add(m);
        }
        return merged;
    }

    private static readonly IReadOnlyList<ModelInfo> OpenAiFallback = new[]
    {
        new ModelInfo("gpt-4o",        "GPT-4o",       "openai"),
        new ModelInfo("gpt-4o-mini",   "GPT-4o Mini",  "openai"),
        new ModelInfo("gpt-4-turbo",   "GPT-4 Turbo",  "openai"),
        new ModelInfo("gpt-3.5-turbo", "GPT-3.5 Turbo","openai"),
        new ModelInfo("o1-preview",    "o1 Preview",   "openai"),
        new ModelInfo("o1-mini",       "o1 Mini",      "openai"),
    };

    private static readonly IReadOnlyList<ModelInfo> DeepSeekFallback = new[]
    {
        new ModelInfo("deepseek-chat",     "DeepSeek Chat",      "deepseek"),
        new ModelInfo("deepseek-reasoner", "DeepSeek Reasoner",  "deepseek"),
        new ModelInfo("deepseek-coder",    "DeepSeek Coder",     "deepseek"),
    };
}