using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;

namespace DeskPilot.Core.Tools;

/// <summary>
/// Excel 批处理工具：对指定目录下的 xlsx 批量执行三种操作之一。
/// 使用 ClosedXML 库（纯 .NET，无需安装 Excel）。
///
/// 支持的 operation：
///   - list_sheets：列出每个 xlsx 的 sheet 名 + 行/列数 + 文件大小
///   - extract_data：汇总所有 xlsx 第一张表的数据到 JSON 数组
///   - write_summary：把每文件的「文件名 + sheet 名 + 行数 + 列数」汇总到 outputPath 新 xlsx
///
/// AI 调用示例：
/// {
///   "inputDirectory": "C:\\data",
///   "fileFilter": "*.xlsx",
///   "operation": "list_sheets"
/// }
/// </summary>
public sealed class BatchExcelTool : ITool
{
    public RiskLevel Risk => RiskLevel.Destructive;  // write_summary 写新 xlsx

    public string Name => "batch_excel";
    public string Description =>
        "对指定目录下的 xlsx 文件批量执行三种操作之一。" +
        "list_sheets：列出每个 xlsx 的 sheet 名 + 行/列数 + 文件大小。" +
        "extract_data：汇总所有 xlsx 第一张表的数据到 JSON 数组（仅处理可序列化内容）。" +
        "write_summary：把每文件的「文件名 + sheet 名 + 行数 + 列数」汇总到 outputPath 新 xlsx。" +
        "使用 ClosedXML（纯 .NET，无需安装 Excel）。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "inputDirectory": { "type": "string", "description": "输入目录绝对路径" },
            "fileFilter": { "type": "string", "description": "文件名 glob，默认 *.xlsx" },
            "operation": { "type": "string", "description": "list_sheets | extract_data | write_summary" },
            "outputPath": { "type": "string", "description": "write_summary 必填：汇总 xlsx 输出路径" }
          },
          "required": ["inputDirectory", "operation"]
        }
        """;

    private static readonly string[] SupportedOps = { "list_sheets", "extract_data", "write_summary" };

    [Microsoft.SemanticKernel.KernelFunction("batch_excel")]
    public async Task<string> BatchKernelAsync(
        string inputDirectory,
        string operation,
        string? fileFilter = null,
        string? outputPath = null)
    {
        var args = JsonSerializer.Serialize(new
        {
            inputDirectory,
            fileFilter = fileFilter ?? "*.xlsx",
            operation,
            outputPath
        });
        var result = await ExecuteAsync(args).ConfigureAwait(false);
        return JsonSerializer.Serialize(new
        {
            success = result.Success,
            summary = result.Summary,
            error = result.ErrorMessage,
            data = result.Data
        });
    }

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct = default)
    {
        BatchArgs args;
        try { args = JsonSerializer.Deserialize<BatchArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (string.IsNullOrWhiteSpace(args.InputDirectory))
            return ToolResult.Fail("inputDirectory 不能为空");
        if (!Directory.Exists(args.InputDirectory))
            return ToolResult.Fail($"输入目录不存在：{args.InputDirectory}");

        var op = (args.Operation ?? "").ToLowerInvariant();
        if (Array.IndexOf(SupportedOps, op) < 0)
            return ToolResult.Fail($"不支持的 operation：{args.Operation}（支持：{string.Join(", ", SupportedOps)}）");

        if (op == "write_summary" && string.IsNullOrWhiteSpace(args.OutputPath))
            return ToolResult.Fail("write_summary 必须提供 outputPath");

        var filter = string.IsNullOrWhiteSpace(args.FileFilter) ? "*.xlsx" : args.FileFilter;
        var files = Directory.GetFiles(args.InputDirectory, filter, SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
            return ToolResult.Ok($"目录 {args.InputDirectory} 下没有匹配 {filter} 的文件", new
            {
                fileCount = 0,
                operation = op,
                files = Array.Empty<object>()
            });

        try
        {
            var sw = Stopwatch.StartNew();

            var resultData = op switch
            {
                "list_sheets" => ListSheets(files),
                "extract_data" => ExtractData(files),
                "write_summary" => WriteSummary(files, args.OutputPath!),
                _ => throw new InvalidOperationException("unreachable")
            };

            sw.Stop();
            return ToolResult.Ok(
                $"📊 {op} 完毕：处理 {files.Length} 个文件（{sw.ElapsedMilliseconds}ms）",
                resultData);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"Excel 批处理失败：{ex.Message}");
        }
    }

    /// <summary>list_sheets：列出每个 xlsx 的 sheet 名 + 行列数</summary>
    private static object ListSheets(string[] files)
    {
        var sheetsList = new List<object>();
        foreach (var file in files)
        {
            using var wb = new XLWorkbook(file);
            var fileInfo = new FileInfo(file);
            var sheets = wb.Worksheets.Select(ws => new
            {
                name = ws.Name,
                rowCount = ws.LastRowUsed()?.RowNumber() ?? 0,
                colCount = ws.LastColumnUsed()?.ColumnNumber() ?? 0
            }).ToList();
            sheetsList.Add(new
            {
                fileName = Path.GetFileName(file),
                filePath = file,
                fileSizeBytes = fileInfo.Length,
                sheetCount = sheets.Count,
                sheets
            });
        }
        return new { fileCount = files.Length, files = sheetsList };
    }

    /// <summary>extract_data：汇总所有 xlsx 第一张表的数据到 JSON 数组</summary>
    private static object ExtractData(string[] files)
    {
        var rows = new List<object>();
        foreach (var file in files)
        {
            using var wb = new XLWorkbook(file);
            var ws = wb.Worksheets.FirstOrDefault();
            if (ws == null) continue;
            var used = ws.RangeUsed();
            if (used == null) continue;
            // 跳过表头行（第一行）— 只汇总数据行
            var dataRows = used.RowsUsed().Skip(1).ToList();
            foreach (var row in dataRows)
            {
                var cells = row.CellsUsed()
                    .Select(c => c.Value.ToString() ?? "")
                    .ToArray();
                rows.Add(new
                {
                    source = Path.GetFileName(file),
                    sheet = ws.Name,
                    row = row.RowNumber(),
                    values = cells
                });
            }
        }
        return new { fileCount = files.Length, totalRows = rows.Count, rows };
    }

    /// <summary>write_summary：把每文件的「文件名 + sheet 名 + 行数 + 列数」汇总到新 xlsx</summary>
    private static object WriteSummary(string[] files, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Summary");

        // 表头
        ws.Cell(1, 1).Value = "FileName";
        ws.Cell(1, 2).Value = "SheetName";
        ws.Cell(1, 3).Value = "RowCount";
        ws.Cell(1, 4).Value = "ColCount";
        ws.Cell(1, 5).Value = "FileSizeBytes";
        ws.Range(1, 1, 1, 5).Style.Font.Bold = true;

        var rowIdx = 2;
        foreach (var file in files)
        {
            using var srcWb = new XLWorkbook(file);
            var fileInfo = new FileInfo(file);
            foreach (var srcWs in srcWb.Worksheets)
            {
                ws.Cell(rowIdx, 1).Value = Path.GetFileName(file);
                ws.Cell(rowIdx, 2).Value = srcWs.Name;
                ws.Cell(rowIdx, 3).Value = srcWs.LastRowUsed()?.RowNumber() ?? 0;
                ws.Cell(rowIdx, 4).Value = srcWs.LastColumnUsed()?.ColumnNumber() ?? 0;
                ws.Cell(rowIdx, 5).Value = fileInfo.Length;
                rowIdx++;
            }
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(outputPath);

        return new
        {
            fileCount = files.Length,
            totalSheetRows = rowIdx - 2,
            outputPath,
            outputSizeBytes = new FileInfo(outputPath).Length
        };
    }

    private sealed record BatchArgs(string InputDirectory, string? FileFilter, string? Operation, string? OutputPath);
}
