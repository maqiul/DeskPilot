using DeskPilot.Core.Tools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Threading;
using System.Threading.Tasks;

namespace DeskPilot.Core.Services;

/// <summary>
/// 基于 Semantic Kernel 的聊天服务。
///
/// Tool Calling 行为：
/// - 启用 FunctionChoiceBehavior.Auto() 后，SK 自动决定何时调工具
/// - SK 自动执行工具 + 把结果塞回 ChatHistory + 让 AI 生成自然语言响应
/// - 我们只管调用 + 拿最终响应，不手动循环
/// </summary>
public sealed class SemanticKernelChatService : IChatService
{
    private readonly Kernel _kernel;
    private readonly IToolRegistry _toolRegistry;
    private readonly ChatHistory _history = new();

    public SemanticKernelChatService(Kernel kernel, IToolRegistry? toolRegistry = null)
    {
        _kernel = kernel;
        _toolRegistry = toolRegistry ?? new ToolRegistry();
        _history.AddSystemMessage(BuildSystemPrompt());
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
}