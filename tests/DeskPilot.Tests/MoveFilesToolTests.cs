using System.Text.Json;
using DeskPilot.Core.Tools;

namespace DeskPilot.Tests;

public sealed class MoveFilesToolTests : IDisposable
{
    private readonly string _root;
    private readonly MoveFilesTool _tool = new();

    public MoveFilesToolTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"deskpilot_move_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { }
    }

    private string CreateFile(string name, string content = "x")
    {
        var p = Path.Combine(_root, name);
        File.WriteAllText(p, content);
        return p;
    }

    [Fact]
    public async Task Move_AllFiles_ToTargetDir()
    {
        CreateFile("a.txt");
        CreateFile("b.txt");
        CreateFile("c.txt");
        var target = Path.Combine(_root, "target");
        var args = JsonSerializer.Serialize(new { sourceDirectory = _root, targetDirectory = target });

        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(3, ((MoveReport)result.Data!).Moved);
        Assert.Equal(0, ((MoveReport)result.Data).Failed);
        Assert.True(File.Exists(Path.Combine(target, "a.txt")));
        Assert.False(File.Exists(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public async Task Move_WithPatternFilter_OnlyMovesMatching()
    {
        CreateFile("a.pdf");
        CreateFile("b.jpg");
        CreateFile("c.pdf");
        var target = Path.Combine(_root, "pdfs");
        var args = JsonSerializer.Serialize(new
        {
            sourceDirectory = _root,
            targetDirectory = target,
            pattern = "*.pdf"
        });

        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Equal(2, ((MoveReport)result.Data!).Moved);
        Assert.True(File.Exists(Path.Combine(target, "a.pdf")));
        Assert.True(File.Exists(Path.Combine(_root, "b.jpg"))); // 没被移
    }

    [Fact]
    public async Task Move_AutoCreatesTarget_WhenMissing()
    {
        CreateFile("a.txt");
        var target = Path.Combine(_root, "deep", "nested", "dir");
        var args = JsonSerializer.Serialize(new { sourceDirectory = _root, targetDirectory = target });

        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.True(Directory.Exists(target));
        Assert.True(File.Exists(Path.Combine(target, "a.txt")));
    }

    [Fact]
    public async Task Move_NamingCollision_AddsSuffix()
    {
        CreateFile("a.txt");
        var target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "a.txt"), "existing");
        var args = JsonSerializer.Serialize(new { sourceDirectory = _root, targetDirectory = target });

        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(target, "a.txt")));
        Assert.Equal("existing", File.ReadAllText(Path.Combine(target, "a.txt")));
        Assert.True(File.Exists(Path.Combine(target, "a_2.txt")));
    }

    [Fact]
    public async Task Move_NonExistentSource_Fails()
    {
        var args = JsonSerializer.Serialize(new
        {
            sourceDirectory = Path.Combine(_root, "missing"),
            targetDirectory = Path.Combine(_root, "target")
        });
        var result = await _tool.ExecuteAsync(args);
        Assert.False(result.Success);
        Assert.Contains("源目录不存在", result.Summary);
    }

    [Fact]
    public async Task Move_NonExistentTarget_WithoutCreateFlag_Fails()
    {
        CreateFile("a.txt");
        var args = JsonSerializer.Serialize(new
        {
            sourceDirectory = _root,
            targetDirectory = Path.Combine(_root, "missing"),
            createIfMissing = false
        });
        var result = await _tool.ExecuteAsync(args);
        Assert.False(result.Success);
        Assert.Contains("目标目录不存在", result.Summary);
    }

    [Fact]
    public async Task Move_EmptyDirectory_NoOp()
    {
        var target = Path.Combine(_root, "target");
        var args = JsonSerializer.Serialize(new { sourceDirectory = _root, targetDirectory = target });
        var result = await _tool.ExecuteAsync(args);
        Assert.True(result.Success);
        Assert.Equal(0, ((MoveReport)result.Data!).Moved);
    }
}