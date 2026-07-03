using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.Core.Services;

namespace DeskPilot.Server;

/// <summary>
/// v0.0.2 MVP 占位聊天服务。
///
/// 不接 AI provider，仅回显用户消息 + 标记 Sidecar 链路跑通。
/// v0.0.3 替换为 SemanticKernelChatService（复用现有 SK + 18 工具）。
/// </summary>
public sealed class StubChatService : IChatService
{
    public async Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        // 模拟一点延迟，方便观察 Tauri 流式响应
        await Task.Delay(100, cancellationToken);

        var echo = string.IsNullOrWhiteSpace(userMessage)
            ? "（空消息）"
            : $"DeskPilot v2 Sidecar 收到：{userMessage}（v0.0.7 stub，v0.0.8 接 SK + 工具路由）";

        return echo;
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // MVP 阶段流式输出也走 stub：按字符切片返回，方便观察打字机效果
        var reply = await ChatAsync(userMessage, cancellationToken);
        foreach (var ch in reply)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(15, cancellationToken);
            yield return ch.ToString();
        }
    }

    public void Dispose() { /* 无资源 */ }
}