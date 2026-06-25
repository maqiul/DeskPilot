using System.Security.Cryptography;
using System.Text.Json;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 计算文件哈希工具。支持 MD5 / SHA1 / SHA256 / SHA512。
///
/// AI 调用示例：
/// {
///   "directory": "C:\\downloads",
///   "pattern": "*.pdf",
///   "algorithm": "sha256"
/// }
/// </summary>
public sealed class HashFilesTool : ITool
{
    public string Name => "hash_files";
    public string Description =>
        "批量计算目录里文件的哈希值。支持 md5/sha1/sha256/sha512。" +
        "输出包含文件名、相对路径、大小、哈希值。" +
        "可用于文件完整性校验、找重复文件（与 find_duplicates 配合）。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "directory": { "type": "string", "description": "目标目录绝对路径" },
            "pattern": { "type": "string", "description": "glob 过滤（如 *.pdf），留空匹配全部" },
            "algorithm": { "type": "string", "enum": ["md5", "sha1", "sha256", "sha512"], "description": "哈希算法，默认 sha256" },
            "recursive": { "type": "boolean", "description": "是否递归子目录，默认 false" }
          },
          "required": ["directory"]
        }
        """;

    [Microsoft.SemanticKernel.KernelFunction("hash_files")]
    public async Task<string> HashKernelAsync(
        string directory,
        string? pattern = null,
        string? algorithm = null,
        bool recursive = false)
    {
        var args = JsonSerializer.Serialize(new { directory, pattern, algorithm, recursive });
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
        HashArgs args;
        try { args = JsonSerializer.Deserialize<HashArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (!Directory.Exists(args.Directory))
            return ToolResult.Fail($"目录不存在：{args.Directory}");

        var algorithm = (args.Algorithm ?? "sha256").ToLowerInvariant();
        if (algorithm is not ("md5" or "sha1" or "sha256" or "sha512"))
            return ToolResult.Fail($"不支持的算法：{args.Algorithm}（仅支持 md5/sha1/sha256/sha512）");

        var searchOption = args.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var pattern = args.Pattern ?? "*";

        try
        {
            var allFiles = Directory.EnumerateFiles(args.Directory, pattern, new EnumerationOptions
            {
                RecurseSubdirectories = args.Recursive,
                IgnoreInaccessible = true
            }).ToList();

            var report = new HashReport
            {
                Directory = args.Directory,
                Algorithm = algorithm,
                Pattern = pattern,
                Recursive = args.Recursive,
                Scanned = allFiles.Count,
                Details = new List<HashDetail>()
            };

            using HashAlgorithm hasher = algorithm switch
            {
                "md5" => MD5.Create(),
                "sha1" => SHA1.Create(),
                "sha256" => SHA256.Create(),
                "sha512" => SHA512.Create(),
                _ => SHA256.Create()
            };

            foreach (var file in allFiles)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fileInfo = new FileInfo(file);
                    string hash;

                    // 大文件 (>50MB) 分块读，避免内存爆
                    if (fileInfo.Length > 50 * 1024 * 1024)
                    {
                        await using var fs = File.OpenRead(file);
                        var bytes = await hasher.ComputeHashAsync(fs, ct).ConfigureAwait(false);
                        hash = Convert.ToHexString(bytes).ToLowerInvariant();
                    }
                    else
                    {
                        var bytes = await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false);
                        var hashBytes = hasher.ComputeHash(bytes);
                        hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                    }

                    report.Hashed++;
                    report.Details.Add(new HashDetail
                    {
                        FilePath = file,
                        RelativePath = Path.GetRelativePath(args.Directory, file),
                        SizeBytes = fileInfo.Length,
                        Hash = hash,
                        Status = "Hashed"
                    });
                }
                catch (Exception ex)
                {
                    report.Failed++;
                    report.Details.Add(new HashDetail
                    {
                        FilePath = file,
                        RelativePath = Path.GetRelativePath(args.Directory, file),
                        Status = "Failed",
                        Message = ex.Message
                    });
                }

                await Task.Yield();
            }

            return ToolResult.Ok(
                $"✅ {algorithm.ToUpperInvariant()} 计算完成：{report.Hashed} 成功，{report.Failed} 失败（扫描 {report.Scanned}）",
                report);
        }
        catch (OperationCanceledException) { return ToolResult.Fail("用户取消"); }
        catch (Exception ex) { return ToolResult.Fail($"计算异常：{ex.Message}"); }
    }

    private sealed class HashArgs
    {
        public string Directory { get; set; } = string.Empty;
        public string? Pattern { get; set; }
        public string? Algorithm { get; set; }
        public bool Recursive { get; set; }
    }
}

public sealed class HashReport
{
    public string Directory { get; set; } = string.Empty;
    public string Algorithm { get; set; } = "sha256";
    public string Pattern { get; set; } = "*";
    public bool Recursive { get; set; }
    public int Scanned { get; set; }
    public int Hashed { get; set; }
    public int Failed { get; set; }
    public List<HashDetail> Details { get; set; } = new();
}

public sealed class HashDetail
{
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Hash { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}