using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeskPilot.Core.Services;

/// <summary>
/// JSON 文件记忆存储。保存在 %AppData%/DeskPilot/memory.json。
///
/// 特性：
/// - 启动时加载完整历史
/// - 每次对话后自动保存（fire-and-forget，不阻塞 UI）
/// - 最多保留 100 条（裁剪最早的消息）
/// - 文件损坏时自动备份为 .bak + 重建
/// </summary>
public sealed class LocalJsonMemoryStore : IMemoryStore
{
    private static readonly string StoreDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DeskPilot");

    private static readonly string StorePath = Path.Combine(StoreDir, "memory.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>最多保留的消息条数。</summary>
    public int MaxEntries { get; set; } = 100;

    public async Task<List<MemoryEntry>> LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(StorePath))
                return new List<MemoryEntry>();

            var json = await File.ReadAllTextAsync(StorePath, ct);
            var entries = JsonSerializer.Deserialize<List<MemoryEntry>>(json, JsonOptions)
                          ?? new List<MemoryEntry>();
            return entries;
        }
        catch (JsonException)
        {
            // 文件损坏 → 备份并重建
            var bak = StorePath + ".bak";
            try { File.Copy(StorePath, bak, overwrite: true); }
            catch { /* 备份失败不阻塞 */ }
            return new List<MemoryEntry>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(List<MemoryEntry> entries, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // 裁剪：只保留最近 MaxEntries 条
            if (entries.Count > MaxEntries)
                entries = entries.GetRange(entries.Count - MaxEntries, MaxEntries);

            Directory.CreateDirectory(StoreDir);
            var json = JsonSerializer.Serialize(entries, JsonOptions);
            await File.WriteAllTextAsync(StorePath, json, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (File.Exists(StorePath))
                File.Delete(StorePath);
        }
        finally
        {
            _lock.Release();
        }
    }
}
