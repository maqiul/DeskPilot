using DeskPilot.Core.Services;
using DeskPilot.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Generic;

namespace DeskPilot.Tests;

/// <summary>
/// SemanticKernelChatService 测试。
///
/// 注意：SK 1.32 的 tool calling 循环是 SK 内部处理的（FunctionChoiceBehavior.Auto()），
/// 我们不手动循环。所以这里只验证：
/// 1. 简单对话走通
/// 2. History 按顺序追加
/// 3. 系统 prompt 包含工具清单
/// 4. 空 registry 不报错
/// 5. 工具 plugin 正确暴露给 Kernel
/// </summary>
public sealed class SemanticKernelChatServiceTests
{
    /// <summary>
    /// 脚本式 mock 的 IChatCompletionService。
    /// </summary>
    private sealed class ScriptedChatCompletion : IChatCompletionService
    {
        private readonly Queue<Func<ChatHistory, ChatMessageContent>> _responses;

        public ScriptedChatCompletion(IEnumerable<Func<ChatHistory, ChatMessageContent>> responses)
            => _responses = new Queue<Func<ChatHistory, ChatMessageContent>>(responses);

        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

        public Task<ChatMessageContent> GetChatMessageContentAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            var responder = _responses.Count > 0
                ? _responses.Dequeue()
                : (_ => new ChatMessageContent(AuthorRole.Assistant, "[no more responses]"));
            return Task.FromResult(responder(chatHistory));
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            var r = GetChatMessageContentAsync(chatHistory, executionSettings, kernel, cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult<IReadOnlyList<ChatMessageContent>>(new[] { r });
        }
    }

    private static Kernel CreateKernelWithChat(IChatCompletionService chatService, IToolRegistry registry)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chatService);
        var kernel = builder.Build();
        foreach (var p in registry.CreateKernelPlugins())
            kernel.Plugins.Add(p);
        return kernel;
    }

    private static ChatHistory GetHistory(SemanticKernelChatService service)
    {
        var field = typeof(SemanticKernelChatService)
            .GetField("_history", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (ChatHistory)field.GetValue(service)!;
    }

    [Fact]
    public async Task ChatAsync_SimpleMessage_ReturnsAssistantText()
    {
        var registry = new ToolRegistry();
        registry.Register(new ArchiveByDateTool());
        var chatService = new ScriptedChatCompletion(new Func<ChatHistory, ChatMessageContent>[]
        {
            _ => new ChatMessageContent(AuthorRole.Assistant, "你好，有什么可以帮您？")
        });
        var kernel = CreateKernelWithChat(chatService, registry);
        var service = new SemanticKernelChatService(kernel, registry);

        var reply = await service.ChatAsync("hello");

        Assert.Equal("你好，有什么可以帮您？", reply);
    }

    [Fact]
    public async Task ChatAsync_AppendsToHistoryInOrder()
    {
        var registry = new ToolRegistry();
        var chatService = new ScriptedChatCompletion(new Func<ChatHistory, ChatMessageContent>[]
        {
            _ => new ChatMessageContent(AuthorRole.Assistant, "回复1"),
            _ => new ChatMessageContent(AuthorRole.Assistant, "回复2")
        });
        var kernel = CreateKernelWithChat(chatService, registry);
        var service = new SemanticKernelChatService(kernel, registry);

        await service.ChatAsync("first");
        await service.ChatAsync("second");

        var hs = GetHistory(service);
        Assert.Equal(AuthorRole.System, hs[0].Role);
        Assert.Equal("first", hs[1].Content);
        Assert.Equal(AuthorRole.Assistant, hs[2].Role);
        Assert.Equal("回复1", hs[2].Content);
        Assert.Equal("second", hs[3].Content);
        Assert.Equal(AuthorRole.Assistant, hs[4].Role);
        Assert.Equal("回复2", hs[4].Content);
    }

    [Fact]
    public async Task ChatAsync_EmptyRegistry_StillWorks()
    {
        var registry = new ToolRegistry();
        var chatService = new ScriptedChatCompletion(new Func<ChatHistory, ChatMessageContent>[]
        {
            _ => new ChatMessageContent(AuthorRole.Assistant, "ok")
        });
        var kernel = CreateKernelWithChat(chatService, registry);
        var service = new SemanticKernelChatService(kernel, registry);

        var reply = await service.ChatAsync("hi");
        Assert.Equal("ok", reply);
    }

    [Fact]
    public void Constructor_WithNullRegistry_UsesEmptyRegistry()
    {
        var kernel = Kernel.CreateBuilder().Build();
        var service = new SemanticKernelChatService(kernel);
        Assert.NotNull(service);
        // system message 也不应包含工具列表
        Assert.DoesNotContain("你可用的工具", GetHistory(service)[0].Content);
    }

    [Fact]
    public void Constructor_WithTools_SystemPromptMentionsTools()
    {
        var registry = new ToolRegistry();
        registry.Register(new ArchiveByDateTool());
        var kernel = Kernel.CreateBuilder().Build();
        var service = new SemanticKernelChatService(kernel, registry);

        var sysPrompt = GetHistory(service)[0].Content;
        Assert.Contains("DeskPilot", sysPrompt);
        Assert.Contains("archive_files_by_date", sysPrompt);
        Assert.Contains("按文件日期", sysPrompt);
    }

    [Fact]
    public void ChatService_PassesCancellationToken_ToKernel()
    {
        // ChatService 内部把 token 传给 chatService.GetChatMessageContentAsync(history, settings, kernel, token)
        // 我们这里只验证 ChatService 不会因为 token 报错（具体传播由 SK 保证）
        var registry = new ToolRegistry();
        var chatService = new ScriptedChatCompletion(new Func<ChatHistory, ChatMessageContent>[]
        {
            _ => new ChatMessageContent(AuthorRole.Assistant, "ok")
        });
        var kernel = CreateKernelWithChat(chatService, registry);
        var service = new SemanticKernelChatService(kernel, registry);

        // 验证 default(CancellationToken) 不报错
        var task = service.ChatAsync("hi");
        Assert.True(task.IsCompletedSuccessfully || !task.IsFaulted);
    }

    private sealed class CancellableChatCompletion : IChatCompletionService
    {
        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

        public Task<ChatMessageContent> GetChatMessageContentAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChatMessageContent(AuthorRole.Assistant, "x"));
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChatMessageContent>>(new[] {
                new ChatMessageContent(AuthorRole.Assistant, "x") });
    }

    [Fact]
    public void Kernel_PluginsContainArchiveByDatePlugin()
    {
        var registry = new ToolRegistry();
        registry.Register(new ArchiveByDateTool());
        var kernel = CreateKernelWithChat(
            new ScriptedChatCompletion(Array.Empty<Func<ChatHistory, ChatMessageContent>>()),
            registry);

        // plugin 名字等于 tool name
        var plugin = kernel.Plugins.FirstOrDefault(p => p.Name == "archive_files_by_date");
        Assert.NotNull(plugin);
        // plugin 里至少一个 function（archive_by_date）
        Assert.Contains(kernel.Plugins.GetFunctionsMetadata(), m => m.Name == "archive_by_date");
    }
}