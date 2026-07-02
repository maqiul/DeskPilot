using DeskPilot.App.ViewModels;
using DeskPilot.Core.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.26.0: ChatMessage 复制按钮（CopyMessageCommand）测试
/// </summary>
public class ChatMessageCopyTests
{
    [Fact]
    public void CopyMessageCommand_ExistsAndIsRelayCommand()
    {
        // v0.26.0: CopyMessageCommand 必须由 [RelayCommand] 生成
        var vm = new ChatViewModel(new StubChatService());
        Assert.NotNull(vm.CopyMessageCommand);
    }

    [Fact]
    public void CopyMessageCommand_NullMessage_DoesNotThrow()
    {
        // v0.26.0: null 参数必须静默处理
        var vm = new ChatViewModel(new StubChatService());
        var ex = Record.Exception(() => vm.CopyMessageCommand.Execute(null));
        Assert.Null(ex);
    }

    [Fact]
    public void CopyMessageCommand_EmptyContent_DoesNotThrow()
    {
        // v0.26.0: 空内容消息不抛异常
        var vm = new ChatViewModel(new StubChatService());
        var msg = new ChatMessage("user", "");
        var ex = Record.Exception(() => vm.CopyMessageCommand.Execute(msg));
        Assert.Null(ex);
    }

    /// <summary>测试用 StubChatService</summary>
    private sealed class StubChatService : IChatService
    {
        public Task<string> ChatAsync(string userMessage, System.Threading.CancellationToken ct = default) =>
            Task.FromResult("stub");
        public IAsyncEnumerable<string> ChatStreamAsync(string userMessage, System.Threading.CancellationToken ct = default) =>
            EmptyStream();

        public void Dispose() { }

        private async IAsyncEnumerable<string> EmptyStream()
        {
            await System.Threading.Tasks.Task.Yield();
            yield break;
        }
    }
}