using System.Threading;
using System.Threading.Tasks;

namespace DeskPilot.Core.Services;

/// <summary>
/// 聊天服务接口。提供与 AI 模型对话的统一抽象。
/// </summary>
public interface IChatService
{
    /// <summary>
    /// 发送用户消息，返回 AI 回复。
    /// </summary>
    /// <param name="userMessage">用户输入的消息内容。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>AI 的回复文本。</returns>
    Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default);
}