using System.Collections.Generic;
using DeskPilot.Core.Models;

namespace DeskPilot.App.Models;

/// <summary>
/// UI 层用的模型选项（VM 直接绑定）。把 Core 的 ModelInfo 拍平为可绑定字段。
/// </summary>
public sealed class ModelOption
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsLocal { get; init; }
    public string OwnedBy { get; init; } = string.Empty;

    public static ModelOption FromCore(ModelInfo m) => new()
    {
        Id = m.Id,
        DisplayName = m.EffectiveDisplayName,
        IsLocal = m.IsLocal,
        OwnedBy = m.OwnedBy ?? string.Empty
    };

    public static IReadOnlyList<ModelOption> FromCoreMany(IEnumerable<ModelInfo> models)
    {
        var list = new List<ModelOption>();
        foreach (var m in models) list.Add(FromCore(m));
        return list;
    }
}