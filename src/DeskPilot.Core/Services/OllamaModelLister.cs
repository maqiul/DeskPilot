using System.Text.Json;
using DeskPilot.Core.Models;
using Microsoft.Extensions.Logging;

namespace DeskPilot.Core.Services;

/// <summary>
/// Ollama 模型列表提供者（本地服务，无鉴权）。
/// Endpoint: GET http://localhost:11434/api/tags
/// 响应格式: { "models": [ { "name": "llama3.1:8b", "size": ..., "details": {...} }, ... ] }
/// </summary>
public sealed class OllamaModelLister : IModelLister
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger? _logger;

    public OllamaModelLister(IHttpClientFactory httpClientFactory, ILogger? logger = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderName => "ollama";

    public async Task<IReadOnlyList<ModelInfo>> ListAsync(
        string? apiKey = null,
        string? endpoint = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = (string.IsNullOrWhiteSpace(endpoint) ? "http://localhost:11434" : endpoint.TrimEnd('/')) + "/api/tags";

            using var client = _httpClientFactory.CreateClient(ProviderName);
            using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
                return Array.Empty<ModelInfo>();

            var list = new List<ModelInfo>();
            foreach (var item in models.EnumerateArray())
            {
                if (!item.TryGetProperty("name", out var nameEl)) continue;
                var name = nameEl.GetString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                long? size = item.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out var s) ? s : null;
                var display = size.HasValue ? $"{name} ({size.Value / (1024 * 1024 * 1024.0):0.#} GB)" : name;
                list.Add(new ModelInfo(name, display, IsLocal: true));
            }
            return list;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[ollama] 拉取本地模型列表失败");
            return Array.Empty<ModelInfo>();
        }
    }
}