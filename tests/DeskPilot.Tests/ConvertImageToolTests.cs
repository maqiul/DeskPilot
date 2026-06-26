using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using DeskPilot.Core.Tools;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.14: ConvertImageTool 测试。覆盖 PNG→JPG / JPG→PNG / 质量参数生效 / 不存在文件 / 不支持格式 5 个场景。
/// </summary>
public class ConvertImageToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly ConvertImageTool _tool;

    public ConvertImageToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ConvertImageToolTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _tool = new ConvertImageTool();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { /* 忽略清理失败 */ }
    }

    /// <summary>用 System.Drawing 创建一个 100x100 纯色 PNG 测试图</summary>
    private string CreateTestPng(string fileName, Color color)
    {
        var path = Path.Combine(_testDir, fileName);
        using var bmp = new Bitmap(100, 100);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(color);
        }
        bmp.Save(path, ImageFormat.Png);
        return path;
    }

    /// <summary>用 System.Drawing 创建一个 100x100 JPEG 测试图</summary>
    private string CreateTestJpg(string fileName, long quality = 90)
    {
        var path = Path.Combine(_testDir, fileName);
        using var bmp = new Bitmap(100, 100);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.LightBlue);
        }
        var jpegCodec = ImageCodecInfo.GetImageEncoders()
            .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
        bmp.Save(path, jpegCodec, parameters);
        return path;
    }

    [Fact]
    public async Task PngToJpg_Succeeds()
    {
        var input = CreateTestPng("source.png", Color.Red);
        var output = Path.Combine(_testDir, "converted.jpg");
        var args = JsonSerializer.Serialize(new
        {
            inputPath = input,
            outputPath = output,
            targetFormat = "jpg"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(output));
        // 验证文件头是 JPEG (FF D8 FF)
        var bytes = File.ReadAllBytes(output);
        Assert.True(bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "输出文件不是有效的 JPEG 格式");
        Assert.Contains("转换", result.Summary);
    }

    [Fact]
    public async Task JpgToPng_Succeeds()
    {
        var input = CreateTestJpg("source.jpg");
        var output = Path.Combine(_testDir, "converted.png");
        var args = JsonSerializer.Serialize(new
        {
            inputPath = input,
            outputPath = output,
            targetFormat = "png"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(output));
        // 验证文件头是 PNG (89 50 4E 47)
        var bytes = File.ReadAllBytes(output);
        Assert.True(bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
            "输出文件不是有效的 PNG 格式");
    }

    [Fact]
    public async Task Quality_AffectsJpgSize()
    {
        var input = CreateTestJpg("source.jpg", quality: 90);
        var lowQ = Path.Combine(_testDir, "low.jpg");
        var highQ = Path.Combine(_testDir, "high.jpg");

        var lowArgs = JsonSerializer.Serialize(new
        {
            inputPath = input,
            outputPath = lowQ,
            targetFormat = "jpg",
            quality = 10
        });
        var highArgs = JsonSerializer.Serialize(new
        {
            inputPath = input,
            outputPath = highQ,
            targetFormat = "jpg",
            quality = 95
        });

        var lowResult = await _tool.ExecuteAsync(lowArgs);
        var highResult = await _tool.ExecuteAsync(highArgs);

        Assert.True(lowResult.Success, lowResult.ErrorMessage);
        Assert.True(highResult.Success, highResult.ErrorMessage);

        var lowSize = new FileInfo(lowQ).Length;
        var highSize = new FileInfo(highQ).Length;
        Assert.True(highSize > lowSize, $"高质量应大于低质量（实际 high={highSize} low={lowSize}）");
    }

    [Fact]
    public async Task NonExistentFile_ReturnsError()
    {
        var output = Path.Combine(_testDir, "out.png");
        var args = JsonSerializer.Serialize(new
        {
            inputPath = Path.Combine(_testDir, "ghost.png"),
            outputPath = output,
            targetFormat = "png"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("不存在", result.ErrorMessage);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task UnsupportedFormat_ReturnsError()
    {
        var input = CreateTestPng("source.png", Color.Blue);
        var output = Path.Combine(_testDir, "out.tiff");
        var args = JsonSerializer.Serialize(new
        {
            inputPath = input,
            outputPath = output,
            targetFormat = "tiff"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("不支持", result.ErrorMessage);
    }
}
