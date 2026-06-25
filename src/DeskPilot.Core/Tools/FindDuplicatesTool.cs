using System.Security.Cryptography;
using System.Text.Json;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 查找重复文件工具。按 SHA256 hash 判断内容完全相同的文件。
///
/// AI 调用示例：
/// {
///   "directory": "C:\\Users\\me\\Documents",
///   "pattern": "*.pdf",
///   "recursive": true,
///   "minSizeBytes": 1024
/// }
/// </summary>
public sealed class FindDuplicatesTool : ITool
{
    public RiskLevel Risk => RiskLevel.Safe;

    public string Name => "find_duplicates";
    public string Description =>
        "在指定目录里查找内容完全相同的重复文件（按 SHA256 哈希）。" +
        "返回每组重复文件（hash + 文件列表），不删除任何文件。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "directory": { "type": "string", "description": "要扫描的目录绝对路径" },
            "pattern": { "type": "string", "description": "glob 过滤（如 *.pdf），留空匹配全部" },
            "recursive": { "type": "boolean", "description": "是否递归子目录，默认 true" },
            "minSizeBytes": { "type": "integer", "description": "只扫描大于此字节数的文件（避免误判空文件）" }
          },
          "required": ["directory"]
        }
        """;

    [Microsoft.SemanticKernel.KernelFunction("find_duplicates")]
    public async Task<string> FindDuplicatesKernelAsync(
        string directory,
        string? pattern = null,
        bool recursive = true,
        long minSizeBytes = 0)
    {
        var args = JsonSerializer.Serialize(new
        {
            directory,
            pattern,
            recursive,
            minSizeBytes
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
        DupArgs args;
        try { args = JsonSerializer.Deserialize<DupArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (!Directory.Exists(args.Directory))
            return ToolResult.Fail($"目录不存在：{args.Directory}");

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = args.Recursive,
                IgnoreInaccessible = true
            };
            var files = Directory.EnumerateFiles(args.Directory, args.Pattern ?? "*", options).ToList();

            // 按 size 分组（先快速预筛）
            var bySize = files
                .Where(f =>
                {
                    try { return new FileInfo(f).Length >= args.MinSizeBytes; }
                    catch { return false; }
                })
                .GroupBy(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return -1L; }
                })
                .Where(g => g.Count() > 1)
                .ToList();

            var duplicateGroups = new List<DuplicateGroup>();
            var totalDuplicateFiles = 0;

            foreach (var sizeGroup in bySize)
            {
                ct.ThrowIfCancellationRequested();
                // 对同 size 的文件计算 hash
                var byHash = new Dictionary<string, List<string>>();
                foreach (var file in sizeGroup)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var hash = await ComputeHashAsync(file, ct).ConfigureAwait(false);
                        if (!byHash.TryGetValue(hash, out var list))
                            byHash[hash] = list = new List<string>();
                        list.Add(file);
                    }
                    catch
                    {
                        // 跳过读取失败的文件
                    }
                }

                foreach (var (hash, paths) in byHash.Where(kv => kv.Value.Count > 1))
                {
                    duplicateGroups.Add(new DuplicateGroup
                    {
                        Hash = hash,
                        SizeBytes = sizeGroup.Key,
                        Files = paths
                    });
                    totalDuplicateFiles += paths.Count;
                }

                await Task.Yield();
            }

            var report = new DuplicateReport
            {
                Directory = args.Directory,
                Scanned = files.Count,
                DuplicateGroups = duplicateGroups.Count,
                DuplicateFiles = totalDuplicateFiles,
                WastedBytes = duplicateGroups.Sum(g => g.SizeBytes * (g.Files.Count - 1)),
                Groups = duplicateGroups
            };

            if (duplicateGroups.Count == 0)
                return ToolResult.Ok($"✅ 未发现重复文件（扫描 {files.Count} 个）", report);

            return ToolResult.Ok(
                $"🔍 发现 {duplicateGroups.Count} 组重复文件，共 {totalDuplicateFiles} 个文件，" +
                $"可节省 {report.WastedBytes / 1024.0 / 1024.0:F2} MB",
                report);
        }
        catch (OperationCanceledException) { return ToolResult.Fail("用户取消"); }
        catch (Exception ex) { return ToolResult.Fail($"扫描异常：{ex.Message}"); }
    }

    private static async Task<string> ComputeHashAsync(string file, CancellationToken ct)
    {
        using var stream = File.OpenRead(file);
        using var sha = SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hashBytes);
    }

    private sealed class DupArgs
    {
        public string Directory { get; set; } = string.Empty;
        public string? Pattern { get; set; }
        public bool Recursive { get; set; } = true;
        public long MinSizeBytes { get; set; }
    }
}

public sealed class DuplicateReport
{
    public string Directory { get; set; } = string.Empty;
    public int Scanned { get; set; }
    public int DuplicateGroups { get; set; }
    public int DuplicateFiles { get; set; }
    public long WastedBytes { get; set; }
    public List<DuplicateGroup> Groups { get; set; } = new();
}

public sealed class DuplicateGroup
{
    public string Hash { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public List<string> Files { get; set; } = new();
}