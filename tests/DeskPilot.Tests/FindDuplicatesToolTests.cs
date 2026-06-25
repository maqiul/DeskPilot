using System.Text.Json;
using DeskPilot.Core.Tools;

namespace DeskPilot.Tests;

public sealed class FindDuplicatesToolTests : IDisposable
{
    private readonly string _root;
    private readonly FindDuplicatesTool _tool = new();

    public FindDuplicatesToolTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"deskpilot_dup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { }
    }

    private string CreateFile(string name, string content)
    {
        var p = Path.Combine(_root, name);
        File.WriteAllText(p, content);
        return p;
    }

    [Fact]
    public async Task Find_NoDuplicates_ReturnsEmptyReport()
    {
        CreateFile("a.txt", "hello");
        CreateFile("b.txt", "world");

        var result = await _tool.ExecuteAsync(JsonSerializer.Serialize(new { directory = _root }));

        Assert.True(result.Success);
        var report = (DuplicateReport)result.Data!;
        Assert.Equal(0, report.DuplicateGroups);
        Assert.Equal(2, report.Scanned);
    }

    [Fact]
    public async Task Find_TwoFilesSameContent_DetectsDuplicate()
    {
        CreateFile("a.txt", "identical content");
        CreateFile("b.txt", "identical content");

        var result = await _tool.ExecuteAsync(JsonSerializer.Serialize(new { directory = _root }));

        Assert.True(result.Success);
        var report = (DuplicateReport)result.Data!;
        Assert.Equal(1, report.DuplicateGroups);
        Assert.Equal(2, report.DuplicateFiles);
        Assert.Single(report.Groups);
        Assert.Equal(2, report.Groups[0].Files.Count);
    }

    [Fact]
    public async Task Find_DifferentContent_NoDuplicates()
    {
        CreateFile("a.txt", "content A");
        CreateFile("b.txt", "content B");
        CreateFile("c.txt", "content C");

        var result = await _tool.ExecuteAsync(JsonSerializer.Serialize(new { directory = _root }));

        Assert.True(result.Success);
        Assert.Equal(0, ((DuplicateReport)result.Data!).DuplicateGroups);
    }

    [Fact]
    public async Task Find_ThreeCopies_OneGroupOfThree()
    {
        CreateFile("a.txt", "same");
        CreateFile("b.txt", "same");
        CreateFile("c.txt", "same");

        var result = await _tool.ExecuteAsync(JsonSerializer.Serialize(new { directory = _root }));

        Assert.True(result.Success);
        var report = (DuplicateReport)result.Data!;
        Assert.Equal(1, report.DuplicateGroups);
        Assert.Equal(3, report.DuplicateFiles);
        Assert.Equal(3, report.Groups[0].Files.Count);
    }

    [Fact]
    public async Task Find_DuplicatesAcrossSubdirs_WithRecursive()
    {
        var sub = Path.Combine(_root, "sub");
        Directory.CreateDirectory(sub);
        CreateFile("a.txt", "duplicate");
        File.WriteAllText(Path.Combine(sub, "b.txt"), "duplicate");

        var result = await _tool.ExecuteAsync(JsonSerializer.Serialize(new { directory = _root, recursive = true }));

        Assert.True(result.Success);
        Assert.Equal(1, ((DuplicateReport)result.Data!).DuplicateGroups);
    }

    [Fact]
    public async Task Find_DuplicatesAcrossSubdirs_NonRecursive_Missed()
    {
        var sub = Path.Combine(_root, "sub");
        Directory.CreateDirectory(sub);
        CreateFile("a.txt", "duplicate");
        File.WriteAllText(Path.Combine(sub, "b.txt"), "duplicate");

        var result = await _tool.ExecuteAsync(JsonSerializer.Serialize(new { directory = _root, recursive = false }));

        Assert.True(result.Success);
        Assert.Equal(0, ((DuplicateReport)result.Data!).DuplicateGroups);
    }

    [Fact]
    public async Task Find_PatternFilter_OnlyScansMatchingFiles()
    {
        CreateFile("a.pdf", "PDF content");
        CreateFile("b.pdf", "PDF content");
        CreateFile("c.txt", "PDF content"); // 同内容但 .txt 不算

        var result = await _tool.ExecuteAsync(JsonSerializer.Serialize(new { directory = _root, pattern = "*.pdf" }));

        Assert.True(result.Success);
        var report = (DuplicateReport)result.Data!;
        Assert.Equal(1, report.DuplicateGroups);
        Assert.All(report.Groups[0].Files, f => Assert.EndsWith(".pdf", f));
    }

    [Fact]
    public async Task Find_WastedBytes_CalculatedCorrectly()
    {
        var content = new string('x', 1000); // 1KB
        CreateFile("a.txt", content);
        CreateFile("b.txt", content);
        CreateFile("c.txt", content);

        var result = await _tool.ExecuteAsync(JsonSerializer.Serialize(new { directory = _root }));

        Assert.True(result.Success);
        var report = (DuplicateReport)result.Data!;
        // 1 组，3 个文件：浪费 = 1000 * (3-1) = 2000 bytes
        Assert.Equal(2000, report.WastedBytes);
    }

    [Fact]
    public async Task Find_NonExistentDirectory_Fails()
    {
        var result = await _tool.ExecuteAsync(JsonSerializer.Serialize(new
        {
            directory = Path.Combine(_root, "missing")
        }));
        Assert.False(result.Success);
        Assert.Contains("目录不存在", result.Summary);
    }

    [Fact]
    public async Task Find_EmptyDirectory_NoOp()
    {
        var result = await _tool.ExecuteAsync(JsonSerializer.Serialize(new { directory = _root }));
        Assert.True(result.Success);
        Assert.Equal(0, ((DuplicateReport)result.Data!).DuplicateGroups);
    }
}