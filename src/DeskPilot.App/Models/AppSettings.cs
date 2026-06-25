using System.Text.Json.Serialization;

namespace DeskPilot.App.Models;

/// <summary>
/// AI 提供商类型。
/// </summary>
public enum AiProvider
{
    OpenAI,
    DeepSeek,
    Ollama
}

/// <summary>
/// 应用设置数据模型。序列化为 JSON 后整体加密存储。
/// </summary>
public class AppSettings
{
    /// <summary>
    /// 当前选中的 AI Provider。
    /// </summary>
    public AiProvider Provider { get; set; } = AiProvider.OpenAI;

    // ----- OpenAI -----
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string OpenAiModel { get; set; } = "gpt-4o-mini";

    // ----- DeepSeek -----
    public string DeepSeekApiKey { get; set; } = string.Empty;
    public string DeepSeekModel { get; set; } = "deepseek-chat";

    // ----- Ollama -----
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "qwen2.5:7b";

    /// <summary>
    /// 缓存的模型列表（按 Provider 存）。离线时使用。
    /// key 为 AiProvider 枚举名（如 "OpenAI" / "DeepSeek" / "Ollama"）。
    /// </summary>
    public Dictionary<string, List<string>> CachedModels { get; set; } = new();

    [JsonIgnore]
    public static AppSettings Default => new();
}