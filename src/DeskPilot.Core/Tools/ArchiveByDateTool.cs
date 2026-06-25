using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 日期字段选择：按文件修改时间还是创建时间归档。
/// </summary>
public enum DateField
{
    Modified,
    Created
}

/// <summary>
/// 归档粒度：按年/月/日分子目录。
/// </summary>
public enum ArchiveGranularity
{
    Year,   // 2024/
    Month,  // 2024-03/
    Day     // 2024-03-15/
}

/// <summary>
/// 按日期归档文件工具。
///
/// AI 调用示例：
/// {
///   "sourceDirectory": "C:\\Users\\me\\Desktop\\发票",
///   "dateField": "Modified",
///   "granularity": "Month",
///   "dryRun": false
/// }
/// </summary>
public sealed class ArchiveByDateTool : ITool
{
    public RiskLevel Risk => RiskLevel.Destructive;

    public string Name => "archive_files_by_date";
    public string Description =>
        "按文件日期（修改时间或创建时间）把目录里的文件归档到子文件夹。 " +
        "可指定粒度（年/月/日）和目标目录。默认情况下移动文件；dryRun=true 时只报告不移动。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "sourceDirectory": { "type": "string", "description": "要归档的源目录绝对路径" },
            "targetDirectory": { "type": "string", "description": "归档目标根目录（留空则在源目录下创建 archive/ 子目录）" },
            "dateField": { "type": "string", "enum": ["Modified", "Created"], "description": "按哪个日期归档" },
            "granularity": { "type": "string", "enum": ["Year", "Month", "Day"], "description": "子目录粒度" },
            "dryRun": { "type": "boolean", "description": "是否只预览不实际移动" },
            "pattern": { "type": "string", "description": "文件名过滤（glob，如 *.pdf），留空匹配全部" }
          },
          "required": ["sourceDirectory"]
        }
        """;

    /// <summary>
    /// Semantic Kernel 调用的入口。
    /// SK 会根据 [KernelFunction] 标注的方法名 + 参数类型生成 schema 推给 AI。
    /// AI 决定调这个方法时，会填好下面每个参数 → 我们转 JSON 再走 ITool.ExecuteAsync 路径。
    /// </summary>
    [Microsoft.SemanticKernel.KernelFunction("archive_by_date")]
    public async Task<string> ArchiveByDateKernelAsync(
        string sourceDirectory,
        string? targetDirectory = null,
        string? dateField = null,
        string? granularity = null,
        bool dryRun = false,
        string? pattern = null)
    {
        // 把 SK 的强类型参数重新打包为 JSON 走 ITool 路径（保持单一实现路径）
        var args = new
        {
            sourceDirectory,
            targetDirectory,
            dateField = string.IsNullOrWhiteSpace(dateField) ? "Modified" : dateField,
            granularity = string.IsNullOrWhiteSpace(granularity) ? "Month" : granularity,
            dryRun,
            pattern
        };
        var json = System.Text.Json.JsonSerializer.Serialize(args);
        var result = await ExecuteAsync(json).ConfigureAwait(false);

        // SK 期望返回 string（结果会进 ChatHistory）
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            success = result.Success,
            summary = result.Summary,
            error = result.ErrorMessage,
            data = result.Data
        });
    }

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
    {
        ArchiveArgs args;
        try
        {
            args = ParseArgs(argumentsJson);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"参数解析失败：{ex.Message}");
        }

        if (!Directory.Exists(args.SourceDirectory))
            return ToolResult.Fail($"源目录不存在：{args.SourceDirectory}");

        try
        {
            var report = await ArchiveInternalAsync(args, ct).ConfigureAwait(false);
            return ToolResult.Ok(
                args.DryRun
                    ? $"📋 [预览] 共 {report.Scanned} 个文件，将移动 {report.WouldMove} 个，跳过 {report.Skipped} 个"
                    : $"✅ 归档完成：移动 {report.Moved} 个，跳过 {report.Skipped} 个，失败 {report.Failed} 个",
                report);
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Fail("用户取消");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"归档异常：{ex.Message}");
        }
    }

    private static ArchiveArgs ParseArgs(string json)
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        return JsonSerializer.Deserialize<ArchiveArgs>(json, opts)
               ?? throw new InvalidOperationException("参数为空");
    }

    private static async Task<ArchiveReport> ArchiveInternalAsync(ArchiveArgs args, CancellationToken ct)
    {
        var sourceDir = args.SourceDirectory.TrimEnd('\\', '/');
        var targetRoot = string.IsNullOrWhiteSpace(args.TargetDirectory)
            ? Path.Combine(sourceDir, "archive")
            : args.TargetDirectory.TrimEnd('\\', '/');

        // 收集候选文件
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true
        };
        var files = Directory.EnumerateFiles(sourceDir, args.Pattern ?? "*", enumerationOptions).ToList();

        var report = new ArchiveReport
        {
            SourceDirectory = sourceDir,
            TargetRoot = targetRoot,
            Scanned = files.Count,
            DryRun = args.DryRun,
            Granularity = args.Granularity.ToString(),
            DateField = args.DateField.ToString(),
            Details = new List<ArchiveDetail>()
        };

        // 按 (目标子目录) 分组，避免重复创建目录
        var grouped = new Dictionary<string, List<string>>();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var date = GetDate(file, args.DateField);
                var subdir = BuildSubdir(date, args.Granularity);

                if (!grouped.TryGetValue(subdir, out var list))
                    grouped[subdir] = list = new List<string>();
                list.Add(file);
            }
            catch (Exception ex)
            {
                report.Failed++;
                report.Details.Add(new ArchiveDetail
                {
                    SourcePath = file,
                    Status = "Failed",
                    Message = $"读取日期失败：{ex.Message}"
                });
            }
        }

        report.Subdirectories = grouped.Count;

        if (!args.DryRun)
            Directory.CreateDirectory(targetRoot);

        foreach (var (subdir, sources) in grouped)
        {
            var fullTargetDir = Path.Combine(targetRoot, subdir);
            if (!args.DryRun)
                Directory.CreateDirectory(fullTargetDir);

            foreach (var src in sources)
            {
                ct.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(src);
                var dst = Path.Combine(fullTargetDir, fileName);

                try
                {
                    if (args.DryRun)
                    {
                        report.WouldMove++;
                        report.Details.Add(new ArchiveDetail
                        {
                            SourcePath = src,
                            TargetPath = dst,
                            Status = "WouldMove"
                        });
                        continue;
                    }

                    // 处理重名：在文件名后加 _2, _3 ...
                    var finalDst = ResolveCollision(dst);
                    File.Move(src, finalDst);
                    report.Moved++;
                    report.Details.Add(new ArchiveDetail
                    {
                        SourcePath = src,
                        TargetPath = finalDst,
                        Status = "Moved"
                    });
                }
                catch (Exception ex)
                {
                    report.Failed++;
                    report.Details.Add(new ArchiveDetail
                    {
                        SourcePath = src,
                        TargetPath = dst,
                        Status = "Failed",
                        Message = ex.Message
                    });
                }
            }

            // 异步让出（不阻塞 UI）
            await Task.Yield();
        }

        return report;
    }

    private static DateTime GetDate(string file, DateField field) => field switch
    {
        DateField.Created => File.GetCreationTime(file),
        _ => File.GetLastWriteTime(file)
    };

    private static string BuildSubdir(DateTime date, ArchiveGranularity granularity) => granularity switch
    {
        ArchiveGranularity.Year => date.ToString("yyyy"),
        ArchiveGranularity.Day => date.ToString("yyyy-MM-dd"),
        _ => date.ToString("yyyy-MM")
    };

    private static string ResolveCollision(string targetPath)
    {
        if (!File.Exists(targetPath)) return targetPath;

        var dir = Path.GetDirectoryName(targetPath)!;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        var ext = Path.GetExtension(targetPath);

        for (int i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        // 极端情况：返回带时间戳的
        return Path.Combine(dir, $"{name}_{DateTime.Now:HHmmssfff}{ext}");
    }

    private sealed class ArchiveArgs
    {
        public string SourceDirectory { get; set; } = string.Empty;
        public string? TargetDirectory { get; set; }
        public DateField DateField { get; set; } = DateField.Modified;
        public ArchiveGranularity Granularity { get; set; } = ArchiveGranularity.Month;
        public bool DryRun { get; set; }
        public string? Pattern { get; set; }
    }
}

/// <summary>
/// 归档报告（Data 字段返回给 AI）。
/// </summary>
public sealed class ArchiveReport
{
    public string SourceDirectory { get; set; } = string.Empty;
    public string TargetRoot { get; set; } = string.Empty;
    public int Scanned { get; set; }
    public int Subdirectories { get; set; }
    public int Moved { get; set; }
    public int WouldMove { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public bool DryRun { get; set; }
    public string Granularity { get; set; } = string.Empty;
    public string DateField { get; set; } = string.Empty;
    public List<ArchiveDetail> Details { get; set; } = new();
}

public sealed class ArchiveDetail
{
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}