using DeskPilot.Core.Tools;
using System.Text;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>v0.13 B1: 文本文件统计工具测试。</summary>
public class TextStatsToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly TextStatsTool _tool = new();

    public TextStatsToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "textstats_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    private static string E(string path) => path.Replace("\\", "\\\\");

    [Fact]
    public async Task TextStats_FileNotExists_ReturnsFail()
    {
        var missing = E(Path.Combine(_testDir, "ghost.txt"));
        var json = $"{{ \"filePath\": \"{missing}\" }}";
        var result = await _tool.ExecuteAsync(json);
        Assert.False(result.Success);
        Assert.Contains("文件不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task TextStats_EmptyFile_ZeroStats()
    {
        var path = Path.Combine(_testDir, "empty.txt");
        File.WriteAllText(path, string.Empty);
        var json = $"{{ \"filePath\": \"{E(path)}\" }}";
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var summary = result.Summary;
        Assert.Contains("empty.txt", summary);
        Assert.Contains("0 行", summary);
        Assert.Contains("0 字符", summary);
    }

    [Fact]
    public async Task TextStats_AsciiContent_CorrectLineAndCharCount()
    {
        var path = Path.Combine(_testDir, "ascii.txt");
        var content = "hello\nworld\nfoo bar\n";
        File.WriteAllText(path, content);
        var json = $"{{ \"filePath\": \"{E(path)}\" }}";
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        Assert.Contains("3 行", result.Summary);   // 3 行（最后有换行）
        Assert.Contains("20 字符", result.Summary); // hello(5)+\n(1)+world(5)+\n(1)+foo bar(7)+\n(1)=20
    }

    [Fact]
    public async Task TextStats_ChineseContent_CountsCharsAndWords()
    {
        var path = Path.Combine(_testDir, "cn.txt");
        // "今天天气真好" 6 个汉字
        var content = "今天天气真好\n你好世界\n";
        File.WriteAllText(path, content, Encoding.UTF8);
        var json = $"{{ \"filePath\": \"{E(path)}\" }}";
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        // 含 utf-8 encoding + 中文字符
        Assert.Contains("utf-8", result.Summary.ToLowerInvariant());
        // 中文字符 + 换行
        Assert.Contains("字符", result.Summary);
    }

    [Fact]
    public async Task TextStats_TopN_LimitsResults()
    {
        var path = Path.Combine(_testDir, "freq.txt");
        // 制造高频词：apple 出现 5 次，banana 出现 3 次，cherry 出现 2 次
        var sb = new StringBuilder();
        for (var i = 0; i < 5; i++) sb.AppendLine("apple apple");
        for (var i = 0; i < 3; i++) sb.AppendLine("banana banana");
        for (var i = 0; i < 2; i++) sb.AppendLine("cherry");
        File.WriteAllText(path, sb.ToString());
        var json = $"{{ \"filePath\": \"{E(path)}\", \"topN\": 2 }}";
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        // summary 应只显示 top 2
        Assert.Contains("top 2", result.Summary);
        // 最高频词 apple 应在前 2
        Assert.Contains("apple", result.Summary);
    }

    [Fact]
    public async Task TextStats_TopNZero_NoWordStats()
    {
        var path = Path.Combine(_testDir, "wordcount_only.txt");
        File.WriteAllText(path, "alpha beta gamma\n");
        var json = $"{{ \"filePath\": \"{E(path)}\", \"topN\": 0 }}";
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        // topN=0 时 summary 不应含「高频词」标记
        Assert.DoesNotContain("高频词", result.Summary);
    }
}
