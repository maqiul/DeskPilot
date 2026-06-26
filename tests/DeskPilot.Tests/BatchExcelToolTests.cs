using System.Text.Json;
using ClosedXML.Excel;
using DeskPilot.Core.Tools;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.14: BatchExcelTool 测试。覆盖空目录 / list_sheets 多文件 / extract_data / write_summary / 不存在目录 / 不支持 operation 6 个场景。
/// </summary>
public class BatchExcelToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly BatchExcelTool _tool;

    public BatchExcelToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "BatchExcelToolTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _tool = new BatchExcelTool();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { /* 忽略清理失败 */ }
    }

    /// <summary>用 ClosedXML 创建一个含若干行 × 列数据的 xlsx</summary>
    private string CreateTestXlsx(string fileName, params (string col1, string col2)[] rows)
    {
        var path = Path.Combine(_testDir, fileName);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Value";
        for (var i = 0; i < rows.Length; i++)
        {
            ws.Cell(i + 2, 1).Value = rows[i].col1;
            ws.Cell(i + 2, 2).Value = rows[i].col2;
        }
        wb.SaveAs(path);
        return path;
    }

    [Fact]
    public async Task EmptyDirectory_ReturnsEmptyResult()
    {
        var emptyDir = Path.Combine(_testDir, "empty");
        Directory.CreateDirectory(emptyDir);

        var args = JsonSerializer.Serialize(new
        {
            inputDirectory = emptyDir,
            operation = "list_sheets"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("没有匹配", result.Summary);

        // 验证 data 里的 fileCount = 0
        var data = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(result.Data));
        Assert.Equal(0, data.GetProperty("fileCount").GetInt32());
    }

    [Fact]
    public async Task ListSheets_MultipleFiles_ReturnsSheets()
    {
        CreateTestXlsx("a.xlsx", ("a1", "a2"));
        CreateTestXlsx("b.xlsx", ("b1", "b2"), ("b3", "b4"));

        var args = JsonSerializer.Serialize(new
        {
            inputDirectory = _testDir,
            operation = "list_sheets"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("处理 2 个文件", result.Summary);

        var data = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(result.Data));
        Assert.Equal(2, data.GetProperty("fileCount").GetInt32());
        var files = data.GetProperty("files");
        Assert.Equal(2, files.GetArrayLength());

        // 验证 b.xlsx 的 rowCount = 3（含表头 1 行 + 2 数据行）
        var bFile = files.EnumerateArray().First(f => f.GetProperty("fileName").GetString() == "b.xlsx");
        var bSheets = bFile.GetProperty("sheets");
        Assert.Equal(1, bSheets.GetArrayLength());
        Assert.Equal("Sheet1", bSheets[0].GetProperty("name").GetString());
        Assert.Equal(3, bSheets[0].GetProperty("rowCount").GetInt32());
    }

    [Fact]
    public async Task ExtractData_AggregatesAllRows()
    {
        CreateTestXlsx("a.xlsx", ("x", "1"), ("y", "2"));
        CreateTestXlsx("b.xlsx", ("z", "3"));

        var args = JsonSerializer.Serialize(new
        {
            inputDirectory = _testDir,
            operation = "extract_data"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);

        var data = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(result.Data));
        Assert.Equal(2, data.GetProperty("fileCount").GetInt32());
        // a.xlsx 2 数据行 + b.xlsx 1 数据行 = 3
        Assert.Equal(3, data.GetProperty("totalRows").GetInt32());
    }

    [Fact]
    public async Task WriteSummary_CreatesOutputXlsx()
    {
        CreateTestXlsx("a.xlsx", ("x", "1"), ("y", "2"));
        CreateTestXlsx("b.xlsx", ("z", "3"));
        var output = Path.Combine(_testDir, "summary.xlsx");

        var args = JsonSerializer.Serialize(new
        {
            inputDirectory = _testDir,
            operation = "write_summary",
            outputPath = output
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(output));

        // 验证输出 xlsx 可被读取
        using var verifyWb = new XLWorkbook(output);
        var verifyWs = verifyWb.Worksheets.First();
        Assert.Equal("Summary", verifyWs.Name);
        Assert.Equal("FileName", verifyWs.Cell(1, 1).GetString());
        // a.xlsx 1 sheet + b.xlsx 1 sheet = 2 数据行，加上 Summary 表头行 = 3
        Assert.Equal(3, verifyWs.LastRowUsed()?.RowNumber());
    }

    [Fact]
    public async Task NonExistentDirectory_ReturnsError()
    {
        var args = JsonSerializer.Serialize(new
        {
            inputDirectory = Path.Combine(_testDir, "ghost"),
            operation = "list_sheets"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task UnsupportedOperation_ReturnsError()
    {
        var args = JsonSerializer.Serialize(new
        {
            inputDirectory = _testDir,
            operation = "delete_everything"
        });
        var result = await _tool.ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("不支持", result.ErrorMessage);
    }
}
