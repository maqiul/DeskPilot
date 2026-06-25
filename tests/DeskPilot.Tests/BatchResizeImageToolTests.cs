using DeskPilot.Core.Tools;
using System.Drawing;
using Xunit;

namespace DeskPilot.Tests;

public class BatchResizeImageToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly BatchResizeImageTool _tool = new();

    public BatchResizeImageToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "resize_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    private string CreateTestImage(string name, int width, int height)
    {
        var path = Path.Combine(_testDir, name);
        using var bmp = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Red);
            g.DrawString(name, new Font("Arial", 12), Brushes.White, 0, 0);
        }
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
        return path;
    }

    [Fact]
    public async Task Resize_DirectoryNotExists_ReturnsFail()
    {
        var missing = Path.Combine(_testDir, "nonexistent");
        var json = string.Format("{{ \"directory\": \"{0}\", \"maxWidth\": 100, \"maxHeight\": 100 }}", missing.Replace("\\", "\\\\"));
        var result = await _tool.ExecuteAsync(json);
        Assert.False(result.Success);
        Assert.Contains("目录不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task Resize_InvalidMaxSize_ReturnsFail()
    {
        CreateTestImage("a.jpg", 800, 600);
        var dir = _testDir.Replace("\\", "\\\\");
        var json = string.Format("{{ \"directory\": \"{0}\", \"maxWidth\": 0, \"maxHeight\": 100 }}", dir);
        var result = await _tool.ExecuteAsync(json);
        Assert.False(result.Success);
        Assert.Contains("maxWidth/maxHeight", result.ErrorMessage);
    }

    [Fact]
    public async Task Resize_DryRun_DoesNotWriteFiles()
    {
        CreateTestImage("a.jpg", 2000, 1500);
        CreateTestImage("b.jpg", 800, 600);
        var dir = _testDir.Replace("\\", "\\\\");
        var json = string.Format("{{ \"directory\": \"{0}\", \"maxWidth\": 800, \"maxHeight\": 600, \"dryRun\": true }}", dir);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        Assert.Contains("预览", result.Summary);
        var report = Assert.IsType<ResizeReport>(result.Data);
        Assert.Equal(2, report.Scanned);
        Assert.Equal(0, report.Resized);
        Assert.Equal(2, report.WouldResize);
        Assert.False(File.Exists(Path.Combine(_testDir, "a_resized.jpg")));
    }

    [Fact]
    public async Task Resize_ActualRun_CreatesResizedFiles()
    {
        CreateTestImage("photo1.jpg", 2000, 1500);
        CreateTestImage("photo2.jpg", 1000, 800);
        var dir = _testDir.Replace("\\", "\\\\");
        var json = string.Format("{{ \"directory\": \"{0}\", \"maxWidth\": 800, \"maxHeight\": 600 }}", dir);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        Assert.Contains("✅", result.Summary);
        var report = Assert.IsType<ResizeReport>(result.Data);
        Assert.Equal(2, report.Scanned);
        Assert.Equal(2, report.Resized);
        Assert.Equal(0, report.Failed);
        Assert.True(File.Exists(Path.Combine(_testDir, "photo1_resized.jpg")));
        Assert.True(File.Exists(Path.Combine(_testDir, "photo2_resized.jpg")));
    }

    [Fact]
    public async Task Resize_SmallerThanMax_KeepsOriginalSize()
    {
        CreateTestImage("small.jpg", 400, 300);
        var dir = _testDir.Replace("\\", "\\\\");
        var json = string.Format("{{ \"directory\": \"{0}\", \"maxWidth\": 800, \"maxHeight\": 600 }}", dir);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var report = Assert.IsType<ResizeReport>(result.Data);
        var detail = report.Details.First();
        Assert.Equal(400, detail.NewWidth);
        Assert.Equal(300, detail.NewHeight);
    }

    [Fact]
    public async Task Resize_PatternFilter_OnlyProcessesMatchingFiles()
    {
        CreateTestImage("a.jpg", 2000, 1500);
        CreateTestImage("b.png", 2000, 1500);
        var dir = _testDir.Replace("\\", "\\\\");
        var json = string.Format("{{ \"directory\": \"{0}\", \"maxWidth\": 800, \"maxHeight\": 600, \"pattern\": \"*.jpg\" }}", dir);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var report = Assert.IsType<ResizeReport>(result.Data);
        Assert.Equal(1, report.Scanned);
        Assert.Equal(1, report.Resized);
        Assert.True(File.Exists(Path.Combine(_testDir, "a_resized.jpg")));
        Assert.False(File.Exists(Path.Combine(_testDir, "b_resized.png")));
    }

    [Fact]
    public async Task Resize_RunTwice_SkipsAlreadyResized()
    {
        CreateTestImage("a.jpg", 2000, 1500);
        var dir = _testDir.Replace("\\", "\\\\");
        var json = string.Format("{{ \"directory\": \"{0}\", \"maxWidth\": 800, \"maxHeight\": 600 }}", dir);
        await _tool.ExecuteAsync(json);
        // 第二次：扫描到 a.jpg + a_resized.jpg 两个文件
        // a.jpg 重新处理，a_resized.jpg 因已带后缀被 skip
        var result2 = await _tool.ExecuteAsync(json);
        Assert.True(result2.Success);
        var report = Assert.IsType<ResizeReport>(result2.Data);
        Assert.Equal(2, report.Scanned);
        Assert.Equal(1, report.Resized);
        Assert.Equal(1, report.Skipped);
    }
}