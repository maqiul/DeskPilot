using DeskPilot.App.ViewModels;
using DeskPilot.Core.Services;
using System.IO;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.22.0: ChatViewModel.ExportToMarkdownCommand 测试
/// </summary>
public class ExportToMarkdownTests
{
    [Fact]
    public void ExportToMarkdown_NullPath_DoesNothing()
    {
        // v0.22.0: 空路径不抛异常
        var vm = new ChatViewModel(new StubChatService());
        vm.Messages.Add(new ChatMessage("user", "hello"));
        var ex = Record.Exception(() => vm.ExportToMarkdownCommand.Execute(null));
        Assert.Null(ex);
    }

    [Fact]
    public void ExportToMarkdown_EmptyPath_DoesNothing()
    {
        // v0.22.0: 空字符串路径不抛异常
        var vm = new ChatViewModel(new StubChatService());
        var ex = Record.Exception(() => vm.ExportToMarkdownCommand.Execute(""));
        Assert.Null(ex);
    }

    [Fact]
    public void ExportToMarkdown_WritesFile_WithMarkdownContent()
    {
        // v0.22.0: 导出后文件包含 Markdown 标题 + 消息内容
        var vm = new ChatViewModel(new StubChatService());
        vm.Messages.Add(new ChatMessage("user", "你好"));
        vm.Messages.Add(new ChatMessage("assistant", "你好！有什么可以帮你的？"));

        var tempPath = Path.Combine(Path.GetTempPath(), $"deskpilot-test-{Guid.NewGuid()}.md");
        try
        {
            vm.ExportToMarkdownCommand.Execute(tempPath);
            Assert.True(File.Exists(tempPath));

            var content = File.ReadAllText(tempPath);
            Assert.Contains("# DeskPilot 对话记录", content);
            Assert.Contains("👤 用户", content);
            Assert.Contains("你好", content);
            Assert.Contains("🤖 AI", content);
            Assert.Contains("有什么可以帮你的？", content);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ExportToMarkdown_NonexistentDirectory_SetsErrorStatus()
    {
        // v0.22.0: 写到不存在的目录失败时 ToolStatus 必须包含错误信息
        var vm = new ChatViewModel(new StubChatService());
        var badPath = @"Z:\nonexistent-deskpilot-test\foo.md";
        vm.ExportToMarkdownCommand.Execute(badPath);
        Assert.StartsWith("❌", vm.ToolStatus);
    }

    /// <summary>测试用 StubChatService（实现 IChatService + Dispose）</summary>
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