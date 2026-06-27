using System.Drawing;
using System.Text.Json;
using DeskPilot.Core.Tools;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.16 B: CropImageTool 测试。覆盖 4 个场景：
///   1. EmptyInput：inputPath 为空 → 错误
///   2. NonExistentFile：文件不存在 → 错误
///   3. ValidCrop：400x300 源图裁剪 (50, 50, 200, 100) → 200x100 输出
///   4. OutOfBoundsCrop：自动截断到源图片边界
/// </summary>
public class CropImageToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly CropImageTool _tool;

    public CropImageToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "CropImageToolTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _tool = new CropImageTool();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { /* 忽略清理失败 */ }
    }

    /// <summary>用 System.Drawing 创建一个指定尺寸的测试 PNG 图片</summary>
    private string CreateTestImage(string fileName, int width = 400, int height = 300)
    {
        var path = Path.Combine(_testDir, fileName);
        using var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Blue);
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }

    [Fact]
    public async Task EmptyInput_ReturnsError()
    {
        var outPath = Path.Combine(_testDir, "out_empty.png");
        var args = JsonSerializer.Serialize(new { inputPath = "", outputPath = outPath, x = 0, y = 0, width = 100, height = 100 });
        var result = await _tool.ExecuteAsync(args);
        Assert.False(result.Success);
        Assert.Contains("inputPath", result.ErrorMessage);
    }

    [Fact]
    public async Task NonExistentFile_ReturnsError()
    {
        var outPath = Path.Combine(_testDir, "out_noexist.png");
        var args = JsonSerializer.Serialize(new { inputPath = "C:\\does\\not\\exist.png", outputPath = outPath, x = 0, y = 0, width = 100, height = 100 });
        var result = await _tool.ExecuteAsync(args);
        Assert.False(result.Success);
        Assert.Contains("不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidCrop_ProducesCorrectSize()
    {
        var input = CreateTestImage("input.png", width: 400, height: 300);
        var outPath = Path.Combine(_testDir, "out_crop.png");
        var args = JsonSerializer.Serialize(new { inputPath = input, outputPath = outPath, x = 50, y = 50, width = 200, height = 100 });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("裁剪图片完成", result.Summary);
        Assert.Contains("200x100", result.Summary);
        Assert.True(File.Exists(outPath));

        // 验证输出图片尺寸
        using var verify = Image.FromFile(outPath);
        Assert.Equal(200, verify.Width);
        Assert.Equal(100, verify.Height);
    }

    [Fact]
    public async Task OutOfBoundsCrop_TruncatesToImageBounds()
    {
        var input = CreateTestImage("input.png", width: 400, height: 300);
        var outPath = Path.Combine(_testDir, "out_truncated.png");
        // 请求 (350, 250, 200, 100) → 实际只能裁 (350, 250, 50, 50)
        var args = JsonSerializer.Serialize(new { inputPath = input, outputPath = outPath, x = 350, y = 250, width = 200, height = 100 });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("已截断", result.Summary);
        Assert.Contains("50x50", result.Summary);

        // 验证输出图片尺寸（被截断）
        using var verify = Image.FromFile(outPath);
        Assert.Equal(50, verify.Width);
        Assert.Equal(50, verify.Height);
    }
}