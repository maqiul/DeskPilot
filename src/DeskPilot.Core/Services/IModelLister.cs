using DeskPilot.Core.Models;

namespace DeskPilot.Core.Services;

/// <summary>
/// 模型列表提供者。从具体 AI Provider 拉取可用模型。
/// </summary>
public interface IModelLister
{
    /// <summary>
    /// 平台标识（"openai" / "deepseek" / "ollama"）。
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 拉取可用模型列表。网络/API 错误时返回空列表（不抛异常）。
    /// </summary>
    /// <param name="apiKey">API Key（Ollama 可为 null/空）</param>
    /// <param name="endpoint">自定义 endpoint（Ollama 用，OpenAI/DeepSeek 可选）</param>
    /// <param name="ct">取消令牌</param>
    Task<IReadOnlyList<ModelInfo>> ListAsync(
        string? apiKey = null,
        string? endpoint = null,
        CancellationToken ct = default);
}