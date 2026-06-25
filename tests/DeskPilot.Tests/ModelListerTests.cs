using System.Net;
using System.Text.Json;
using DeskPilot.Core.Models;
using DeskPilot.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// Stub HttpClientFactory：返回预设的 HttpMessageHandler，可模拟任意响应。
/// </summary>
internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;
    public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _client = new HttpClient(new StubHandler(responder));
    }
    public HttpClient CreateClient(string name) => _client;

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}

/// <summary>
/// OpenAIModelLister 单元测试。
/// </summary>
public class OpenAIModelListerTests
{
    [Fact]
    public async Task ListAsync_ParsesOpenAiResponse()
    {
        var factory = new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                @object = "list",
                data = new[]
                {
                    new { id = "gpt-4o", @object = "model", owned_by = "openai" },
                    new { id = "gpt-4o-mini", @object = "model", owned_by = "openai" },
                    new { id = "gpt-3.5-turbo", @object = "model", owned_by = "openai" }
                }
            }))
        });

        var lister = new OpenAIModelLister(factory, NullLogger.Instance);
        var models = await lister.ListAsync("sk-test");

        Assert.Equal(3, models.Count);
        Assert.Contains(models, m => m.Id == "gpt-4o" && m.OwnedBy == "openai");
    }

    [Fact]
    public async Task ListAsync_SendsBearerHeader()
    {
        HttpRequestMessage? captured = null;
        var factory = new StubHttpClientFactory(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[]}") };
        });

        var lister = new OpenAIModelLister(factory);
        await lister.ListAsync("sk-abc123");

        Assert.NotNull(captured);
        Assert.NotNull(captured!.Headers.Authorization);
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal("sk-abc123", captured.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task ListAsync_ReturnsEmpty_OnHttpError()
    {
        var factory = new StubHttpClientFactory(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") });

        var lister = new OpenAIModelLister(factory);
        var models = await lister.ListAsync("sk-bad");

        Assert.Empty(models);
    }

    [Fact]
    public async Task ListAsync_UsesCustomEndpoint_WhenProvided()
    {
        Uri? capturedUri = null;
        var factory = new StubHttpClientFactory(req =>
        {
            capturedUri = req.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[]}") };
        });

        var lister = new OpenAIModelLister(factory);
        await lister.ListAsync(apiKey: null, endpoint: "https://proxy.example.com/v1");

        Assert.NotNull(capturedUri);
        Assert.Equal("https://proxy.example.com/v1/models", capturedUri!.ToString());
    }
}

/// <summary>
/// DeepSeekModelLister 单元测试。
/// </summary>
public class DeepSeekModelListerTests
{
    [Fact]
    public async Task ListAsync_ParsesDeepSeekResponse_WithDataWrapper()
    {
        var factory = new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                @object = "list",
                data = new[]
                {
                    new { id = "deepseek-chat", @object = "model", owned_by = "deepseek" },
                    new { id = "deepseek-reasoner", @object = "model", owned_by = "deepseek" }
                }
            }))
        });

        var lister = new DeepSeekModelLister(factory);
        var models = await lister.ListAsync("sk-ds");

        Assert.Equal(2, models.Count);
        Assert.Contains(models, m => m.Id == "deepseek-reasoner");
    }

    [Fact]
    public async Task ListAsync_ParsesDeepSeekResponse_BareArray()
    {
        // DeepSeek 早期 API 直接返回数组
        var factory = new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new[]
            {
                new { id = "deepseek-chat", @object = "model" }
            }))
        });

        var lister = new DeepSeekModelLister(factory);
        var models = await lister.ListAsync("sk-ds");

        Assert.Single(models);
        Assert.Equal("deepseek-chat", models[0].Id);
    }
}

/// <summary>
/// OllamaModelLister 单元测试。
/// </summary>
public class OllamaModelListerTests
{
    [Fact]
    public async Task ListAsync_ParsesOllamaTagsResponse()
    {
        var factory = new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                models = new[]
                {
                    new { name = "llama3.1:8b",   size = 4_500_000_000L, details = new { family = "llama" } },
                    new { name = "qwen2.5:7b",    size = 4_100_000_000L, details = new { family = "qwen" } }
                }
            }))
        });

        var lister = new OllamaModelLister(factory);
        var models = await lister.ListAsync();

        Assert.Equal(2, models.Count);
        Assert.All(models, m => Assert.True(m.IsLocal));
        Assert.Contains(models, m => m.Id == "llama3.1:8b");
        Assert.Contains(models, m => m.DisplayName!.Contains("GB"));
    }

    [Fact]
    public async Task ListAsync_ReturnsEmpty_WhenServiceDown()
    {
        var factory = new StubHttpClientFactory(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var lister = new OllamaModelLister(factory);
        var models = await lister.ListAsync(endpoint: "http://localhost:11434");

        Assert.Empty(models);
    }
}

/// <summary>
/// AiModelCatalog 静态 fallback 测试。
/// </summary>
public class AiModelCatalogTests
{
    [Theory]
    [InlineData("openai", 6)]    // gpt-4o, gpt-4o-mini, gpt-4-turbo, gpt-3.5-turbo, o1-preview, o1-mini
    [InlineData("deepseek", 3)]
    [InlineData("OpenAI", 6)]    // 大小写不敏感
    [InlineData("DEEPSEEK", 3)]
    [InlineData("ollama", 0)]    // 本地无 fallback
    [InlineData("unknown", 0)]
    public void FallbackFor_ReturnsExpectedCount(string provider, int expectedCount)
    {
        var models = AiModelCatalog.FallbackFor(provider);
        Assert.Equal(expectedCount, models.Count);
    }

    [Fact]
    public void MergeWithFallback_EmptyLive_ReturnsFallback()
    {
        var merged = AiModelCatalog.MergeWithFallback("openai", Array.Empty<ModelInfo>());
        Assert.Equal(6, merged.Count); // 全是 fallback
    }

    [Fact]
    public void MergeWithFallback_Deduplicates()
    {
        var live = new[]
        {
            new ModelInfo("gpt-4o", "GPT-4o"),
            new ModelInfo("claude-3.5-sonnet", "Claude 3.5") // 不在 fallback
        };

        var merged = AiModelCatalog.MergeWithFallback("openai", live);

        // gpt-4o 出现一次（live 优先），claude-3.5-sonnet 是新增，剩下 5 个 fallback
        Assert.Equal(7, merged.Count);
        Assert.Equal("gpt-4o", merged[0].Id);
        Assert.Equal("claude-3.5-sonnet", merged[1].Id);
    }
}