using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 文件内容搜索工具：递归扫描目录，正则匹配每行内容。
///
/// AI 调用示例：
/// {
///   "directory": "C:\\src",
///   "pattern": "TODO|FIXME|HACK",
///   "fileFilter": "*.cs",
///   "maxResults": 100,
///   "recursive": true
/// }
/// </summary>
public sealed class SearchContentTool : ITool
{
    public RiskLevel Risk => RiskLevel.Safe;

    public string Name => "search_content";
    public string Description =>
        "在目录里按正则表达式搜索文件内容。返回每个匹配的文件路径、行号、匹配行内容。" +
        "可指定文件过滤（如 *.cs、*.md）、最大结果数、是否递归子目录。" +
        "纯只读工具，不会修改任何文件。适用于「帮我找所有 TODO」「哪些文件包含这个关键词」这类问题。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "directory": { "type": "string", "description": "目标目录绝对路径" },
            "pattern": { "type": "string", "description": "正则表达式（.NET 语法，如 TODO|FIXME 或 class\\s+\\w+）" },
            "fileFilter": { "type": "string", "description": "glob 文件过滤（如 *.cs / *.md），默认 * 匹配全部" },
            "maxResults": { "type": "integer", "description": "最大匹配行数，0 或省略 = 不限制", "minimum": 0, "maximum": 10000 },
            "recursive": { "type": "boolean", "description": "是否递归子目录，默认 false" }
          },
          "required": ["directory", "pattern"]
        }
        """;

    [Microsoft.SemanticKernel.KernelFunction("search_content")]
    public async Task<string> SearchKernelAsync(
        string directory,
        string pattern,
        string? fileFilter = null,
        int? maxResults = null,
        bool recursive = false)
    {
        var args = JsonSerializer.Serialize(new { directory, pattern, fileFilter, maxResults, recursive });
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
        SearchArgs args;
        try { args = JsonSerializer.Deserialize<SearchArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (string.IsNullOrWhiteSpace(args.Directory))
            return ToolResult.Fail("directory 不能为空");
        if (string.IsNullOrWhiteSpace(args.Pattern))
            return ToolResult.Fail("pattern 不能为空");
        if (!Directory.Exists(args.Directory))
            return ToolResult.Fail($"目录不存在：{args.Directory}");

        // 先验证正则语法
        Regex regex;
        try { regex = new Regex(args.Pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(2)); }
        catch (ArgumentException ex) { return ToolResult.Fail($"正则语法错误：{ex.Message}"); }

        var searchOption = args.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var fileFilter = string.IsNullOrWhiteSpace(args.FileFilter) ? "*" : args.FileFilter;
        var maxResults = args.MaxResults ?? 0;

        try
        {
            var allFiles = Directory.EnumerateFiles(args.Directory, fileFilter, searchOption);
            var matches = new List<SearchMatch>();
            var filesScanned = 0;
            var filesMatched = 0;

            foreach (var file in allFiles)
            {
                if (ct.IsCancellationRequested) break;
                filesScanned++;

                // 跳过明显非文本文件（按扩展名快速判断）
                if (IsBinaryFile(file)) continue;

                IEnumerable<string> linesEnum;
                try { linesEnum = await File.ReadAllLinesAsync(file, Encoding.UTF8, ct).ConfigureAwait(false); }
                catch { continue; } // 权限不足 / 编码错误等跳过

                var lines = linesEnum.ToArray();
                var fileMatched = false;
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var m = regex.Match(line);
                    if (!m.Success) continue;

                    if (!fileMatched) { filesMatched++; fileMatched = true; }
                    matches.Add(new SearchMatch(file, i + 1, line.Trim(), m.Value));

                    if (maxResults > 0 && matches.Count >= maxResults) break;
                }

                if (maxResults > 0 && matches.Count >= maxResults) break;
            }

            var data = new
            {
                directory = args.Directory,
                pattern = args.Pattern,
                fileFilter,
                recursive = args.Recursive,
                filesScanned,
                filesMatched,
                matchCount = matches.Count,
                truncated = maxResults > 0 && matches.Count >= maxResults,
                matches
            };

            var summary = matches.Count == 0
                ? $"🔍 在 {args.Directory} 未找到匹配「{args.Pattern}」的文件（扫描 {filesScanned} 个）"
                : $"🔍 找到 {matches.Count} 处匹配（{filesMatched} 个文件，扫描 {filesScanned} 个）：" +
                  string.Join("；", matches.Take(3).Select(m => $"{Path.GetFileName(m.FilePath)}:{m.LineNumber}"));

            return ToolResult.Ok(summary, data);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"搜索失败：{ex.Message}");
        }
    }

    /// <summary>按扩展名快速判断是否可能为二进制文件。</summary>
    private static bool IsBinaryFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".exe" or ".dll" or ".pdb" or ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".bz2"
            or ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico" or ".webp" or ".tiff"
            or ".mp3" or ".mp4" or ".avi" or ".mov" or ".mkv" or ".flv" or ".wmv"
            or ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx"
            or ".so" or ".dylib" or ".o" or ".a" or ".lib"
            or ".ttf" or ".otf" or ".woff" or ".woff2"
            or ".bin" or ".dat"
                => true,
            _ => false
        };
    }

    private sealed record SearchArgs(string Directory, string Pattern, string? FileFilter, int? MaxResults, bool Recursive);

    /// <summary>单条匹配（JSON 输出扁平）。</summary>
    public sealed record SearchMatch(string FilePath, int LineNumber, string LineContent, string MatchedText);
}
