using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 文本文件统计工具：行数 / 字符数 / 字节数 / 编码 / 高频词。
///
/// AI 调用示例：
/// {
///   "filePath": "C:\\docs\\report.md",
///   "topN": 10
/// }
/// </summary>
public sealed class TextStatsTool : ITool
{
    public RiskLevel Risk => RiskLevel.Safe;

    public string Name => "text_stats";
    public string Description =>
        "统计文本文件的元数据：行数、字符数、字节数、检测编码、最后修改时间，" +
        "以及可选的 topN 高频词（默认 10 个，跳过常见停用词）。" +
        "纯只读工具，不会修改任何文件。适用于「这个文件多大」「哪些词出现最多」这类问题。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "filePath": { "type": "string", "description": "目标文本文件绝对路径" },
            "topN": { "type": "integer", "description": "返回 topN 高频词，0 或省略 = 不统计", "minimum": 0, "maximum": 100 }
          },
          "required": ["filePath"]
        }
        """;

    [Microsoft.SemanticKernel.KernelFunction("text_stats")]
    public async Task<string> TextStatsKernelAsync(
        string filePath,
        int? topN = null)
    {
        var args = JsonSerializer.Serialize(new { filePath, topN });
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
        TextStatsArgs args;
        try { args = JsonSerializer.Deserialize<TextStatsArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (string.IsNullOrWhiteSpace(args.FilePath))
            return ToolResult.Fail("filePath 不能为空");

        if (!File.Exists(args.FilePath))
            return ToolResult.Fail($"文件不存在：{args.FilePath}");

        try
        {
            var fileInfo = new FileInfo(args.FilePath);
            var encoding = DetectEncoding(fileInfo.FullName);
            var content = await File.ReadAllTextAsync(fileInfo.FullName, encoding, ct).ConfigureAwait(false);

            // 统计行数（用 \n 计数；最后一行若无换行则 +1）
            var lineCount = 0;
            if (content.Length > 0)
                lineCount = content.Count(c => c == '\n') + (content[^1] == '\n' ? 0 : 1);

            var charCount = content.Length;
            var wordCount = CountWords(content);
            var byteCount = fileInfo.Length;
            var topWords = (args.TopN is > 0 and <= 100)
                ? TopWords(content, args.TopN.Value)
                : Array.Empty<TopWord>();

            var data = new
            {
                filePath = fileInfo.FullName,
                fileName = fileInfo.Name,
                sizeBytes = byteCount,
                encoding = encoding.WebName,
                lineCount,
                charCount,
                wordCount,
                lastModified = fileInfo.LastWriteTime,
                topWords
            };

            var summary = $"📄 {fileInfo.Name}: {lineCount} 行 / {charCount} 字符 / {byteCount:N0} 字节 ({encoding.WebName})";
            if (topWords.Length > 0)
                summary += $"；高频词 top {topWords.Length}：{string.Join("、", topWords.Take(3).Select(w => $"{w.Word}({w.Count})"))}";

            return ToolResult.Ok(summary, data);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"读取文件失败：{ex.Message}");
        }
    }

    /// <summary>通过 BOM 检测编码，无 BOM 默认 UTF-8。</summary>
    private static Encoding DetectEncoding(string path)
    {
        using var fs = File.OpenRead(path);
        var bom = new byte[4];
        var read = fs.Read(bom, 0, 4);
        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return Encoding.UTF8;
        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;
        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode;
        if (read >= 4 && bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF) return Encoding.UTF32;
        return new UTF8Encoding(false);
    }

    /// <summary>中英混合词数：英文按连续字母数字分词，中文按每个汉字 1 词。</summary>
    private static int CountWords(string content)
    {
        var count = 0;
        var inWord = false;
        foreach (var c in content)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (!inWord) { count++; inWord = true; }
            }
            else
            {
                inWord = false;
            }
            // 中文每个字单独算（不依赖 inWord 状态）
            if (c >= 0x4E00 && c <= 0x9FFF) count++;
        }
        return count;
    }

    /// <summary>统计高频词（跳过停用词 + 单字符 + 纯数字）。</summary>
    private static TopWord[] TopWords(string content, int topN)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "and", "or", "but", "if", "then", "else", "of", "to", "in", "on", "at", "by",
            "for", "with", "as", "this", "that", "it", "its", "i", "you", "he", "she", "we", "they",
            "的", "了", "和", "是", "在", "也", "都", "与", "或", "但", "就", "那", "这", "我", "你", "他", "她", "它"
        };

        // 提取英文词（连续字母数字）+ 中文词（连续中文）
        var words = new List<string>();
        var enMatch = Regex.Matches(content, @"[A-Za-z][A-Za-z0-9_]+");
        foreach (Match m in enMatch) words.Add(m.Value.ToLowerInvariant());
        var zhMatch = Regex.Matches(content, @"[\u4E00-\u9FFF]+");
        foreach (Match m in zhMatch) words.Add(m.Value);

        return words
            .Where(w => w.Length > 1 && !stopWords.Contains(w) && !long.TryParse(w, out _))
            .GroupBy(w => w)
            .Select(g => new TopWord(g.Key, g.Count()))
            .OrderByDescending(w => w.Count)
            .ThenBy(w => w.Word)
            .Take(topN)
            .ToArray();
    }

    private sealed record TextStatsArgs(string FilePath, int? TopN);

    /// <summary>高频词条目（JSON 输出扁平）。</summary>
    public sealed record TopWord(string Word, int Count);
}
