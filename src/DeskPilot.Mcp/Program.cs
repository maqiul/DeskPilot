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

        // 注册 DeskPilot 工具 (v0.5: 4 → 7 → v0.16.2: 10)
        builder.Services.AddSingleton<ArchiveByDateTool>();
        builder.Services.AddSingleton<MoveFilesTool>();
        builder.Services.AddSingleton<FindDuplicatesTool>();
        builder.Services.AddSingleton<RenameByPatternTool>();
        builder.Services.AddSingleton<BatchResizeImageTool>();
        builder.Services.AddSingleton<ExtractArchiveTool>();
        builder.Services.AddSingleton<HashFilesTool>();
        builder.Services.AddSingleton<MergePdfTool>();
        builder.Services.AddSingleton<ConvertImageTool>();
        builder.Services.AddSingleton<TextStatsTool>();

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
    private readonly BatchResizeImageTool _resize;
    private readonly ExtractArchiveTool _extract;
    private readonly HashFilesTool _hash;
    private readonly MergePdfTool _mergePdf;
    private readonly ConvertImageTool _convertImage;
    private readonly TextStatsTool _textStats;

    public DeskPilotMcpTools(
        ArchiveByDateTool archive,
        MoveFilesTool move,
        FindDuplicatesTool find,
        RenameByPatternTool rename,
        BatchResizeImageTool resize,
        ExtractArchiveTool extract,
        HashFilesTool hash,
        MergePdfTool mergePdf,
        ConvertImageTool convertImage,
        TextStatsTool textStats)
    {
        _archive = archive;
        _move = move;
        _find = find;
        _rename = rename;
        _resize = resize;
        _extract = extract;
        _hash = hash;
        _mergePdf = mergePdf;
        _convertImage = convertImage;
        _textStats = textStats;
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

    /// <summary>
    /// 批量缩放图片到指定尺寸（保持原图比例）。支持 jpg/png/bmp/gif，可选 JPEG 质量。
    /// </summary>
    /// <param name="directory">目标目录绝对路径</param>
    /// <param name="maxWidth">最大宽度（像素）</param>
    /// <param name="maxHeight">最大高度（像素）</param>
    /// <param name="pattern">glob 过滤（如 *.jpg）</param>
    /// <param name="quality">JPEG 质量 1-100，默认 85</param>
    /// <param name="suffix">输出文件后缀，默认 _resized</param>
    /// <param name="dryRun">只预览不保存</param>
    [McpServerTool(Name = "batch_resize_image")]
    public async Task<string> BatchResizeImage(
        string directory,
        int maxWidth,
        int maxHeight,
        string? pattern = null,
        int? quality = null,
        string? suffix = null,
        bool dryRun = false)
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new
        {
            directory,
            maxWidth,
            maxHeight,
            pattern,
            quality,
            suffix,
            dryRun
        });
        var result = await _resize.ExecuteAsync(args);
        return FormatResult(result);
    }

    /// <summary>
    /// 解压 zip 文件到指定目录。自动防 Zip Slip 攻击。
    /// </summary>
    /// <param name="archivePath">zip 文件绝对路径</param>
    /// <param name="outputDirectory">解压目标目录（留空则解压到 zip 同名的子目录）</param>
    /// <param name="overwrite">是否覆盖已存在文件</param>
    [McpServerTool(Name = "extract_archive")]
    public async Task<string> ExtractArchive(
        string archivePath,
        string? outputDirectory = null,
        bool overwrite = false)
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new
        {
            archivePath,
            outputDirectory,
            overwrite
        });
        var result = await _extract.ExecuteAsync(args);
        return FormatResult(result);
    }

    /// <summary>
    /// 批量计算文件的哈希值。支持 md5/sha1/sha256/sha512，可选递归。
    /// </summary>
    /// <param name="directory">目标目录绝对路径</param>
    /// <param name="pattern">glob 过滤（如 *.pdf）</param>
    /// <param name="algorithm">哈希算法: md5 / sha1 / sha256 / sha512，默认 sha256</param>
    /// <param name="recursive">是否递归子目录</param>
    [McpServerTool(Name = "hash_files")]
    public async Task<string> HashFiles(
        string directory,
        string? pattern = null,
        string? algorithm = null,
        bool recursive = false)
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new
        {
            directory,
            pattern,
            algorithm,
            recursive
        });
        var result = await _hash.ExecuteAsync(args);
        return FormatResult(result);
    }

    /// <summary>
    /// 把多份 PDF 合并为一份新 PDF（按顺序拼接页面）。
    /// </summary>
    /// <param name="inputFiles">输入 PDF 绝对路径数组（按顺序合并）</param>
    /// <param name="outputPath">合并后的新 PDF 绝对路径</param>
    [McpServerTool(Name = "merge_pdfs")]
    public async Task<string> MergePdfs(
        string[] inputFiles,
        string outputPath)
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new { inputFiles, outputPath });
        var result = await _mergePdf.ExecuteAsync(args);
        return FormatResult(result);
    }

    /// <summary>
    /// 把一张图片从源格式转换为目标格式（png/jpg/bmp/webp/gif）。
    /// </summary>
    /// <param name="inputPath">源图片绝对路径</param>
    /// <param name="outputPath">目标图片绝对路径</param>
    /// <param name="targetFormat">目标格式: 小写 png / jpg / bmp / webp / gif</param>
    /// <param name="quality">JPG 质量 1-100（默认 85，其他格式忽略）</param>
    [McpServerTool(Name = "convert_image")]
    public async Task<string> ConvertImage(
        string inputPath,
        string outputPath,
        string targetFormat,
        int? quality = null)
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new
        {
            inputPath,
            outputPath,
            targetFormat,
            quality = quality ?? 85
        });
        var result = await _convertImage.ExecuteAsync(args);
        return FormatResult(result);
    }

    /// <summary>
    /// 统计文本文件的字符数、单词数、行数等元信息。
    /// </summary>
    /// <param name="inputPath">文本文件绝对路径</param>
    /// <param name="encoding">文件编码（默认 utf-8，可选 gbk / gb2312 / ascii / utf-16）</param>
    [McpServerTool(Name = "text_stats")]
    public async Task<string> TextStats(
        string inputPath,
        string? encoding = null)
    {
        var args = System.Text.Json.JsonSerializer.Serialize(new { inputPath, encoding });
        var result = await _textStats.ExecuteAsync(args);
        return FormatResult(result);
    }

    private static string FormatResult(ToolResult result)
    {
        if (result.Success)
            return $"OK: {result.Summary}\n\n```json\n{System.Text.Json.JsonSerializer.Serialize(result.Data)}\n```";
        return $"ERROR: {result.Summary}";
    }
}
