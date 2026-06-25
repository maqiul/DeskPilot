using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 批量重命名工具。支持 3 种模式：
/// 1. 正则替换（pattern + replacement）
/// 2. 添加前缀
/// 3. 添加后缀（保留扩展名前）
///
/// AI 调用示例：
/// {
///   "directory": "C:\\Users\\me\\photos",
///   "find": "IMG_(\\d+)",
///   "replace": "photo_$1",
///   "pattern": "*.jpg"
/// }
/// </summary>
public sealed class RenameByPatternTool : ITool
{
    public RiskLevel Risk => RiskLevel.Destructive;

    public string Name => "rename_by_pattern";
    public string Description =>
        "批量重命名目录里的文件。支持正则替换（如 IMG_001 → photo_001）、添加前缀或后缀。" +
        "DryRun 模式只预览不实际改名。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "directory": { "type": "string", "description": "目标目录绝对路径" },
            "pattern": { "type": "string", "description": "glob 过滤（如 *.jpg），留空匹配全部" },
            "find": { "type": "string", "description": "正则表达式（要替换的部分），留空则不加正则替换" },
            "replace": { "type": "string", "description": "替换字符串（支持 $1/$2 等捕获组引用）" },
            "prefix": { "type": "string", "description": "添加前缀（如 '2024_'）" },
            "suffix": { "type": "string", "description": "添加后缀（保留扩展名前，如 '_backup'）" },
            "dryRun": { "type": "boolean", "description": "true 只预览不重命名，默认 false" }
          },
          "required": ["directory"]
        }
        """;

    [Microsoft.SemanticKernel.KernelFunction("rename_by_pattern")]
    public async Task<string> RenameKernelAsync(
        string directory,
        string? pattern = null,
        string? find = null,
        string? replace = null,
        string? prefix = null,
        string? suffix = null,
        bool dryRun = false)
    {
        var args = JsonSerializer.Serialize(new
        {
            directory,
            pattern,
            find,
            replace,
            prefix,
            suffix,
            dryRun
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
        RenameArgs args;
        try { args = JsonSerializer.Deserialize<RenameArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (!Directory.Exists(args.Directory))
            return ToolResult.Fail($"目录不存在：{args.Directory}");

        var hasRegex = !string.IsNullOrEmpty(args.Find);
        var hasPrefix = !string.IsNullOrEmpty(args.Prefix);
        var hasSuffix = !string.IsNullOrEmpty(args.Suffix);
        if (!hasRegex && !hasPrefix && !hasSuffix)
            return ToolResult.Fail("必须至少指定一个：find (正则替换) / prefix / suffix");

        Regex? regex = null;
        if (hasRegex)
        {
            try { regex = new Regex(args.Find!, RegexOptions.Compiled); }
            catch (Exception ex) { return ToolResult.Fail($"正则表达式无效：{ex.Message}"); }
        }

        try
        {
            var options = new EnumerationOptions { IgnoreInaccessible = true };
            var files = Directory.EnumerateFiles(args.Directory, args.Pattern ?? "*", options).ToList();

            var report = new RenameReport
            {
                Directory = args.Directory,
                Scanned = files.Count,
                DryRun = args.DryRun,
                Details = new List<RenameDetail>()
            };

            foreach (var src in files)
            {
                ct.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(src);
                var ext = Path.GetExtension(src);
                var nameWithoutExt = Path.GetFileNameWithoutExtension(src);

                var newName = nameWithoutExt;
                if (hasRegex)
                    newName = regex!.Replace(newName, args.Replace ?? string.Empty);
                if (hasPrefix)
                    newName = args.Prefix + newName;
                if (hasSuffix)
                    newName = newName + args.Suffix;

                newName += ext;

                if (newName == fileName) continue; // 无变化

                var dst = Path.Combine(args.Directory, newName);
                var finalDst = ResolveCollision(dst);

                try
                {
                    if (!args.DryRun)
                        File.Move(src, finalDst);
                    var status = args.DryRun ? "WouldRename" : "Renamed";
                    report.Renamed++;
                    report.Details.Add(new RenameDetail
                    {
                        OldPath = src,
                        NewPath = finalDst,
                        Status = status
                    });
                }
                catch (Exception ex)
                {
                    report.Failed++;
                    report.Details.Add(new RenameDetail
                    {
                        OldPath = src,
                        NewPath = finalDst,
                        Status = "Failed",
                        Message = ex.Message
                    });
                }

                await Task.Yield();
            }

            return ToolResult.Ok(
                args.DryRun
                    ? $"📋 [预览] 共 {report.Scanned} 个文件，将重命名 {report.Renamed} 个"
                    : $"✅ 重命名完成：改名 {report.Renamed} 个，失败 {report.Failed} 个（扫描 {report.Scanned}）",
                report);
        }
        catch (OperationCanceledException) { return ToolResult.Fail("用户取消"); }
        catch (Exception ex) { return ToolResult.Fail($"重命名异常：{ex.Message}"); }
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

    private sealed class RenameArgs
    {
        public string Directory { get; set; } = string.Empty;
        public string? Pattern { get; set; }
        public string? Find { get; set; }
        public string? Replace { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public bool DryRun { get; set; }
    }
}

public sealed class RenameReport
{
    public string Directory { get; set; } = string.Empty;
    public int Scanned { get; set; }
    public int Renamed { get; set; }
    public int Failed { get; set; }
    public bool DryRun { get; set; }
    public List<RenameDetail> Details { get; set; } = new();
}

public sealed class RenameDetail
{
    public string OldPath { get; set; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}