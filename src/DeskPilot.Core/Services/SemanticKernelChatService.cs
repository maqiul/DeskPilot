using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Threading;
using System.Threading.Tasks;

namespace DeskPilot.Core.Services;

/// <summary>
/// 基于 Semantic Kernel 的聊天服务实现。
/// 支持 OpenAI / DeepSeek / Ollama 等多种模型（通过 Kernel 配置）。
/// </summary>
public class SemanticKernelChatService : IChatService
{
    private readonly Kernel _kernel;
    private readonly ChatHistory _history = new();

    public SemanticKernelChatService(Kernel kernel)
    {
        _kernel = kernel;
        _history.AddSystemMessage(
            "你是 DeskPilot，一个桌面 AI 助手。" +
            "你擅长帮助用户处理办公场景任务：文件整理、文档处理、数据整理等。" +
            "请用简洁、专业的中文回答。");
    }

    public async Task<string> ChatAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        _history.AddUserMessage(userMessage);

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatService.GetChatMessageContentAsync(_history, cancellationToken: cancellationToken);

        var assistantMessage = result.Content ?? string.Empty;
        _history.AddAssistantMessage(assistantMessage);
        return assistantMessage;
    }
}