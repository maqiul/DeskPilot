using System.Collections.Concurrent;
using System.Text.Json;

namespace DeskPilot.Server;

/// <summary>
/// v0.1.1: Tool 调用历史记录。
///
/// 设计：
/// - 内存维护最近 100 条 Tool 调用环形队列
/// - 按需持久化到 %LOCALAPPDATA%\DeskPilot\tool-history.json
/// - 单例注入到 /api/tools/execute 端点，每个调用记录 1 条
/// - 给前端 GET /api/tools/history 提供数据
/// </summary>
public sealed class ToolHistoryEntry
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string ToolName { get; init; } = string.Empty;
    public string ArgsJson { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}

public sealed class ToolHistoryStore
{
    private const int MaxEntries = 100;
    private readonly ConcurrentQueue<ToolHistoryEntry> _entries = new();
    private readonly string _persistPath;
    private readonly object _fileLock = new();

    public ToolHistoryStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "DeskPilot");
        Directory.CreateDirectory(dir);
        _persistPath = Path.Combine(dir, "tool-history.json");
        LoadFromDisk();
    }

    public void Add(ToolHistoryEntry entry)
    {
        _entries.Enqueue(entry);
        // 保持环形：超出 MaxEntries 则出队
        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _)) { }
        PersistToDisk();
    }

    public IReadOnlyList<ToolHistoryEntry> List(int limit = 50)
    {
        return _entries.Take(limit).ToList();
    }

    /// <summary>
    /// v0.1.4: 分页查询 - 只返回 Timestamp 早于 <paramref name="before"/> 的记录。
    /// 注意：_entries 是 ConcurrentQueue，按入队顺序 = 按时间升序遍历。
    /// </summary>
    public IReadOnlyList<ToolHistoryEntry> ListBefore(DateTime before, int limit = 50)
    {
        return _entries
            .Where(e => e.Timestamp < before)
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToList();
    }

    private void PersistToDisk()
    {
        try
        {
            lock (_fileLock)
            {
                var json = JsonSerializer.Serialize(_entries.ToList(), new JsonSerializerOptions
                {
                    WriteIndented = false
                });
                File.WriteAllText(_persistPath, json);
            }
        }
        catch
        {
            // 持久化失败不致命（内存还有）
        }
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_persistPath)) return;
        try
        {
            var json = File.ReadAllText(_persistPath);
            var entries = JsonSerializer.Deserialize<List<ToolHistoryEntry>>(json);
            if (entries == null) return;
            foreach (var e in entries.OrderByDescending(e => e.Timestamp).Take(MaxEntries))
            {
                _entries.Enqueue(e);
            }
        }
        catch
        {
            // 加载失败 = 文件坏了，清空重启
        }
    }
}
