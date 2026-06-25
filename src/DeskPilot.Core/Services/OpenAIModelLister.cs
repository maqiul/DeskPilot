using System.Text.Json;
using DeskPilot.Core.Models;
using Microsoft.Extensions.Logging;

namespace DeskPilot.Core.Services;

/// <summary>
/// OpenAI 模型列表提供者。
/// Endpoint: GET https://api.openai.com/v1/models
/// 响应格式: { "data": [ { "id": "gpt-4o", "owned_by": "openai" }, ... ] }
/// </summary>
public sealed class OpenAIModelLister : HttpModelListerBase
{
    private const string DefaultEndpoint = "https://api.openai.com/v1/models";

    public OpenAIModelLister(IHttpClientFactory httpClientFactory, ILogger? logger = null)
        : base(httpClientFactory, logger) { }

    public override string ProviderName => "openai";

    protected override string BuildUrl(string? apiKey, string? endpoint)
        => string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.TrimEnd('/') + "/models";

    protected override IReadOnlyList<ModelInfo> ParseModels(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return Array.Empty<ModelInfo>();

        var list = new List<ModelInfo>();
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idEl)) continue;
            var id = idEl.GetString();
            if (string.IsNullOrWhiteSpace(id)) continue;

            var ownedBy = item.TryGetProperty("owned_by", out var ownEl) ? ownEl.GetString() : null;
            list.Add(new ModelInfo(id, OwnedBy: ownedBy));
        }
        return list;
    }
}