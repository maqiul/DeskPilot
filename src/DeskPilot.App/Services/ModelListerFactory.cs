using System;
using System.Net.Http;
using DeskPilot.App.Models;
using DeskPilot.App.ViewModels;
using DeskPilot.Core.Services;
using Microsoft.Extensions.Logging;

namespace DeskPilot.App.Services;

/// <summary>
/// 路由 AiProvider 到具体 IModelLister 的工厂。
/// 由 DI 容器注入，所有 lister 共享同一个 IHttpClientFactory。
/// </summary>
public sealed class ModelListerFactory : IModelListerFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModelListerFactory>? _logger;

    public ModelListerFactory(IHttpClientFactory httpClientFactory, ILogger<ModelListerFactory>? logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public IModelLister GetLister(AiProvider provider) => provider switch
    {
        AiProvider.OpenAI   => new OpenAIModelLister(_httpClientFactory, _logger as ILogger),
        AiProvider.DeepSeek => new DeepSeekModelLister(_httpClientFactory, _logger as ILogger),
        AiProvider.Ollama   => new OllamaModelLister(_httpClientFactory, _logger as ILogger),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "未知的 AI Provider")
    };
}