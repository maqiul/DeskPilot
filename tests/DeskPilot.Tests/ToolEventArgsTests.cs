using DeskPilot.Core.Services;

namespace DeskPilot.Tests;

/// <summary>
/// ToolEventArgs 数据结构测试。
/// 注意：ToolCallObserver（private nested）通过 SemanticKernelChatService
/// 的 ToolInvoking/ToolInvoked 事件间接覆盖（集成测试层）。
/// </summary>
public sealed class ToolEventArgsTests
{
    [Fact]
    public void ToolEventArgs_StoresAllFields()
    {
        var args = new ToolEventArgs("archive_by_date", true, 123, "test detail");
        Assert.Equal("archive_by_date", args.ToolName);
        Assert.True(args.Success);
        Assert.Equal(123, args.ElapsedMs);
        Assert.Equal("test detail", args.Detail);
    }

    [Fact]
    public void ToolEventArgs_DetailIsOptional()
    {
        var args = new ToolEventArgs("move_files", false, 0);
        Assert.Null(args.Detail);
    }

    [Fact]
    public void ToolEventArgs_ElapsedMsCanBeZero()
    {
        // ToolInvoking 事件触发时 elapsed=0
        var args = new ToolEventArgs("find_duplicates", true, 0);
        Assert.Equal(0, args.ElapsedMs);
    }
}