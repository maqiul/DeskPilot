using DeskPilot.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// SemanticKernelChatService 的单元测试。
/// 用 Stub 实现代替 Mock（因为 IChatCompletionService 的方法是扩展方法，Moq 无法处理）。
/// </summary>
public class ChatServiceTests
{
    [Fact]
    public async Task ChatAsync_AddsUserAndAssistantMessagesToHistory()
    {
        // Arrange
        var stub = new StubChatCompletionService("Mock AI Reply");
        var kernel = Kernel.CreateBuilder()
            .Services
            .AddSingleton<IChatCompletionService>(stub)
            .BuildKernelViaServices();
        var service = new SemanticKernelChatService(kernel);

        // Act
        var reply = await service.ChatAsync("Hello AI");

        // Assert
        Assert.Equal("Mock AI Reply", reply);
        Assert.NotNull(stub.LastHistory);
        Assert.Equal(3, stub.LastHistory!.Count); // system + user + assistant
        Assert.Equal("Hello AI", stub.LastHistory[1].Content);
        Assert.Equal("Mock AI Reply", stub.LastHistory[2].Content);
    }

    [Fact]
    public async Task ChatAsync_PreservesHistoryAcrossCalls()
    {
        // Arrange
        var stub = new StubChatCompletionService("Default Reply");
        var kernel = Kernel.CreateBuilder()
            .Services
            .AddSingleton<IChatCompletionService>(stub)
            .BuildKernelViaServices();
        var service = new SemanticKernelChatService(kernel);

        // Act
        await service.ChatAsync("First");
        await service.ChatAsync("Second");
        var reply = await service.ChatAsync("Third");

        // Assert
        Assert.Equal("Default Reply", reply);
        Assert.NotNull(stub.LastHistory);
        // 第三次调用时，history: system + (u1+a1) + (u2+a2) + u3 = 7
        Assert.Equal(7, stub.LastHistory!.Count);
        Assert.Equal("First", stub.LastHistory[1].Content);
        Assert.Equal("Default Reply", stub.LastHistory[2].Content);
        Assert.Equal("Second", stub.LastHistory[3].Content);
        Assert.Equal("Default Reply", stub.LastHistory[4].Content);
        Assert.Equal("Third", stub.LastHistory[5].Content);
    }

    [Fact]
    public async Task ChatAsync_PropagatesCancellation()
    {
        // Arrange
        var stub = new StubChatCompletionService();
        var kernel = Kernel.CreateBuilder()
            .Services
            .AddSingleton<IChatCompletionService>(stub)
            .BuildKernelViaServices();
        var service = new SemanticKernelChatService(kernel);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.ChatAsync("test", cts.Token));
    }

    /// <summary>
    /// 极简的 IChatCompletionService Stub。
    /// 实现 GetChatMessageContentsAsync（扩展方法 GetChatMessageContentAsync 底层调的就是它）。
    /// </summary>
    private class StubChatCompletionService : IChatCompletionService
    {
        private readonly string _reply;

        public StubChatCompletionService(string reply = "Default Reply")
        {
            _reply = reply;
        }

        public ChatHistory? LastHistory { get; private set; }
        public int CallCount { get; private set; }
        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        async Task<IReadOnlyList<ChatMessageContent>> IChatCompletionService.GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings,
            Kernel? kernel,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastHistory = chatHistory;

            // 模拟一点点延迟
            await Task.Delay(1, cancellationToken);

            return new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, _reply)
            };
        }
    }
}

/// <summary>
/// 把 IServiceCollection 改写成 Kernel 的扩展（KernelBuilder.Services 已经是 IServiceCollection 了）。
/// </summary>
internal static class TestKernelExtensions
{
    public static Kernel BuildKernelViaServices(this IServiceCollection services)
    {
        // Kernel.CreateBuilder() 返回 KernelBuilder，其 .Services 就是 IServiceCollection
        var builder = Kernel.CreateBuilder();
        foreach (var svc in services)
        {
            if (svc.ServiceType == typeof(IChatCompletionService))
            {
                builder.Services.AddSingleton(svc.ServiceType, svc.ImplementationInstance!);
            }
        }
        return builder.Build();
    }
}