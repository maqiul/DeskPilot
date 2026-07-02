using DeskPilot.App.ViewModels;
using DeskPilot.Core.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.24.0: ChatViewModel.SearchKeyword + FilteredMessages 测试
/// </summary>
public class ChatSearchFilterTests
{
    [Fact]
    public void FilteredMessages_EmptyKeyword_ReturnsAll()
    {
        // v0.24.0: 关键词为空时返回所有消息
        var vm = new ChatViewModel(new StubChatService());
        vm.Messages.Add(new ChatMessage("user", "hello"));
        vm.Messages.Add(new ChatMessage("assistant", "hi"));
        Assert.Equal(2, vm.FilteredMessages.Count);
    }

    [Fact]
    public void FilteredMessages_Keyword_FiltersCaseInsensitive()
    {
        // v0.24.0: 关键词过滤必须大小写不敏感
        var vm = new ChatViewModel(new StubChatService());
        vm.Messages.Add(new ChatMessage("user", "Hello World"));
        vm.Messages.Add(new ChatMessage("assistant", "你好"));
        vm.SearchKeyword = "hello";
        Assert.Single(vm.FilteredMessages);
        Assert.Equal("Hello World", vm.FilteredMessages[0].Content);
    }

    [Fact]
    public void FilteredMessages_ChineseKeyword_Filters()
    {
        // v0.24.0: 中文关键词过滤
        var vm = new ChatViewModel(new StubChatService());
        vm.Messages.Add(new ChatMessage("user", "今天天气如何"));
        vm.Messages.Add(new ChatMessage("assistant", "晴天"));
        vm.SearchKeyword = "天气";
        Assert.Single(vm.FilteredMessages);
    }

    [Fact]
    public void FilteredMessages_NoMatch_ReturnsEmpty()
    {
        // v0.24.0: 无匹配时返回空列表
        var vm = new ChatViewModel(new StubChatService());
        vm.Messages.Add(new ChatMessage("user", "hello"));
        vm.SearchKeyword = "nonexistent";
        Assert.Empty(vm.FilteredMessages);
    }

    [Fact]
    public void MatchCountText_EmptyKeyword_ReturnsEmpty()
    {
        // v0.24.0: 关键词为空时统计文本为空字符串
        var vm = new ChatViewModel(new StubChatService());
        Assert.Equal(string.Empty, vm.MatchCountText);
    }

    [Fact]
    public void MatchCountText_WithKeyword_ShowsRatio()
    {
        // v0.24.0: 关键词非空时显示 "matched/total" 格式
        var vm = new ChatViewModel(new StubChatService());
        vm.Messages.Add(new ChatMessage("user", "hello"));
        vm.Messages.Add(new ChatMessage("user", "world"));
        vm.Messages.Add(new ChatMessage("assistant", "hello there"));
        vm.SearchKeyword = "hello";
        Assert.Equal("2 / 3", vm.MatchCountText);
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