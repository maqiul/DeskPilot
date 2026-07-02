using DeskPilot.App.ViewModels;
using DeskPilot.Core.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.27.0: ChatMessage 删除按钮（DeleteMessageCommand）测试
/// </summary>
public class ChatMessageDeleteTests
{
    [Fact]
    public void DeleteMessageCommand_ExistsAndIsRelayCommand()
    {
        // v0.27.0: DeleteMessageCommand 必须由 [RelayCommand] 生成
        var vm = new ChatViewModel(new StubChatService());
        Assert.NotNull(vm.DeleteMessageCommand);
    }

    [Fact]
    public void DeleteMessageCommand_NullMessage_DoesNotThrow()
    {
        // v0.27.0: null 参数必须静默处理
        var vm = new ChatViewModel(new StubChatService());
        vm.Messages.Add(new ChatMessage("user", "hello"));
        var ex = Record.Exception(() => vm.DeleteMessageCommand.Execute(null));
        Assert.Null(ex);
        Assert.Single(vm.Messages);
    }

    [Fact]
    public void DeleteMessageCommand_ValidMessage_RemovesFromCollection()
    {
        // v0.27.0: 有效消息必须从集合中删除
        var vm = new ChatViewModel(new StubChatService());
        var msg1 = new ChatMessage("user", "first");
        var msg2 = new ChatMessage("assistant", "second");
        var msg3 = new ChatMessage("user", "third");
        vm.Messages.Add(msg1);
        vm.Messages.Add(msg2);
        vm.Messages.Add(msg3);
        vm.DeleteMessageCommand.Execute(msg2);
        Assert.Equal(2, vm.Messages.Count);
        Assert.Same(msg1, vm.Messages[0]);
        Assert.Same(msg3, vm.Messages[1]);
    }

    [Fact]
    public void DeleteMessageCommand_NotInCollection_DoesNotThrow()
    {
        // v0.27.0: 集合外的消息对象不抛异常（只静默失败）
        var vm = new ChatViewModel(new StubChatService());
        vm.Messages.Add(new ChatMessage("user", "hello"));
        var orphan = new ChatMessage("user", "orphan");
        var ex = Record.Exception(() => vm.DeleteMessageCommand.Execute(orphan));
        Assert.Null(ex);
        Assert.Single(vm.Messages);
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