using DeskPilot.Core.Tools;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>v0.13 B2: 文件内容搜索工具测试。</summary>
public class SearchContentToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly SearchContentTool _tool = new();

    public SearchContentToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "search_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    private static string E(string path) => path.Replace("\\", "\\\\");

    /// <summary>用反射从匿名 data 对象提取 matches 列表（避开 SearchMatch 强类型依赖）。</summary>
    private static List<object> ExtractMatches(object? data)
    {
        if (data == null) return new List<object>();
        var prop = data.GetType().GetProperty("matches");
        var enumerable = prop?.GetValue(data) as System.Collections.IEnumerable;
        return enumerable?.Cast<object>().ToList() ?? new List<object>();
    }

    private static string? GetMatchProp(object match, string propName)
    {
        var p = match.GetType().GetProperty(propName);
        return p?.GetValue(match) as string;
    }

    private static int GetMatchIntProp(object match, string propName)
    {
        var p = match.GetType().GetProperty(propName);
        return p?.GetValue(match) is int i ? i : 0;
    }

    [Fact]
    public async Task Search_DirectoryNotExists_ReturnsFail()
    {
        var missing = E(Path.Combine(_testDir, "ghost"));
        var json = $"{{ \"directory\": \"{missing}\", \"pattern\": \"TODO\" }}";
        var result = await _tool.ExecuteAsync(json);
        Assert.False(result.Success);
        Assert.Contains("目录不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_EmptyDirectory_NoMatches()
    {
        var dir = E(_testDir);
        var json = $"{{ \"directory\": \"{dir}\", \"pattern\": \"TODO\" }}";
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        Assert.Contains("未找到", result.Summary);
        var matches = ExtractMatches(result.Data);
        Assert.Empty(matches);
    }

    [Fact]
    public async Task Search_MultipleFiles_MultipleMatches()
    {
        File.WriteAllText(Path.Combine(_testDir, "a.cs"), "// TODO: implement\npublic class A {}\n");
        File.WriteAllText(Path.Combine(_testDir, "b.cs"), "// FIXME: broken\npublic class B {}\n");
        File.WriteAllText(Path.Combine(_testDir, "c.md"), "TODO list:\n- todo item\n");
        var dir = E(_testDir);
        var json = $"{{ \"directory\": \"{dir}\", \"pattern\": \"TODO|FIXME\" }}";
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var matches = ExtractMatches(result.Data);
        // 3 处 TODO/FIXME（a.cs 的 TODO + b.cs 的 FIXME + c.md 的 TODO list 标题行）
        Assert.Equal(3, matches.Count);
        Assert.Contains(matches, m => (GetMatchProp(m, "FilePath") ?? "").EndsWith("a.cs") && (GetMatchProp(m, "LineContent") ?? "").Contains("TODO"));
        Assert.Contains(matches, m => (GetMatchProp(m, "FilePath") ?? "").EndsWith("b.cs") && (GetMatchProp(m, "LineContent") ?? "").Contains("FIXME"));
    }

    [Fact]
    public async Task Search_InvalidRegex_ReturnsFail()
    {
        var dir = E(_testDir);
        // 未闭合的 [ 是非法的正则
        var json = $"{{ \"directory\": \"{dir}\", \"pattern\": \"[unclosed\" }}";
        var result = await _tool.ExecuteAsync(json);
        Assert.False(result.Success);
        Assert.Contains("正则语法错误", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_MaxResults_LimitsOutput()
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 20; i++) sb.AppendLine($"TODO item {i}");
        File.WriteAllText(Path.Combine(_testDir, "many.cs"), sb.ToString());
        var dir = E(_testDir);
        var json = $"{{ \"directory\": \"{dir}\", \"pattern\": \"TODO\", \"maxResults\": 5 }}";
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var matches = ExtractMatches(result.Data);
        Assert.Equal(5, matches.Count);
    }

    [Fact]
    public async Task Search_RecursiveOff_DoesNotEnterSubdirs()
    {
        // 顶层文件 + 子目录文件各 1 个匹配
        File.WriteAllText(Path.Combine(_testDir, "top.cs"), "// TODO: top\n");
        var sub = Path.Combine(_testDir, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "deep.cs"), "// TODO: deep\n");
        var dir = E(_testDir);

        // 非递归 → 只匹配 1 个（top.cs）
        var json = $"{{ \"directory\": \"{dir}\", \"pattern\": \"TODO\", \"recursive\": false }}";
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var matches = ExtractMatches(result.Data);
        Assert.Single(matches);
        Assert.EndsWith("top.cs", GetMatchProp(matches[0], "FilePath") ?? "");

        // 递归 → 匹配 2 个
        var jsonRec = $"{{ \"directory\": \"{dir}\", \"pattern\": \"TODO\", \"recursive\": true }}";
        var resultRec = await _tool.ExecuteAsync(jsonRec);
        Assert.True(resultRec.Success);
        var matchesRec = ExtractMatches(resultRec.Data);
        Assert.Equal(2, matchesRec.Count);
    }
}
