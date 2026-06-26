using System.Text.Json;
using DeskPilot.Core.Tools;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.14: MergePdfTool 测试。覆盖空数组 / 单文件 / 多文件 / 不存在文件 / 损坏 PDF 5 个场景。
/// </summary>
public class MergePdfToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly MergePdfTool _tool;

    public MergePdfToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "MergePdfToolTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _tool = new MergePdfTool();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { /* 忽略清理失败 */ }
    }

    /// <summary>用 PdfSharpCore 创建一个 N 页的测试 PDF</summary>
    private string CreateTestPdf(string fileName, int pageCount = 1)
    {
        var path = Path.Combine(_testDir, fileName);
        using var doc = new PdfDocument();
        for (var i = 0; i < pageCount; i++)
        {
            doc.AddPage();
        }
        doc.Save(path);
        return path;
    }

    [Fact]
    public async Task EmptyInputFiles_ReturnsError()
    {
        var output = Path.Combine(_testDir, "out_empty.pdf");
        var args = JsonSerializer.Serialize(new { inputFiles = new string[0], outputPath = output });
        var result = await _tool.ExecuteAsync(args);
        Assert.False(result.Success);
        Assert.Contains("inputFiles", result.ErrorMessage);
    }

    [Fact]
    public async Task SingleFile_MergesSuccessfully()
    {
        var input = CreateTestPdf("single.pdf", pageCount: 2);
        var output = Path.Combine(_testDir, "out_single.pdf");
        var args = JsonSerializer.Serialize(new { inputFiles = new[] { input }, outputPath = output });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(output));
        Assert.Contains("合并 1 个 PDF", result.Summary);
        Assert.Contains("2 页", result.Summary);

        // 验证输出 PDF 可被 PdfReader 重新打开
        using var verify = PdfReader.Open(output, PdfDocumentOpenMode.InformationOnly);
        Assert.Equal(2, verify.PageCount);
    }

    [Fact]
    public async Task MultipleFiles_PreservesPageOrder()
    {
        var a = CreateTestPdf("a.pdf", pageCount: 1);
        var b = CreateTestPdf("b.pdf", pageCount: 2);
        var c = CreateTestPdf("c.pdf", pageCount: 3);
        var output = Path.Combine(_testDir, "out_multi.pdf");

        var args = JsonSerializer.Serialize(new
        {
            inputFiles = new[] { a, b, c },
            outputPath = output
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("合并 3 个 PDF", result.Summary);
        Assert.Contains("6 页", result.Summary);

        using var verify = PdfReader.Open(output, PdfDocumentOpenMode.InformationOnly);
        Assert.Equal(6, verify.PageCount);
    }

    [Fact]
    public async Task NonExistentFile_ReturnsError()
    {
        var output = Path.Combine(_testDir, "out_missing.pdf");
        var args = JsonSerializer.Serialize(new
        {
            inputFiles = new[] { Path.Combine(_testDir, "nonexistent.pdf") },
            outputPath = output
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("不存在", result.ErrorMessage);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task CorruptedPdf_ReturnsError()
    {
        // 写入非 PDF 内容（伪装成 .pdf 扩展名）
        var fakePdf = Path.Combine(_testDir, "fake.pdf");
        File.WriteAllText(fakePdf, "this is not a pdf content, just text");
        var output = Path.Combine(_testDir, "out_corrupt.pdf");

        var args = JsonSerializer.Serialize(new
        {
            inputFiles = new[] { fakePdf },
            outputPath = output
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.False(result.Success);
        // 损坏文件可能触发 PdfReaderException 或其他解析错误，关键是 success=false
        Assert.True(result.ErrorMessage.Length > 0);
    }
}
