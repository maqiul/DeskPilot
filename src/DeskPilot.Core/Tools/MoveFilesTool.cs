using System.Text.Json;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 批量移动文件工具。
///
/// AI 调用示例：
/// {
///   "sourceDirectory": "C:\\Users\\me\\Desktop\\old",
///   "targetDirectory": "C:\\Users\\me\\Documents\\archive",
///   "pattern": "*.pdf",
///   "createIfMissing": true
/// }
/// </summary>
public sealed class MoveFilesTool : ITool
{
    public string Name => "move_files";
    public string Description =>
        "把源目录里的文件批量移动到目标目录。可选 glob 过滤（如 *.pdf）。" +
        "默认不递归子目录。文件已在目标目录存在则自动加 _2/_3 后缀。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "sourceDirectory": { "type": "string", "description": "源目录绝对路径" },
            "targetDirectory": { "type": "string", "description": "目标目录绝对路径" },
            "pattern": { "type": "string", "description": "文件名 glob 过滤（如 *.pdf），留空匹配全部" },
            "recursive": { "type": "boolean", "description": "是否递归子目录，默认 false" },
            "createIfMissing": { "type": "boolean", "description": "目标目录不存在是否自动创建，默认 true" }
          },
          "required": ["sourceDirectory", "targetDirectory"]
        }
        """;

    [Microsoft.SemanticKernel.KernelFunction("move_files")]
    public async Task<string> MoveFilesKernelAsync(
        string sourceDirectory,
        string targetDirectory,
        string? pattern = null,
        bool recursive = false,
        bool createIfMissing = true)
    {
        var args = JsonSerializer.Serialize(new
        {
            sourceDirectory,
            targetDirectory,
            pattern,
            recursive,
            createIfMissing
        });
        var result = await ExecuteAsync(args).ConfigureAwait(false);
        return JsonSerializer.Serialize(new
        {
            success = result.Success,
            summary = result.Summary,
            error = result.ErrorMessage,
            data = result.Data
        });
    }

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
    {
        MoveArgs args;
        try { args = JsonSerializer.Deserialize<MoveArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (!Directory.Exists(args.SourceDirectory))
            return ToolResult.Fail($"源目录不存在：{args.SourceDirectory}");

        try
        {
            if (args.CreateIfMissing && !Directory.Exists(args.TargetDirectory))
                Directory.CreateDirectory(args.TargetDirectory);
            else if (!Directory.Exists(args.TargetDirectory))
                return ToolResult.Fail($"目标目录不存在：{args.TargetDirectory}");

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = args.Recursive,
                IgnoreInaccessible = true
            };
            var files = Directory.EnumerateFiles(args.SourceDirectory, args.Pattern ?? "*", options).ToList();

            var report = new MoveReport
            {
                SourceDirectory = args.SourceDirectory,
                TargetDirectory = args.TargetDirectory,
                Scanned = files.Count,
                Details = new List<MoveDetail>()
            };

            foreach (var src in files)
            {
                ct.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(src);
                var dst = Path.Combine(args.TargetDirectory, fileName);

                try
                {
                    var finalDst = ResolveCollision(dst);
                    File.Move(src, finalDst);
                    report.Moved++;
                    report.Details.Add(new MoveDetail { SourcePath = src, TargetPath = finalDst, Status = "Moved" });
                }
                catch (Exception ex)
                {
                    report.Failed++;
                    report.Details.Add(new MoveDetail { SourcePath = src, TargetPath = dst, Status = "Failed", Message = ex.Message });
                }

                await Task.Yield();
            }

            return ToolResult.Ok(
                $"✅ 移动完成：移动 {report.Moved} 个，失败 {report.Failed} 个（扫描 {report.Scanned}）",
                report);
        }
        catch (OperationCanceledException) { return ToolResult.Fail("用户取消"); }
        catch (Exception ex) { return ToolResult.Fail($"移动异常：{ex.Message}"); }
    }

    private static string ResolveCollision(string targetPath)
    {
        if (!File.Exists(targetPath)) return targetPath;
        var dir = Path.GetDirectoryName(targetPath)!;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        var ext = Path.GetExtension(targetPath);
        for (int i = 2; i < 1000; i++)
        {
            var c = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(c)) return c;
        }
        return Path.Combine(dir, $"{name}_{DateTime.Now:HHmmssfff}{ext}");
    }

    private sealed class MoveArgs
    {
        public string SourceDirectory { get; set; } = string.Empty;
        public string TargetDirectory { get; set; } = string.Empty;
        public string? Pattern { get; set; }
        public bool Recursive { get; set; }
        public bool CreateIfMissing { get; set; } = true;
    }
}

public sealed class MoveReport
{
    public string SourceDirectory { get; set; } = string.Empty;
    public string TargetDirectory { get; set; } = string.Empty;
    public int Scanned { get; set; }
    public int Moved { get; set; }
    public int Failed { get; set; }
    public List<MoveDetail> Details { get; set; } = new();
}

public sealed class MoveDetail
{
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}