using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using DeskPilot.Core.Models;
using Microsoft.Extensions.Logging;

namespace DeskPilot.Core.Services;

/// <summary>
/// 基于 HTTP 的模型列表提供者基类。
/// 处理鉴权头、JSON 解析、错误吞咽的通用逻辑。
/// </summary>
public abstract class HttpModelListerBase : IModelLister
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger? _logger;

    protected HttpModelListerBase(IHttpClientFactory httpClientFactory, ILogger? logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public abstract string ProviderName { get; }

    /// <summary>
    /// 子类实现：构造请求 URL。
    /// </summary>
    protected abstract string BuildUrl(string? apiKey, string? endpoint);

    /// <summary>
    /// 子类实现：设置请求头（鉴权等）。
    /// </summary>
    protected virtual void ConfigureHeaders(HttpRequestMessage request, string? apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <summary>
    /// 子类实现：从 JSON 解析出模型列表。
    /// </summary>
    protected abstract IReadOnlyList<ModelInfo> ParseModels(JsonElement root);

    public async Task<IReadOnlyList<ModelInfo>> ListAsync(
        string? apiKey = null,
        string? endpoint = null,
        CancellationToken ct = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient(ProviderName);
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(apiKey, endpoint));
            ConfigureHeaders(request, apiKey);

            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return ParseModels(doc.RootElement.Clone());
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[{Provider}] 拉取模型列表失败", ProviderName);
            return Array.Empty<ModelInfo>();
        }
    }
}