using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeskPilot.Core.Services;

/// <summary>
/// 聊天记忆存储抽象。支持持久化 + 裁剪，让 AI 跨会话记住上下文。
/// </summary>
public interface IMemoryStore
{
    /// <summary>加载所有历史消息（按时间排序）。</summary>
    Task<List<MemoryEntry>> LoadAsync(CancellationToken ct = default);

    /// <summary>全量保存消息（通常先裁剪再保存）。</summary>
    Task SaveAsync(List<MemoryEntry> entries, CancellationToken ct = default);

    /// <summary>清空记忆。</summary>
    Task ClearAsync(CancellationToken ct = default);
}

/// <summary>
/// 单条记忆条目。与 SK 的 AuthorRole 对齐。
/// </summary>
public sealed class MemoryEntry
{
    /// <summary>system / user / assistant / tool</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>消息正文。</summary>
    public string Content { get; set; } = string.Empty;

    public MemoryEntry() { }

    public MemoryEntry(string role, string content)
    {
        Role = role;
        Content = content;
    }
}
