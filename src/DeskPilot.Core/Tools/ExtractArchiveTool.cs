using System.IO.Compression;
using System.Text.Json;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 解压 zip 文件工具（System.IO.Compression 内置，无第三方依赖）。
///
/// AI 调用示例：
/// {
///   "archivePath": "C:\\downloads\\receipts.zip",
///   "outputDirectory": "C:\\invoices",
///   "overwrite": false
/// }
/// </summary>
public sealed class ExtractArchiveTool : ITool
{
    public RiskLevel Risk => RiskLevel.Destructive;

    public string Name => "extract_archive";
    public string Description =>
        "解压 zip 文件到指定目录。" +
        "安全：自动跳过 Zip Slip 攻击（路径穿越）。" +
        "可选覆盖已存在文件。" +
        "注意：当前仅支持 zip 格式（rar/7z 待后续扩展）。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "archivePath": { "type": "string", "description": "zip 文件绝对路径" },
            "outputDirectory": { "type": "string", "description": "解压目标目录（留空则解压到 zip 同名的子目录）" },
            "overwrite": { "type": "boolean", "description": "是否覆盖已存在文件，默认 false" }
          },
          "required": ["archivePath"]
        }
        """;

    [Microsoft.SemanticKernel.KernelFunction("extract_archive")]
    public async Task<string> ExtractKernelAsync(
        string archivePath,
        string? outputDirectory = null,
        bool overwrite = false)
    {
        var args = JsonSerializer.Serialize(new { archivePath, outputDirectory, overwrite });
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
        ExtractArgs args;
        try { args = JsonSerializer.Deserialize<ExtractArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (!File.Exists(args.ArchivePath))
            return ToolResult.Fail($"压缩文件不存在：{args.ArchivePath}");

        if (!args.ArchivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return ToolResult.Fail($"当前仅支持 .zip 格式（rar/7z 待后续扩展）");

        var outputDir = string.IsNullOrWhiteSpace(args.OutputDirectory)
            ? Path.Combine(
                Path.GetDirectoryName(args.ArchivePath) ?? Directory.GetCurrentDirectory(),
                Path.GetFileNameWithoutExtension(args.ArchivePath))
            : args.OutputDirectory!;

        try
        {
            Directory.CreateDirectory(outputDir);

            using var archive = ZipFile.OpenRead(args.ArchivePath);
            var report = new ExtractReport
            {
                ArchivePath = args.ArchivePath,
                OutputDirectory = outputDir,
                TotalEntries = archive.Entries.Count,
                Details = new List<ExtractDetail>()
            };

            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();

                // Zip Slip 防护：拒绝 ../ 开头的 entry 路径
                var destPath = Path.GetFullPath(Path.Combine(outputDir, entry.FullName));
                var fullOutputDir = Path.GetFullPath(outputDir) + Path.DirectorySeparatorChar;
                if (!destPath.StartsWith(fullOutputDir, StringComparison.Ordinal))
                {
                    report.Skipped++;
                    report.Failed++;
                    report.Details.Add(new ExtractDetail
                    {
                        EntryName = entry.FullName,
                        Status = "Failed",
                        Message = "Zip Slip 防护：路径超出目标目录"
                    });
                    continue;
                }

                try
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        // 目录条目
                        Directory.CreateDirectory(destPath);
                        report.Directories++;
                        report.Details.Add(new ExtractDetail
                        {
                            EntryName = entry.FullName,
                            OutputPath = destPath,
                            Status = "Directory"
                        });
                    }
                    else
                    {
                        // 文件条目
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                        if (File.Exists(destPath) && !args.Overwrite)
                        {
                            report.Skipped++;
                            report.Details.Add(new ExtractDetail
                            {
                                EntryName = entry.FullName,
                                OutputPath = destPath,
                                Status = "Skipped",
                                Message = "文件已存在，未开启 overwrite"
                            });
                            continue;
                        }

                        entry.ExtractToFile(destPath, args.Overwrite);
                        report.Extracted++;
                        report.Details.Add(new ExtractDetail
                        {
                            EntryName = entry.FullName,
                            OutputPath = destPath,
                            CompressedSize = entry.CompressedLength,
                            UncompressedSize = entry.Length,
                            Status = "Extracted"
                        });
                    }
                }
                catch (Exception ex)
                {
                    report.Failed++;
                    report.Details.Add(new ExtractDetail
                    {
                        EntryName = entry.FullName,
                        OutputPath = destPath,
                        Status = "Failed",
                        Message = ex.Message
                    });
                }

                await Task.Yield();
            }

            return ToolResult.Ok(
                $"✅ 解压完成：{report.Extracted} 个文件，{report.Directories} 个目录，" +
                $"{report.Skipped} 跳过，{report.Failed} 失败（{report.TotalEntries} 总条目）",
                report);
        }
        catch (OperationCanceledException) { return ToolResult.Fail("用户取消"); }
        catch (InvalidDataException) { return ToolResult.Fail($"不是有效的 zip 文件或文件已损坏：{args.ArchivePath}"); }
        catch (Exception ex) { return ToolResult.Fail($"解压异常：{ex.Message}"); }
    }

    private sealed class ExtractArgs
    {
        public string ArchivePath { get; set; } = string.Empty;
        public string? OutputDirectory { get; set; }
        public bool Overwrite { get; set; }
    }
}

public sealed class ExtractReport
{
    public string ArchivePath { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public int TotalEntries { get; set; }
    public int Extracted { get; set; }
    public int Directories { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<ExtractDetail> Details { get; set; } = new();
}

public sealed class ExtractDetail
{
    public string EntryName { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public long CompressedSize { get; set; }
    public long UncompressedSize { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}