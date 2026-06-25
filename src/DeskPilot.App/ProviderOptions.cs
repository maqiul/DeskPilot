using DeskPilot.App.Models;
using System.Collections.Generic;

namespace DeskPilot.App;

/// <summary>
/// AI Provider 下拉框数据源。
/// </summary>
public static class ProviderOptions
{
    public static IReadOnlyList<AiProvider> ProviderList { get; } = new[]
    {
        AiProvider.OpenAI,
        AiProvider.DeepSeek,
        AiProvider.Ollama
    };
}