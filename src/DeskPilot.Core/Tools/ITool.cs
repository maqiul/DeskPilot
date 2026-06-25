namespace DeskPilot.Core.Tools;

/// <summary>
/// 工具风险等级。
/// </summary>
public enum RiskLevel
{
    /// <summary>只读操作，不会修改任何文件</summary>
    Safe,
    /// <summary>会修改/移动/删除文件，需要用户确认</summary>
    Destructive
}

/// <summary>
/// 工具统一接口。所有 DeskPilot "能干活"的工具都实现此接口。
/// 类似 MCP 的 Function Calling 风格（先做内嵌实现，未来可暴露为 MCP Server）。
/// </summary>
public interface ITool
{
    /// <summary>
    /// 工具名称（英文短名，供 AI 调用）。
    /// 例：archive_files_by_date
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 工具描述（中文，供 AI 理解何时调用）。
    /// 例："按文件日期把目录里的文件归档到子文件夹"
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 输入参数 JSON Schema（供 AI 解析参数）。
    /// </summary>
    string InputSchemaJson { get; }

    /// <summary>
    /// 风险等级。Safe = 只读，Destructive = 会修改文件。
    /// </summary>
    RiskLevel Risk { get; }

    /// <summary>
    /// 执行工具。
    /// </summary>
    /// <param name="argumentsJson">参数 JSON 字符串</param>
    /// <param name="ct">取消令牌</param>
    Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct = default);
}

/// <summary>
/// 工具执行结果。
/// </summary>
/// <param name="Success">是否成功（业务层面）</param>
/// <param name="Summary">人类可读的简短摘要（给用户看）</param>
/// <param name="Data">结构化数据（给 AI 进一步推理用）</param>
/// <param name="ErrorMessage">错误信息（如果失败）</param>
public sealed record ToolResult(
    bool Success,
    string Summary,
    object? Data = null,
    string? ErrorMessage = null)
{
    public static ToolResult Ok(string summary, object? data = null)
        => new(true, summary, data);

    public static ToolResult Fail(string error, object? data = null)
        => new(false, $"❌ {error}", data, error);
}