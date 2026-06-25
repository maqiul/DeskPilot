using DeskPilot.Core.Tools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeskPilot.Core.Services;

/// <summary>
/// 工具调用事件参数。
/// </summary>
public sealed class ToolEventArgs : EventArgs
{
    public string ToolName { get; }
    public bool Success { get; }
    public long ElapsedMs { get; }
    public string? Detail { get; }

    public ToolEventArgs(string toolName, bool success, long elapsedMs, string? detail = null)
    {
        ToolName = toolName;
        Success = success;
        ElapsedMs = elapsedMs;
        Detail = detail;
    }
}

/// <summary>
/// 基于 Semantic Kernel 的聊天服务。
///
/// Tool Calling 行为：
/// - 启用 FunctionChoiceBehavior.Auto() 后，SK 自动决定何时调工具
/// - SK 自动执行工具 + 把结果塞回 ChatHistory + 让 AI 生成自然语言响应
/// - 我们只管调用 + 拿最终响应，不手动循环
/// - 通过 SK 的 FunctionInvocationFilter（推荐做法，替代过时的 events）暴露
///   ToolInvoking / ToolInvoked 事件给上层（UI 层）用来显示实时状态
/// </summary>
public sealed class SemanticKernelChatService : IChatService
{
    private readonly Kernel _kernel;
    private readonly IToolRegistry _toolRegistry;
    private readonly ChatHistory _history = new();

    /// <summary>
    /// 工具开始调用前触发。供 UI 显示"🔧 正在调用 xxx..."
    /// </summary>
    public event EventHandler<ToolEventArgs>? ToolInvoking;

    /// <summary>
    /// 工具调用完成后触发（无论成功失败）。供 UI 显示"✅ xxx 完成 (123ms)"。
    /// </summary>
    public event EventHandler<ToolEventArgs>? ToolInvoked;

    public SemanticKernelChatService(Kernel kernel, IToolRegistry? toolRegistry = null)
    {
        _kernel = kernel;
        _toolRegistry = toolRegistry ?? new ToolRegistry();
        _history.AddSystemMessage(BuildSystemPrompt());

        // 用 SK 推荐的 FunctionInvocationFilter 监听工具调用
        // 替代过时的 Kernel.FunctionInvoking/FunctionInvoked events
        _kernel.FunctionInvocationFilters.Add(new ToolCallObserver(this));
    }

    private string BuildSystemPrompt()
    {
        var sb = new System.Text.StringBuilder(
            "你是 DeskPilot，一个桌面 AI 助手 ✈️。\n" +
            "你擅长帮助用户处理办公场景任务：文件整理、文档处理、数据整理等。\n" +
            "请用简洁、专业的中文回答。");

        var tools = _toolRegistry.ListTools();
        if (tools.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## 你可用的工具");
            foreach (var t in tools)
            {
                sb.AppendLine($"- **{t.Name}**: {t.Description}");
            }
            sb.AppendLine();
            sb.AppendLine("调用工具时要诚实——如果工具返回失败，告诉用户具体原因，不要编造结果。");
        }

        return sb.ToString();
    }

    public async Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        _history.AddUserMessage(userMessage);

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        // SK 1.32：传 kernel 后会自动处理 tool calling 循环
        var result = await chatService.GetChatMessageContentAsync(
            _history,
            executionSettings,
            _kernel,
            cancellationToken).ConfigureAwait(false);

        var assistantMessage = result.Content ?? string.Empty;
        _history.AddAssistantMessage(assistantMessage);
        return assistantMessage;
    }

    public void Dispose() { /* Kernel 生命周期由 DI 容器管理 */ }

    /// <summary>
    /// 流式对话：逐 token 返回 AI 回复，实现"打字机"效果。
    /// SK 1.32 的 GetStreamingChatMessageContentsAsync + FunctionChoiceBehavior.Auto()
    /// 会自动处理 tool calling 循环——先内部执行工具，再流式输出最终 LLM 回复。
    /// </summary>
    public async IAsyncEnumerable<string> ChatStreamAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _history.AddUserMessage(userMessage);

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var fullResponse = new StringBuilder();
        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
            _history, executionSettings, _kernel, cancellationToken).ConfigureAwait(false))
        {
            var text = chunk.Content ?? string.Empty;
            fullResponse.Append(text);
            yield return text;
        }

        _history.AddAssistantMessage(fullResponse.ToString());
    }

    /// <summary>
    /// 工具调用观察者：把 SK 的 FunctionInvocationFilter 转成我们自己的事件。
    /// </summary>
    private sealed class ToolCallObserver : IFunctionInvocationFilter
    {
        private readonly SemanticKernelChatService _owner;

        public ToolCallObserver(SemanticKernelChatService owner) => _owner = owner;

        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            var name = context.Function.Name;
            var sw = Stopwatch.StartNew();

            _owner.ToolInvoking?.Invoke(_owner, new ToolEventArgs(name, true, 0, "Invoking"));

            try
            {
                await next(context).ConfigureAwait(false);
                sw.Stop();
                _owner.ToolInvoked?.Invoke(_owner, new ToolEventArgs(name, true, sw.ElapsedMilliseconds, "Invoked"));
            }
            catch
            {
                sw.Stop();
                _owner.ToolInvoked?.Invoke(_owner, new ToolEventArgs(name, false, sw.ElapsedMilliseconds, "Failed"));
                throw;
            }
        }
    }
}