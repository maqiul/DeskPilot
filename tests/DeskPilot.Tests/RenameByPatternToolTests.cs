using System.Text.Json;
using DeskPilot.Core.Tools;

namespace DeskPilot.Tests;

public sealed class RenameByPatternToolTests : IDisposable
{
    private readonly string _root;
    private readonly RenameByPatternTool _tool = new();

    public RenameByPatternToolTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"deskpilot_rename_{Guid.NewGuid():N}");
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
    public async Task Rename_RegexReplacement()
    {
        CreateFile("IMG_001.jpg");
        CreateFile("IMG_002.jpg");
        CreateFile("IMG_003.jpg");

        var args = JsonSerializer.Serialize(new
        {
            directory = _root,
            find = "IMG_",
            replace = "photo_"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(3, ((RenameReport)result.Data!).Renamed);
        Assert.True(File.Exists(Path.Combine(_root, "photo_001.jpg")));
        Assert.True(File.Exists(Path.Combine(_root, "photo_002.jpg")));
        Assert.True(File.Exists(Path.Combine(_root, "photo_003.jpg")));
        Assert.False(File.Exists(Path.Combine(_root, "IMG_001.jpg")));
    }

    [Fact]
    public async Task Rename_RegexWithCaptureGroup()
    {
        CreateFile("file_abc_123.txt");
        var args = JsonSerializer.Serialize(new
        {
            directory = _root,
            find = "file_(\\w+)_(\\d+)",
            replace = "$2_$1"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_root, "123_abc.txt")));
    }

    [Fact]
    public async Task Rename_AddPrefix()
    {
        CreateFile("a.txt");
        CreateFile("b.txt");

        var args = JsonSerializer.Serialize(new { directory = _root, prefix = "2024_" });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_root, "2024_a.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "2024_b.txt")));
    }

    [Fact]
    public async Task Rename_AddSuffix_BeforeExtension()
    {
        CreateFile("report.pdf");
        var args = JsonSerializer.Serialize(new { directory = _root, suffix = "_backup" });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success);
        // 后缀加在扩展名前
        Assert.True(File.Exists(Path.Combine(_root, "report_backup.pdf")));
    }

    [Fact]
    public async Task Rename_PrefixAndRegex_Combined()
    {
        CreateFile("IMG_001.jpg");
        var args = JsonSerializer.Serialize(new
        {
            directory = _root,
            prefix = "vacation_",
            find = "IMG_",
            replace = ""
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success);
        // prefix 先加？还是 regex 先执行？当前实现：先 regex 再 prefix
        Assert.True(File.Exists(Path.Combine(_root, "vacation_001.jpg")));
    }

    [Fact]
    public async Task Rename_DryRun_DoesNotRename()
    {
        CreateFile("a.txt");
        CreateFile("b.txt");
        var args = JsonSerializer.Serialize(new
        {
            directory = _root,
            prefix = "x_",
            dryRun = true
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.Equal(2, ((RenameReport)result.Data!).Renamed); // 报告里有 2 个
        // 但文件还在原位
        Assert.True(File.Exists(Path.Combine(_root, "a.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "b.txt")));
        Assert.False(File.Exists(Path.Combine(_root, "x_a.txt")));
    }

    [Fact]
    public async Task Rename_Collision_AddsSuffix()
    {
        // 制造 conflict：把 b.txt 通过 prefix 改名为和现有 x_a.txt 冲突
        // 简化：用 find 替换把 b 改为 x_a，触发 collision
        File.WriteAllText(Path.Combine(_root, "x_a.txt"), "original");
        CreateFile("b.txt", "content");

        var args = JsonSerializer.Serialize(new
        {
            directory = _root,
            find = "^b$",
            replace = "x_a"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        // 原来的 x_a.txt 没被覆盖
        Assert.Equal("original", File.ReadAllText(Path.Combine(_root, "x_a.txt")));
        // b.txt 被改名为 x_a_2.txt（collision 后缀）
        Assert.True(File.Exists(Path.Combine(_root, "x_a_2.txt")));
    }

    [Fact]
    public async Task Rename_NoArgs_ReturnsFailure()
    {
        CreateFile("a.txt");
        var args = JsonSerializer.Serialize(new { directory = _root });
        var result = await _tool.ExecuteAsync(args);
        Assert.False(result.Success);
        Assert.Contains("必须至少指定一个", result.Summary);
    }

    [Fact]
    public async Task Rename_InvalidRegex_ReturnsFailure()
    {
        CreateFile("a.txt");
        var args = JsonSerializer.Serialize(new
        {
            directory = _root,
            find = "[invalid("
        });
        var result = await _tool.ExecuteAsync(args);
        Assert.False(result.Success);
        Assert.Contains("正则表达式无效", result.Summary);
    }

    [Fact]
    public async Task Rename_PatternFilter_OnlyRenamesMatching()
    {
        CreateFile("IMG_001.jpg");
        CreateFile("notes.txt");

        var args = JsonSerializer.Serialize(new
        {
            directory = _root,
            pattern = "*.jpg",
            find = "IMG_",
            replace = "photo_"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_root, "photo_001.jpg")));
        Assert.True(File.Exists(Path.Combine(_root, "notes.txt"))); // 没被改
    }

    [Fact]
    public async Task Rename_NonExistentDirectory_Fails()
    {
        var args = JsonSerializer.Serialize(new
        {
            directory = Path.Combine(_root, "missing"),
            prefix = "x_"
        });
        var result = await _tool.ExecuteAsync(args);
        Assert.False(result.Success);
    }
}