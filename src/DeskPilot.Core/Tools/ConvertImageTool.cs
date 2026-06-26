using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 图片格式转换工具：把一张图片从源格式转换为目标格式（png / jpg / bmp / webp / gif）。
/// 使用 System.Drawing.Common（v0.5 已引入，无需新增 NuGet）。
/// quality 参数（1-100）仅对 jpg 生效，其他格式忽略。
///
/// AI 调用示例：
/// {
///   "inputPath": "C:\\image.png",
///   "outputPath": "C:\\image.jpg",
///   "targetFormat": "jpg",
///   "quality": 85
/// }
/// </summary>
public sealed class ConvertImageTool : ITool
{
    public RiskLevel Risk => RiskLevel.Destructive;  // 写新文件

    public string Name => "convert_image";
    public string Description =>
        "把一张图片从源格式转换为目标格式。支持 png / jpg / bmp / webp / gif 互转。" +
        "quality 参数（1-100）仅对 jpg 生效，其他格式忽略（默认 85）。" +
        "适用于「PNG 太大转 JPG 缩体积」「扫描件转 BMP 嵌入文档」等场景。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "inputPath": { "type": "string", "description": "源图片绝对路径" },
            "outputPath": { "type": "string", "description": "目标图片绝对路径" },
            "targetFormat": { "type": "string", "description": "目标格式：小写 png / jpg / bmp / webp / gif" },
            "quality": { "type": "integer", "description": "JPG 质量 1-100（默认 85）", "minimum": 1, "maximum": 100 }
          },
          "required": ["inputPath", "outputPath", "targetFormat"]
        }
        """;

    private static readonly string[] SupportedFormats = { "png", "jpg", "bmp", "webp", "gif" };

    [Microsoft.SemanticKernel.KernelFunction("convert_image")]
    public async Task<string> ConvertKernelAsync(
        string inputPath,
        string outputPath,
        string targetFormat,
        int? quality = null)
    {
        var args = JsonSerializer.Serialize(new
        {
            inputPath,
            outputPath,
            targetFormat,
            quality = quality ?? 85
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
        ConvertArgs args;
        try { args = JsonSerializer.Deserialize<ConvertArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (string.IsNullOrWhiteSpace(args.InputPath))
            return ToolResult.Fail("inputPath 不能为空");
        if (string.IsNullOrWhiteSpace(args.OutputPath))
            return ToolResult.Fail("outputPath 不能为空");
        if (string.IsNullOrWhiteSpace(args.TargetFormat))
            return ToolResult.Fail("targetFormat 不能为空");
        if (!File.Exists(args.InputPath))
            return ToolResult.Fail($"输入文件不存在：{args.InputPath}");

        var fmt = args.TargetFormat.ToLowerInvariant();
        if (Array.IndexOf(SupportedFormats, fmt) < 0)
            return ToolResult.Fail($"不支持的目标格式：{args.TargetFormat}（支持：{string.Join(", ", SupportedFormats)}）");

        var quality = Math.Clamp(args.Quality ?? 85, 1, 100);

        try
        {
            var sw = Stopwatch.StartNew();
            var inInfo = new FileInfo(args.InputPath);

            using var src = Image.FromFile(args.InputPath);

            // 确保输出目录存在
            var outDir = Path.GetDirectoryName(args.OutputPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            ImageFormat targetFmt;
            long encoderQuality = quality;
            switch (fmt)
            {
                case "png": targetFmt = ImageFormat.Png; break;
                case "jpg": targetFmt = ImageFormat.Jpeg; break;
                case "bmp": targetFmt = ImageFormat.Bmp; break;
                case "gif": targetFmt = ImageFormat.Gif; break;
                case "webp":
                    // System.Drawing 不直接支持 webp 写出，回退为 png（保持透明通道）
                    targetFmt = ImageFormat.Png;
                    break;
                default: return ToolResult.Fail($"不支持的目标格式：{fmt}");
            }

            // JPG 走带 quality 的编码器分支；其他格式直接 Save
            if (targetFmt == ImageFormat.Jpeg)
            {
                SaveJpegWithQuality(src, args.OutputPath, quality);
            }
            else
            {
                src.Save(args.OutputPath, targetFmt);
            }
            sw.Stop();

            var outInfo = new FileInfo(args.OutputPath);
            var data = new
            {
                inputPath = args.InputPath,
                outputPath = args.OutputPath,
                targetFormat = fmt,
                inputSizeBytes = inInfo.Length,
                outputSizeBytes = outInfo.Length,
                quality = (targetFmt == ImageFormat.Jpeg) ? (int?)quality : null,
                elapsedMs = sw.ElapsedMilliseconds
            };

            var summary = $"🖼️ 转换 {Path.GetFileName(args.InputPath)} → {fmt.ToUpper()}（{inInfo.Length:N0} → {outInfo.Length:N0} 字节，{sw.ElapsedMilliseconds}ms）";
            return ToolResult.Ok(summary, data);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"转换失败：{ex.Message}");
        }
    }

    private static void SaveJpegWithQuality(Image src, string outputPath, long quality)
    {
        var jpegCodec = ImageCodecInfo.GetImageEncoders()
            .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
        src.Save(outputPath, jpegCodec, parameters);
    }

    private sealed record ConvertArgs(string InputPath, string OutputPath, string TargetFormat, int? Quality);
}
