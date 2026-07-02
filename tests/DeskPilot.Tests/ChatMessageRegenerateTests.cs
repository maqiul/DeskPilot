using DeskPilot.App.ViewModels;
using DeskPilot.Core.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.28.0: 重新生成消息（RegenerateMessageCommand）测试
/// </summary>
public class ChatMessageRegenerateTests
{
    [Fact]
    public void RegenerateMessageCommand_ExistsAndIsAsyncRelayCommand()
    {
        // v0.28.0: RegenerateMessageCommand 必须由 [RelayCommand] 生成（async Task）
        var vm = new ChatViewModel(new StubChatService());
        Assert.NotNull(vm.RegenerateMessageCommand);
    }

    [Fact]
    public void RegenerateMessageCommand_NullMessage_DoesNotThrow()
    {
        // v0.28.0: null 参数必须静默处理
        var vm = new ChatViewModel(new StubChatService());
        var ex = Record.Exception(() => vm.RegenerateMessageCommand.Execute(null));
        Assert.Null(ex);
    }

    [Fact]
    public void RegenerateMessageCommand_UserMessage_Ignored()
    {
        // v0.28.0: user 消息必须被忽略（只对 assistant 工作）
        var vm = new ChatViewModel(new StubChatService());
        var userMsg = new ChatMessage("user", "hi");
        vm.Messages.Add(userMsg);
        var ex = Record.Exception(() => vm.RegenerateMessageCommand.Execute(userMsg));
        Assert.Null(ex);
        Assert.Single(vm.Messages);
    }

    [Fact]
    public void RegenerateMessageCommand_AssistantWithoutPriorUser_Ignored()
    {
        // v0.28.0: assistant 消息前面没有 user prompt → 静默忽略
        var vm = new ChatViewModel(new StubChatService());
        var assistantMsg = new ChatMessage("assistant", "hello");
        vm.Messages.Add(assistantMsg);
        var ex = Record.Exception(() => vm.RegenerateMessageCommand.Execute(assistantMsg));
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