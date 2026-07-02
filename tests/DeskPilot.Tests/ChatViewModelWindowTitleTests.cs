using DeskPilot.App.ViewModels;
using DeskPilot.Core.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.20.0: ChatViewModel.WindowTitle 测试
/// </summary>
public class ChatViewModelWindowTitleTests
{
    [Fact]
    public void WindowTitle_ContainsVersion()
    {
        // v0.20.0: WindowTitle 必须包含 DeskPilot 标识 + 版本号
        var vm = new ChatViewModel(new StubChatService());
        Assert.StartsWith("DeskPilot", vm.WindowTitle);
        Assert.Contains("v", vm.WindowTitle);
    }

    [Fact]
    public void WindowTitle_MatchesAssemblyVersion()
    {
        // v0.20.0: WindowTitle 版本号必须匹配 Assembly 版本
        var vm = new ChatViewModel(new StubChatService());
        var asmVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;
        Assert.Contains($"{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}", vm.WindowTitle);
    }

    /// <summary>测试用 StubChatService（来自 xUnit 测试夹具）</summary>
    private sealed class StubChatService : IChatService
    {
        public Task<string> ChatAsync(string userMessage, System.Threading.CancellationToken ct = default) =>
            Task.FromResult("stub");
        public IAsyncEnumerable<string> ChatStreamAsync(string userMessage, System.Threading.CancellationToken ct = default) =>
            EmptyStream();

        public void Dispose() { /* stub - 无资源 */ }

        private async IAsyncEnumerable<string> EmptyStream()
        {
            await System.Threading.Tasks.Task.Yield();
            yield break;
        }
    }
}