using System.Diagnostics;
using System.Text.Json;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace DeskPilot.Core.Tools;

/// <summary>
/// PDF 合并工具：把多个 PDF 文件按顺序合并为一个新 PDF。
/// 使用 PdfSharpCore 库（纯托管代码，不依赖 GhostScript）。
///
/// AI 调用示例：
/// {
///   "inputFiles": ["C:\\a.pdf", "C:\\b.pdf"],
///   "outputPath": "C:\\merged.pdf"
/// }
/// </summary>
public sealed class MergePdfTool : ITool
{
    public RiskLevel Risk => RiskLevel.Destructive;  // 写新文件

    public string Name => "merge_pdfs";
    public string Description =>
        "把多个 PDF 文件按顺序合并为一个新 PDF。" +
        "使用 PdfSharpCore（纯 .NET 实现，不依赖 GhostScript）。" +
        "输入 inputFiles 数组（绝对路径，按顺序拼接）+ outputPath（合并后的新文件路径）。" +
        "适用于「把多张发票 PDF 合成一份」「合并多份报告」这类场景。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "inputFiles": { "type": "array", "items": { "type": "string" }, "description": "输入 PDF 绝对路径数组（按顺序合并）" },
            "outputPath": { "type": "string", "description": "合并后的新 PDF 绝对路径" }
          },
          "required": ["inputFiles", "outputPath"]
        }
        """;

    [Microsoft.SemanticKernel.KernelFunction("merge_pdfs")]
    public async Task<string> MergeKernelAsync(
        string[] inputFiles,
        string outputPath)
    {
        var args = JsonSerializer.Serialize(new { inputFiles, outputPath });
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
        MergeArgs args;
        try { args = JsonSerializer.Deserialize<MergeArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (args.InputFiles == null || args.InputFiles.Length == 0)
            return ToolResult.Fail("inputFiles 不能为空（至少需要 1 个 PDF）");
        if (string.IsNullOrWhiteSpace(args.OutputPath))
            return ToolResult.Fail("outputPath 不能为空");

        // 校验所有输入文件存在
        for (var i = 0; i < args.InputFiles.Length; i++)
        {
            var p = args.InputFiles[i];
            if (string.IsNullOrWhiteSpace(p))
                return ToolResult.Fail($"inputFiles[{i}] 为空");
            if (!File.Exists(p))
                return ToolResult.Fail($"输入文件不存在：{p}");
        }

        try
        {
            var sw = Stopwatch.StartNew();

            using var outputDoc = new PdfDocument();
            var totalPages = 0;

            foreach (var path in args.InputFiles)
            {
                ct.ThrowIfCancellationRequested();
                using var inputDoc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
                totalPages += inputDoc.PageCount;
                for (var i = 0; i < inputDoc.PageCount; i++)
                {
                    outputDoc.AddPage(inputDoc.Pages[i]);
                }
            }

            // 确保输出目录存在
            var outDir = Path.GetDirectoryName(args.OutputPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            outputDoc.Save(args.OutputPath);
            sw.Stop();

            var outInfo = new FileInfo(args.OutputPath);
            var data = new
            {
                outputPath = args.OutputPath,
                inputCount = args.InputFiles.Length,
                pageCount = totalPages,
                outputSizeBytes = outInfo.Length,
                elapsedMs = sw.ElapsedMilliseconds
            };

            var summary = $"📄 合并 {args.InputFiles.Length} 个 PDF → {Path.GetFileName(args.OutputPath)}（{totalPages} 页 / {outInfo.Length:N0} 字节 / {sw.ElapsedMilliseconds}ms）";
            return ToolResult.Ok(summary, data);
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Fail("合并已取消");
        }
        catch (PdfSharpCore.Pdf.IO.PdfReaderException ex)
        {
            return ToolResult.Fail($"PDF 解析失败（文件可能损坏或加密）：{ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"合并失败：{ex.Message}");
        }
    }

    private sealed record MergeArgs(string[] InputFiles, string OutputPath);
}
