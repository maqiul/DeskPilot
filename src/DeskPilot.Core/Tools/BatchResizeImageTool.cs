using System.Text.Json;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 批量缩放图片工具。保持原图比例缩放到指定宽度/高度。
/// 支持 jpg/jpeg/png/bmp/gif（System.Drawing 内置格式）。
///
/// AI 调用示例：
/// {
///   "directory": "C:\\Users\\me\\photos",
///   "maxWidth": 1920,
///   "maxHeight": 1080,
///   "quality": 85,
///   "pattern": "*.jpg"
/// }
/// </summary>
public sealed class BatchResizeImageTool : ITool
{
    private static readonly HashSet<string> SupportedExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif"
    };

    public string Name => "batch_resize_image";
    public string Description =>
        "批量缩放目录里的图片到指定尺寸（保持原图比例）。" +
        "支持 jpg/jpeg/png/bmp/gif。DryRun 模式只预览不保存。" +
        "可指定最大宽度/高度（按比例缩放）+ JPEG 质量。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "directory": { "type": "string", "description": "目标目录绝对路径" },
            "pattern": { "type": "string", "description": "glob 过滤（如 *.jpg），留空匹配所有图片" },
            "maxWidth": { "type": "integer", "description": "最大宽度（像素），原图更窄则不放大", "minimum": 1 },
            "maxHeight": { "type": "integer", "description": "最大高度（像素），原图更矮则不放大", "minimum": 1 },
            "quality": { "type": "integer", "description": "JPEG 质量 1-100（仅 jpg 输出），默认 85", "minimum": 1, "maximum": 100 },
            "suffix": { "type": "string", "description": "输出文件后缀（默认 _resized），原图保留不动" },
            "dryRun": { "type": "boolean", "description": "true 只预览不保存，默认 false" }
          },
          "required": ["directory", "maxWidth", "maxHeight"]
        }
        """;

    [Microsoft.SemanticKernel.KernelFunction("batch_resize_image")]
    public async Task<string> ResizeKernelAsync(
        string directory,
        int maxWidth,
        int maxHeight,
        string? pattern = null,
        int? quality = null,
        string? suffix = null,
        bool dryRun = false)
    {
        var args = JsonSerializer.Serialize(new
        {
            directory,
            maxWidth,
            maxHeight,
            pattern,
            quality,
            suffix,
            dryRun
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
        ResizeArgs args;
        try { args = JsonSerializer.Deserialize<ResizeArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (!Directory.Exists(args.Directory))
            return ToolResult.Fail($"目录不存在：{args.Directory}");

        if (args.MaxWidth < 1 || args.MaxHeight < 1)
            return ToolResult.Fail($"maxWidth/maxHeight 必须 >= 1（你给的是 {args.MaxWidth}x{args.MaxHeight}）");

        var quality = Math.Clamp(args.Quality ?? 85, 1, 100);
        var suffix = string.IsNullOrEmpty(args.Suffix) ? "_resized" : args.Suffix!;

        try
        {
            var options = new EnumerationOptions { IgnoreInaccessible = true };
            var allFiles = Directory.EnumerateFiles(args.Directory, args.Pattern ?? "*", options)
                .Where(f => SupportedExts.Contains(Path.GetExtension(f)))
                .ToList();

            var report = new ResizeReport
            {
                Directory = args.Directory,
                MaxWidth = args.MaxWidth,
                MaxHeight = args.MaxHeight,
                Quality = quality,
                Suffix = suffix,
                DryRun = args.DryRun,
                Scanned = allFiles.Count,
                Details = new List<ResizeDetail>()
            };

            foreach (var src in allFiles)
            {
                ct.ThrowIfCancellationRequested();
                var ext = Path.GetExtension(src).ToLowerInvariant();
                var nameWithoutExt = Path.GetFileNameWithoutExtension(src);

                // 跳过已经带 suffix 的文件（避免重复处理）
                if (nameWithoutExt.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    report.Skipped++;
                    report.Details.Add(new ResizeDetail
                    {
                        SourcePath = src,
                        Status = "Skipped",
                        Message = $"已带后缀 {suffix}"
                    });
                    continue;
                }

                try
                {
                    using var original = Image.FromFile(src);
                    var newSize = ComputeFitSize(original.Width, original.Height, args.MaxWidth, args.MaxHeight);
                    var finalDst = Path.Combine(
                        args.Directory,
                        $"{nameWithoutExt}{suffix}{ext}");

                    if (args.DryRun)
                    {
                        report.WouldResize++;
                        report.Details.Add(new ResizeDetail
                        {
                            SourcePath = src,
                            OutputPath = finalDst,
                            OriginalWidth = original.Width,
                            OriginalHeight = original.Height,
                            NewWidth = newSize.Width,
                            NewHeight = newSize.Height,
                            Status = "WouldResize"
                        });
                    }
                    else
                    {
                        using var resized = new Bitmap(newSize.Width, newSize.Height);
                        using (var g = Graphics.FromImage(resized))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.CompositingQuality = CompositingQuality.HighQuality;
                            g.DrawImage(original, 0, 0, newSize.Width, newSize.Height);
                        }

                        if (ext is ".jpg" or ".jpeg")
                        {
                            var encoder = ImageCodecInfo.GetImageEncoders()
                                .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                            var encoderParams = new EncoderParameters(1)
                            {
                                Param = { [0] = new EncoderParameter(Encoder.Quality, (long)quality) }
                            };
                            resized.Save(finalDst, encoder, encoderParams);
                        }
                        else
                        {
                            resized.Save(finalDst, GetImageFormat(ext));
                        }

                        report.Resized++;
                        report.Details.Add(new ResizeDetail
                        {
                            SourcePath = src,
                            OutputPath = finalDst,
                            OriginalWidth = original.Width,
                            OriginalHeight = original.Height,
                            NewWidth = newSize.Width,
                            NewHeight = newSize.Height,
                            Status = "Resized"
                        });
                    }
                }
                catch (Exception ex)
                {
                    report.Failed++;
                    report.Details.Add(new ResizeDetail
                    {
                        SourcePath = src,
                        Status = "Failed",
                        Message = ex.Message
                    });
                }

                await Task.Yield();
            }

            return ToolResult.Ok(
                args.DryRun
                    ? $"📋 [预览] 共 {report.Scanned} 张图片，将缩放 {report.WouldResize} 张"
                    : $"✅ 缩放完成：{report.Resized} 张成功，{report.Failed} 失败，{report.Skipped} 跳过（扫描 {report.Scanned}）",
                report);
        }
        catch (OperationCanceledException) { return ToolResult.Fail("用户取消"); }
        catch (Exception ex) { return ToolResult.Fail($"缩放异常：{ex.Message}"); }
    }

    private static Size ComputeFitSize(int srcW, int srcH, int maxW, int maxH)
    {
        if (srcW <= maxW && srcH <= maxH) return new Size(srcW, srcH);
        var ratio = Math.Min((double)maxW / srcW, (double)maxH / srcH);
        return new Size((int)(srcW * ratio), (int)(srcH * ratio));
    }

    private static ImageFormat GetImageFormat(string ext) => ext switch
    {
        ".png" => ImageFormat.Png,
        ".bmp" => ImageFormat.Bmp,
        ".gif" => ImageFormat.Gif,
        _ => ImageFormat.Jpeg
    };

    private sealed class ResizeArgs
    {
        public string Directory { get; set; } = string.Empty;
        public string? Pattern { get; set; }
        public int MaxWidth { get; set; }
        public int MaxHeight { get; set; }
        public int? Quality { get; set; }
        public string? Suffix { get; set; }
        public bool DryRun { get; set; }
    }
}

public sealed class ResizeReport
{
    public string Directory { get; set; } = string.Empty;
    public int MaxWidth { get; set; }
    public int MaxHeight { get; set; }
    public int Quality { get; set; }
    public string Suffix { get; set; } = "_resized";
    public bool DryRun { get; set; }
    public int Scanned { get; set; }
    public int Resized { get; set; }
    public int WouldResize { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public List<ResizeDetail> Details { get; set; } = new();
}

public sealed class ResizeDetail
{
    public string SourcePath { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public int NewWidth { get; set; }
    public int NewHeight { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}