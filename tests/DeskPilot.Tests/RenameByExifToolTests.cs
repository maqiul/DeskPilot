using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using DeskPilot.Core.Tools;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.17 A: RenameByExifTool 测试。覆盖 5 个场景：
///   1. EmptyInput：directory 为空 → 错误
///   2. NonExistentDirectory：目录不存在 → 错误
///   3. JpegWithExif：有 EXIF DateTimeOriginal 的 JPEG → 重命名成功 + 日期正确
///   4. JpegWithoutExif_Fallback：JPEG 无 EXIF → 用文件修改时间
///   5. DryRun_PreviewOnly：预览模式 → 不实际改名
/// </summary>
public class RenameByExifToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly RenameByExifTool _tool;

    public RenameByExifToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RenameByExifToolTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _tool = new RenameByExifTool();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { /* 忽略清理失败 */ }
    }

    /// <summary>
    /// 创建带 EXIF DateTimeOriginal 的 JPEG 图片。
    /// EXIF PropertyItem 0x9003 = DateTimeOriginal，格式 "YYYY:MM:DD HH:MM:SS"（19 字节 ASCII）。
    /// </summary>
    private string CreateJpegWithExif(string fileName, DateTime dateTimeOriginal)
    {
        var path = Path.Combine(_testDir, fileName);
        using var bmp = new Bitmap(200, 100);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Blue);
        bmp.Save(path, ImageFormat.Jpeg);

        // 写入 EXIF DateTimeOriginal（Image.Save 需要 path 参数）
        var bytes = File.ReadAllBytes(path);
        using var ms = new MemoryStream(bytes);
        using var img = Image.FromStream(ms);
        var dateBytes = Encoding.ASCII.GetBytes(dateTimeOriginal.ToString("yyyy:MM:dd HH:mm:ss") + "\0");
        var propItem = img.PropertyItems[0]; // 复制一个 PropertyItem 结构（含 Id/type/length）
        propItem.Id = 0x9003;
        propItem.Type = 2; // ASCII
        propItem.Len = dateBytes.Length;
        propItem.Value = dateBytes;
        img.SetPropertyItem(propItem);
        img.Save(path, ImageFormat.Jpeg); // 保存回原文件
        return path;
    }

    /// <summary>创建不带 EXIF 的 JPEG 图片（用于 fallback 测试）</summary>
    private string CreateJpegWithoutExif(string fileName)
    {
        var path = Path.Combine(_testDir, fileName);
        using var bmp = new Bitmap(200, 100);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Green);
        bmp.Save(path, ImageFormat.Jpeg);
        return path;
    }

    [Fact]
    public async Task EmptyInput_ReturnsError()
    {
        var args = JsonSerializer.Serialize(new { directory = "", pattern = "*.jpg" });
        var result = await _tool.ExecuteAsync(args);
        Assert.False(result.Success);
        Assert.Contains("目录不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task NonExistentDirectory_ReturnsError()
    {
        var args = JsonSerializer.Serialize(new { directory = @"C:\does\not\exist\rename_exif_test_" + Guid.NewGuid().ToString("N"), pattern = "*.jpg" });
        var result = await _tool.ExecuteAsync(args);
        Assert.False(result.Success);
        Assert.Contains("目录不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task JpegWithExif_RenamesToDateTimeOriginal()
    {
        // Arrange：创建带 EXIF 2024-06-15 14:30:00 的 JPEG
        var target = new DateTime(2024, 6, 15, 14, 30, 0);
        var input = CreateJpegWithExif("photo.jpg", target);

        // Act：用默认 dateFormat "yyyy-MM-dd_HH-mm-ss" 重命名
        var args = JsonSerializer.Serialize(new { directory = _testDir, pattern = "*.jpg" });
        var result = await _tool.ExecuteAsync(args);

        // Assert：成功 + 新文件名是 "2024-06-15_14-30-00.jpg"
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("改名", result.Summary);
        Assert.True(File.Exists(Path.Combine(_testDir, "2024-06-15_14-30-00.jpg")));
        Assert.False(File.Exists(input));

        var report = JsonSerializer.Deserialize<RenameByExifReport>(JsonSerializer.Serialize(result.Data));
        Assert.Equal(1, report!.Renamed);
        Assert.Equal(0, report.Failed);
        Assert.Equal(0, report.Skipped);
        Assert.Single(report.Details);
        Assert.Equal("EXIF", report.Details[0].DateSource);
        Assert.Equal(target, report.Details[0].ExifDate);
    }

    [Fact]
    public async Task JpegWithoutExif_UsesFileDateFallback()
    {
        // Arrange：创建无 EXIF 的 JPEG（FileDate 为创建时刻）
        var input = CreateJpegWithoutExif("noexif.jpg");
        var expectedFileDate = File.GetLastWriteTime(input);

        // Act：fallbackToFileDate=true（默认）
        var args = JsonSerializer.Serialize(new { directory = _testDir, pattern = "*.jpg" });
        var result = await _tool.ExecuteAsync(args);

        // Assert：成功 + 新文件名用文件修改时间
        Assert.True(result.Success, result.ErrorMessage);

        var report = JsonSerializer.Deserialize<RenameByExifReport>(JsonSerializer.Serialize(result.Data));
        Assert.Equal(1, report!.Renamed);
        Assert.Equal("FileDate", report.Details[0].DateSource);

        var newPath = Path.Combine(_testDir, expectedFileDate.ToString("yyyy-MM-dd_HH-mm-ss") + ".jpg");
        Assert.True(File.Exists(newPath), $"期望文件 {newPath} 存在");
    }

    [Fact]
    public async Task DryRun_PreviewOnly_DoesNotRename()
    {
        // Arrange：创建带 EXIF 的 JPEG
        var target = new DateTime(2024, 7, 1, 9, 0, 0);
        var input = CreateJpegWithExif("preview.jpg", target);

        // Act：dryRun=true
        var args = JsonSerializer.Serialize(new { directory = _testDir, pattern = "*.jpg", dryRun = true });
        var result = await _tool.ExecuteAsync(args);

        // Assert：成功 + 预览标记 + 原文件未被改名
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("预览", result.Summary);
        Assert.True(File.Exists(input), "原文件应保留");
        Assert.False(File.Exists(Path.Combine(_testDir, "2024-07-01_09-00-00.jpg")));

        var report = JsonSerializer.Deserialize<RenameByExifReport>(JsonSerializer.Serialize(result.Data));
        Assert.True(report!.DryRun);
        Assert.Equal(1, report.Renamed); // 计数 = 1（预览计数）
        Assert.Equal("WouldRename", report.Details[0].Status);
    }
}