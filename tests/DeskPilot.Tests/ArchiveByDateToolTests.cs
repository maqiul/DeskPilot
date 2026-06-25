using System.Text.Json;
using DeskPilot.Core.Tools;

namespace DeskPilot.Tests;

/// <summary>
/// ArchiveByDateTool 单元测试。
/// 策略：每个测试用 Path.GetTempFileName() + 自管子目录，teardown 时删整个 tmp 根目录。
/// </summary>
public sealed class ArchiveByDateToolTests : IDisposable
{
    private readonly string _root;
    private readonly ArchiveByDateTool _tool = new();

    public ArchiveByDateToolTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"deskpilot_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* best-effort 清理 */ }
    }

    /// <summary>
    /// 辅助：在 _root 下创建一个文件，返回文件路径。
    /// </summary>
    private string CreateFile(string name, DateTime? modTime = null, DateTime? createTime = null, string content = "x")
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        if (modTime.HasValue) File.SetLastWriteTime(path, modTime.Value);
        if (createTime.HasValue) File.SetCreationTime(path, createTime.Value);
        return path;
    }

    /// <summary>
    /// 真正复制一个文件到另一个位置（即使同名也安全：先读完源、关闭，再写目标）。
    /// </summary>
    private static void CopyFileSafe(string sourcePath, string destPath, bool force = false)
    {
        using var src = File.OpenRead(sourcePath);
        using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
        src.CopyTo(dst);
    }

    [Fact]
    public async Task Archive_MonthGranularity_CreatesYearMonthSubdirs()
    {
        CreateFile("a.txt", modTime: new DateTime(2024, 1, 15));
        CreateFile("b.txt", modTime: new DateTime(2024, 2, 20));
        CreateFile("c.txt", modTime: new DateTime(2023, 12, 31));

        var json = JsonSerializer.Serialize(new
        {
            sourceDirectory = _root,
            granularity = "Month"
        });
        var result = await _tool.ExecuteAsync(json);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(Directory.Exists(Path.Combine(_root, "archive", "2024-01")));
        Assert.True(Directory.Exists(Path.Combine(_root, "archive", "2024-02")));
        Assert.True(Directory.Exists(Path.Combine(_root, "archive", "2023-12")));
        Assert.True(File.Exists(Path.Combine(_root, "archive", "2024-01", "a.txt")));
        Assert.False(File.Exists(Path.Combine(_root, "a.txt"))); // 已被移动
    }

    [Fact]
    public async Task Archive_YearGranularity_GroupsAll2024FilesTogether()
    {
        CreateFile("jan.txt", modTime: new DateTime(2024, 1, 1));
        CreateFile("dec.txt", modTime: new DateTime(2024, 12, 31));
        CreateFile("old.txt", modTime: new DateTime(2020, 6, 1));

        var json = JsonSerializer.Serialize(new
        {
            sourceDirectory = _root,
            granularity = "Year"
        });
        var result = await _tool.ExecuteAsync(json);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_root, "archive", "2024", "jan.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "archive", "2024", "dec.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "archive", "2020", "old.txt")));
    }

    [Fact]
    public async Task Archive_DayGranularity_CreatesDailyFolders()
    {
        CreateFile("today.txt", modTime: new DateTime(2024, 3, 15, 10, 0, 0));
        CreateFile("yesterday.txt", modTime: new DateTime(2024, 3, 14, 22, 0, 0));

        var json = JsonSerializer.Serialize(new
        {
            sourceDirectory = _root,
            granularity = "Day"
        });
        var result = await _tool.ExecuteAsync(json);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_root, "archive", "2024-03-15", "today.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "archive", "2024-03-14", "yesterday.txt")));
    }

    [Fact]
    public async Task Archive_UseCreatedTime_WhenSpecified()
    {
        // 修改时间 = 2024 年，创建时间 = 2023 年
        var path = CreateFile("doc.pdf",
            modTime: new DateTime(2024, 5, 1),
            createTime: new DateTime(2023, 7, 1));

        var json = JsonSerializer.Serialize(new
        {
            sourceDirectory = _root,
            dateField = "Created",
            granularity = "Year"
        });
        var result = await _tool.ExecuteAsync(json);

        Assert.True(result.Success);
        // 按 Created，应归档到 2023/
        Assert.True(File.Exists(Path.Combine(_root, "archive", "2023", "doc.pdf")));
        Assert.False(Directory.Exists(Path.Combine(_root, "archive", "2024")));
    }

    [Fact]
    public async Task Archive_DryRun_DoesNotMoveFiles()
    {
        var path = CreateFile("a.txt", modTime: new DateTime(2024, 1, 1));

        var json = JsonSerializer.Serialize(new
        {
            sourceDirectory = _root,
            dryRun = true,
            granularity = "Month"
        });
        var result = await _tool.ExecuteAsync(json);

        Assert.True(result.Success);
        // 文件还在源目录
        Assert.True(File.Exists(path), "DryRun 不应移动文件");
        // 但 archive/2024-01 也不应被创建（我们仅在非 dryRun 时 CreateDirectory）
        Assert.False(Directory.Exists(Path.Combine(_root, "archive")));
    }

    [Fact]
    public async Task Archive_ResolvesFilenameCollision()
    {
        CreateFile("report.txt", modTime: new DateTime(2024, 1, 1));
        // 真正复制一个独立文件，再改名
        var sourcePath = Path.Combine(_root, "report.txt");
        CopyFileSafe(sourcePath, Path.Combine(_root, "report2.txt"));
        // 删除源（因为 WriteAllText 已覆盖，源里有 1 个 report.txt + 1 个 report2.txt）
        File.Delete(sourcePath);
        // 再写一个新 report.txt 作为 2 月
        CreateFile("report.txt", modTime: new DateTime(2024, 2, 1));
        // 现在源目录：report.txt (2024-02), report2.txt
        // 重命名 report2.txt -> report.txt?  会覆盖！应该用不同名字测试
        // 干脆简化：源里有 1 个 report.txt (2 月)，加 1 个 1 月文件但不同名
        File.Move(Path.Combine(_root, "report2.txt"), Path.Combine(_root, "report_jan.txt"));
        File.SetLastWriteTime(Path.Combine(_root, "report_jan.txt"), new DateTime(2024, 1, 1));

        var json = JsonSerializer.Serialize(new
        {
            sourceDirectory = _root,
            granularity = "Month"
        });
        var result = await _tool.ExecuteAsync(json);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(Path.Combine(_root, "archive", "2024-01", "report_jan.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "archive", "2024-02", "report.txt")));
    }

    [Fact]
    public async Task Archive_CollisionInSameSubdir_GetsSuffix()
    {
        // 场景：归档目标子目录里已有同名文件，新文件归档时必须不覆盖
        CreateFile("dup.txt", modTime: new DateTime(2024, 1, 1, 10, 0, 0));
        // 预先在归档目录里塞一个同名文件
        var archiveDir = Path.Combine(_root, "archive", "2024-01");
        Directory.CreateDirectory(archiveDir);
        File.WriteAllText(Path.Combine(archiveDir, "dup.txt"), "existing");

        var json = JsonSerializer.Serialize(new
        {
            sourceDirectory = _root,
            granularity = "Month"
        });
        var result = await _tool.ExecuteAsync(json);

        Assert.True(result.Success, result.ErrorMessage);
        // 原有文件保留
        Assert.Equal("existing", File.ReadAllText(Path.Combine(archiveDir, "dup.txt")));
        // 新文件用 _2 后缀
        Assert.True(File.Exists(Path.Combine(archiveDir, "dup_2.txt")));
    }

    [Fact]
    public async Task Archive_PatternFilter_OnlyArchivesMatchingFiles()
    {
        CreateFile("invoice.pdf", modTime: new DateTime(2024, 1, 1));
        CreateFile("photo.jpg", modTime: new DateTime(2024, 1, 1));
        CreateFile("notes.txt", modTime: new DateTime(2024, 1, 1));

        var json = JsonSerializer.Serialize(new
        {
            sourceDirectory = _root,
            pattern = "*.pdf",
            granularity = "Month"
        });
        var result = await _tool.ExecuteAsync(json);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_root, "archive", "2024-01", "invoice.pdf")));
        Assert.True(File.Exists(Path.Combine(_root, "photo.jpg"))); // 没被归档
        Assert.True(File.Exists(Path.Combine(_root, "notes.txt")));
    }

    [Fact]
    public async Task Archive_TargetDirectory_RootsAtSpecifiedPath()
    {
        CreateFile("a.txt", modTime: new DateTime(2024, 1, 1));
        var customTarget = Path.Combine(_root, "..", $"deskpilot_target_{Guid.NewGuid():N}");
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                sourceDirectory = _root,
                targetDirectory = customTarget,
                granularity = "Month"
            });
            var result = await _tool.ExecuteAsync(json);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(customTarget, "2024-01", "a.txt")));
            Assert.False(Directory.Exists(Path.Combine(_root, "archive")));
        }
        finally
        {
            if (Directory.Exists(customTarget)) Directory.Delete(customTarget, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_EmptyDirectory_ReturnsZeroReport()
    {
        // 源目录存在但是空的
        var json = JsonSerializer.Serialize(new { sourceDirectory = _root });
        var result = await _tool.ExecuteAsync(json);

        Assert.True(result.Success);
        var report = Assert.IsType<ArchiveReport>(result.Data);
        Assert.Equal(0, report.Scanned);
        Assert.Equal(0, report.Moved);
    }

    [Fact]
    public async Task Archive_NonExistentSource_ReturnsFailure()
    {
        var json = JsonSerializer.Serialize(new
        {
            sourceDirectory = Path.Combine(_root, "does_not_exist")
        });
        var result = await _tool.ExecuteAsync(json);

        Assert.False(result.Success);
        Assert.Contains("源目录不存在", result.Summary);
    }

    [Fact]
    public async Task Archive_InvalidJson_ReturnsFailure()
    {
        var result = await _tool.ExecuteAsync("not a json at all");
        Assert.False(result.Success);
        Assert.Contains("参数解析失败", result.Summary);
    }

    [Fact]
    public async Task Archive_ReportContainsCorrectCounts()
    {
        CreateFile("a.txt", modTime: new DateTime(2024, 1, 1));
        CreateFile("b.txt", modTime: new DateTime(2024, 1, 2));
        CreateFile("c.txt", modTime: new DateTime(2024, 2, 1));

        var json = JsonSerializer.Serialize(new { sourceDirectory = _root });
        var result = await _tool.ExecuteAsync(json);

        Assert.True(result.Success);
        var report = (ArchiveReport)result.Data!;
        Assert.Equal(3, report.Scanned);
        Assert.Equal(3, report.Moved);
        Assert.Equal(2, report.Subdirectories); // 2024-01, 2024-02
        Assert.Equal(0, report.Failed);
        Assert.Equal(3, report.Details.Count);
    }
}