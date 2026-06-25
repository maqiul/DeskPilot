using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DeskPilot.Core.Services;

/// <summary>
/// 聊天服务接口。提供与 AI 模型对话的统一抽象。
/// </summary>
public interface IChatService : System.IDisposable
{
    /// <summary>
    /// 发送用户消息，返回完整 AI 回复（一次性）。
    /// </summary>
    Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式对话：逐 token 返回 AI 回复。
    /// UI 层逐片追加到消息气泡，实现"打字机"效果。
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(string userMessage, CancellationToken cancellationToken = default);
}