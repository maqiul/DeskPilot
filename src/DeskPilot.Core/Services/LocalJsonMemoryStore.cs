using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeskPilot.Core.Services;

/// <summary>
/// 本地 JSON 文件记忆存储。
/// 
/// - 文件路径：%AppData%/DeskPilot/memory.json
/// - 最大保留 100 条消息，超出裁剪最旧的
/// - 自动创建目录
/// </summary>
public sealed class LocalJsonMemoryStore : IMemoryStore
{
    private const int MaxEntries = 100;
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false, // 省空间
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LocalJsonMemoryStore(string? customPath = null)
    {
        _filePath = customPath ?? GetDefaultPath();
    }

    private static string GetDefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "DeskPilot");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "memory.json");
    }

    public Task<List<MemoryEntry>> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
            return Task.FromResult(new List<MemoryEntry>());

        try
        {
            var json = File.ReadAllText(_filePath);
            var entries = JsonSerializer.Deserialize<List<MemoryEntry>>(json, JsonOptions);
            return Task.FromResult(entries ?? new List<MemoryEntry>());
        }
        catch (Exception ex)
        {
            // 文件损坏 → 备份旧文件 + 新建空记忆
            var backup = _filePath + ".corrupted." + DateTime.Now.ToString("yyyyMMddHHmmss");
            try { File.Move(_filePath, backup); } catch { /* 尽力而为 */ }
            System.Diagnostics.Debug.WriteLine($"[Memory] 文件损坏，已备份到 {backup}: {ex.Message}");
            return Task.FromResult(new List<MemoryEntry>());
        }
    }

    public async Task SaveAsync(List<MemoryEntry> entries, CancellationToken ct = default)
    {
        // 裁剪最旧的消息
        if (entries.Count > MaxEntries)
            entries = entries.GetRange(entries.Count - MaxEntries, MaxEntries);

        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(entries, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, ct).ConfigureAwait(false);
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
        return Task.CompletedTask;
    }
}
