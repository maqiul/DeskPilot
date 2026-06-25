using DeskPilot.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DeskPilot.Mcp;

/// <summary>
/// DeskPilot MCP Server.
///
/// 用途：把 DeskPilot 的 4 个工具通过 stdio 协议暴露给外部 AI 客户端
/// (Claude Desktop / Cursor / Continue.dev 等)。
///
/// 启动方式 (Claude Desktop 配置示例):
/// {
///   "mcpServers": {
///     "deskpilot": {
///       "command": "dotnet",
///       "args": ["run", "--project", "D:\\opensource\\DeskPilot\\src\\DeskPilot.Mcp"]
///     }
///   }
/// }
///
/// 设计:
/// - 每个 ITool 配一个 [McpServerTool] 方法 (强类型参数)
/// - 内部调用 ITool.ExecuteAsync(JSON) -- 复用现有工具实现
/// - 日志走 stderr (不能走 stdout，会污染 JSON-RPC 协议)
/// - SDK 0.3 用 /// XML doc comment 作为参数描述 (无 Description attribute)
/// </summary>
internal static class Program
{
    public static async Task Main(string[] args)
    {
        Console.Error.WriteLine("[DeskPilot.Mcp] starting...");

        var builder = Host.CreateApplicationBuilder(args);

        // 日志走 stderr (避免污染 MCP stdio 协议)
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

        // 注册 DeskPilot 4 个工具
        builder.Services.AddSingleton<ArchiveByDateTool>();
        builder.Services.AddSingleton<MoveFilesTool>();
        builder.Services.AddSingleton<FindDuplicatesTool>();
        builder.Services.AddSingleton<RenameByPatternTool>();

        // 注册 MCP server + 标记工具类
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<DeskPilotMcpTools>();

        Console.Error.WriteLine("[DeskPilot.Mcp] waiting for MCP client...");
        await builder.Build().RunAsync();
    }
}

/// <summary>
/// DeskPilot 的 MCP 工具桥接。
///
/// 每个方法 = 一个 MCP tool (SDK 通过反射 [McpServerTool] 发现)。
/// 内部都是转成 JSON 调 ITool.ExecuteAsync -- 零业务逻辑，全部复用。
/// 参数描述来自 /// XML doc comment 的 &lt;param&gt; 标签。
/// </summary>
[McpServerToolType]
internal sealed class DeskPilotMcpTools
{
    private readonly ArchiveByDateTool _archive;
    private readonly MoveFilesTool _move;
    private readonly FindDuplicatesTool _find;
    private readonly RenameByPatternTool _rename;

    public DeskPilotMcpTools(
        ArchiveByDateTool archive,
        MoveFilesTool move,
        FindDuplicatesTool find,
        RenameByPatternTool rename)
    {
        _archive = archive;
        _move = move;
        _find = find;
        _rename = rename;
    }

    /// <summary>
    /// 按修改/创建时间 + 年/月/日粒度归档文件到子目录。支持 glob 过滤和 DryRun 预览。
    /// </summary>
    /// <param name="sourceDirectory">源目录绝对路径</param>
    /// <param name="dateField">日期字段: Modified 或 Created (默认 Modified)</param>
    /// <param name="granularity">粒度: Year / Month / Day (默认 Month)</param>
    /// <param name="targetDirectory">目标目录 (留空则用 sourceDirectory/archive)</param>
    /// <param name="pattern">glob 过滤 (如 *.pdf)，留空匹配全部</param>
    /// <param name="dryRun">只预览不移动</param>
    [McpServerTool(Name = "archive_files_by_date")]
    public async Task<string> ArchiveFilesByDate(
        string sourceDirectory,
        string dateField = "Modified",
        string granularity = "Month",
        string? targetDirectory = null,
        string? pattern = null,
        bool dryRun = false)
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new
        {
            sourceDirectory,
            dateField,
            granularity,
            targetDirectory,
            pattern,
            dryRun
        });
        var result = await _archive.ExecuteAsync(args);
        return FormatResult(result);
    }

    /// <summary>
    /// 批量移动文件到目标目录。支持 glob 过滤、递归子目录、collision 自动 _2/_3 后缀。
    /// </summary>
    /// <param name="sourceDirectory">源目录绝对路径</param>
    /// <param name="targetDirectory">目标目录绝对路径</param>
    /// <param name="pattern">glob 过滤 (如 *.pdf)，留空匹配全部</param>
    /// <param name="recursive">是否递归子目录</param>
    /// <param name="createIfMissing">目标目录不存在是否自动创建</param>
    [McpServerTool(Name = "move_files")]
    public async Task<string> MoveFiles(
        string sourceDirectory,
        string targetDirectory,
        string? pattern = null,
        bool recursive = false,
        bool createIfMissing = true)
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new
        {
            sourceDirectory,
            targetDirectory,
            pattern,
            recursive,
            createIfMissing
        });
        var result = await _move.ExecuteAsync(args);
        return FormatResult(result);
    }

    /// <summary>
    /// 按 SHA256 哈希查找内容完全相同的重复文件。报告浪费空间 (可清理多少 MB)。
    /// </summary>
    /// <param name="directory">要扫描的目录绝对路径</param>
    /// <param name="pattern">glob 过滤 (如 *.pdf)，留空匹配全部</param>
    /// <param name="recursive">是否递归子目录 (默认 true)</param>
    /// <param name="minSizeBytes">只扫描大于此字节数的文件 (避免空文件误判)</param>
    [McpServerTool(Name = "find_duplicates")]
    public async Task<string> FindDuplicates(
        string directory,
        string? pattern = null,
        bool recursive = true,
        long minSizeBytes = 0)
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new
        {
            directory,
            pattern,
            recursive,
            minSizeBytes
        });
        var result = await _find.ExecuteAsync(args);
        return FormatResult(result);
    }

    /// <summary>
    /// 批量重命名文件。支持正则替换 ($1/$2 捕获组)、前缀/后缀添加、DryRun 预览。
    /// </summary>
    /// <param name="directory">目标目录绝对路径</param>
    /// <param name="pattern">glob 过滤 (如 *.jpg)</param>
    /// <param name="find">正则表达式 (要替换的部分)，留空则不加正则替换</param>
    /// <param name="replace">替换字符串 (支持 $1/$2 等捕获组)</param>
    /// <param name="prefix">添加前缀 (如 '2024_')</param>
    /// <param name="suffix">添加后缀 (保留扩展名前，如 '_backup')</param>
    /// <param name="dryRun">true 只预览不重命名</param>
    [McpServerTool(Name = "rename_by_pattern")]
    public async Task<string> RenameByPattern(
        string directory,
        string? pattern = null,
        string? find = null,
        string? replace = null,
        string? prefix = null,
        string? suffix = null,
        bool dryRun = false)
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new
        {
            directory,
            pattern,
            find,
            replace,
            prefix,
            suffix,
            dryRun
        });
        var result = await _rename.ExecuteAsync(args);
        return FormatResult(result);
    }

    private static string FormatResult(ToolResult result)
    {
        if (result.Success)
            return $"OK: {result.Summary}\n\n```json\n{System.Text.Json.JsonSerializer.Serialize(result.Data)}\n```";
        return $"ERROR: {result.Summary}";
    }
}
