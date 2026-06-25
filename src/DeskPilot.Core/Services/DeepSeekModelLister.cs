using System.Text.Json;
using DeskPilot.Core.Models;
using Microsoft.Extensions.Logging;

namespace DeskPilot.Core.Services;

/// <summary>
/// DeepSeek 模型列表提供者。
/// Endpoint: GET https://api.deepseek.com/models
/// 响应格式: { "data": [ { "id": "deepseek-chat", "object": "model", "owned_by": "deepseek" }, ... ] }
/// （DeepSeek API 兼容 OpenAI 格式）
/// </summary>
public sealed class DeepSeekModelLister : HttpModelListerBase
{
    private const string DefaultEndpoint = "https://api.deepseek.com/models";

    public DeepSeekModelLister(IHttpClientFactory httpClientFactory, ILogger? logger = null)
        : base(httpClientFactory, logger) { }

    public override string ProviderName => "deepseek";

    protected override string BuildUrl(string? apiKey, string? endpoint)
        => string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.TrimEnd('/') + "/models";

    protected override IReadOnlyList<ModelInfo> ParseModels(JsonElement root)
    {
        // DeepSeek 文档说响应是数组而非 {data: [...]}，两种都兼容
        JsonElement arrayEl = root;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d))
            arrayEl = d;
        if (arrayEl.ValueKind != JsonValueKind.Array)
            return Array.Empty<ModelInfo>();

        var list = new List<ModelInfo>();
        foreach (var item in arrayEl.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idEl)) continue;
            var id = idEl.GetString();
            if (string.IsNullOrWhiteSpace(id)) continue;

            var ownedBy = item.TryGetProperty("owned_by", out var ownEl) ? ownEl.GetString() : "deepseek";
            list.Add(new ModelInfo(id, OwnedBy: ownedBy));
        }
        return list;
    }
}