using System.Drawing;
using System.Text.Json;
using DeskPilot.Core.Tools;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.16 B: RotateImageTool 测试。覆盖 4 个场景：
///   1. EmptyInput：inputPath 为空 → 错误
///   2. NonExistentFile：文件不存在 → 错误
///   3. Rotate90：旋转 90 度 → 输出图片宽高交换
///   4. FlipHorizontal：水平翻转 → 输出图片尺寸不变
/// </summary>
public class RotateImageToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly RotateImageTool _tool;

    public RotateImageToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RotateImageToolTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _tool = new RotateImageTool();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { /* 忽略清理失败 */ }
    }

    /// <summary>用 System.Drawing 创建一个指定尺寸的测试 PNG 图片</summary>
    private string CreateTestImage(string fileName, int width = 200, int height = 100)
    {
        var path = Path.Combine(_testDir, fileName);
        using var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Red);
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }

    [Fact]
    public async Task EmptyInput_ReturnsError()
    {
        var outPath = Path.Combine(_testDir, "out_empty.png");
        var args = JsonSerializer.Serialize(new { inputPath = "", outputPath = outPath, rotation = 90 });
        var result = await _tool.ExecuteAsync(args);
        Assert.False(result.Success);
        Assert.Contains("inputPath", result.ErrorMessage);
    }

    [Fact]
    public async Task NonExistentFile_ReturnsError()
    {
        var outPath = Path.Combine(_testDir, "out_noexist.png");
        var args = JsonSerializer.Serialize(new { inputPath = "C:\\does\\not\\exist.png", outputPath = outPath, rotation = 90 });
        var result = await _tool.ExecuteAsync(args);
        Assert.False(result.Success);
        Assert.Contains("不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task Rotate90_SwapsWidthAndHeight()
    {
        var input = CreateTestImage("input.png", width: 200, height: 100);
        var outPath = Path.Combine(_testDir, "out_rot90.png");
        var args = JsonSerializer.Serialize(new { inputPath = input, outputPath = outPath, rotation = 90 });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("旋转 90°", result.Summary);
        Assert.True(File.Exists(outPath));

        // 验证输出图片宽高交换
        using var verify = Image.FromFile(outPath);
        Assert.Equal(100, verify.Width);
        Assert.Equal(200, verify.Height);
    }

    [Fact]
    public async Task FlipHorizontal_PreservesSize()
    {
        var input = CreateTestImage("input.png", width: 200, height: 100);
        var outPath = Path.Combine(_testDir, "out_fliph.png");
        var args = JsonSerializer.Serialize(new { inputPath = input, outputPath = outPath, rotation = 0, flip = "horizontal" });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("水平翻转", result.Summary);

        // 验证输出图片尺寸不变
        using var verify = Image.FromFile(outPath);
        Assert.Equal(200, verify.Width);
        Assert.Equal(100, verify.Height);
    }
}