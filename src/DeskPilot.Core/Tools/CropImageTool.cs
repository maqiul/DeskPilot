using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 图片裁剪工具：从源图片中裁剪出指定矩形区域。
/// 使用 System.Drawing.Common（v0.5 已引入）。
///
/// AI 调用示例：
/// {
///   "inputPath": "C:\\photo.jpg",
///   "outputPath": "C:\\photo_cropped.jpg",
///   "x": 100,
///   "y": 50,
///   "width": 800,
///   "height": 600
/// }
/// </summary>
public sealed class CropImageTool : ITool
{
    public RiskLevel Risk => RiskLevel.Destructive;  // 写新文件

    public string Name => "crop_image";
    public string Description =>
        "图片裁剪工具：从源图片的 (x, y) 坐标开始，裁剪 width × height 大小的矩形区域。" +
        "输入 inputPath（源图片）+ outputPath（目标图片）+ x + y + width + height。" +
        "坐标原点在左上角，单位像素。超出源图片边界的部分会自动截断。" +
        "适用于「裁剪截图多余空白」「提取图片局部区域」等场景。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "inputPath": { "type": "string", "description": "源图片绝对路径" },
            "outputPath": { "type": "string", "description": "目标图片绝对路径" },
            "x": { "type": "integer", "description": "起始 X 坐标（左上角为原点，单位像素）", "minimum": 0 },
            "y": { "type": "integer", "description": "起始 Y 坐标（左上角为原点，单位像素）", "minimum": 0 },
            "width": { "type": "integer", "description": "裁剪宽度（像素）", "minimum": 1 },
            "height": { "type": "integer", "description": "裁剪高度（像素）", "minimum": 1 }
          },
          "required": ["inputPath", "outputPath", "x", "y", "width", "height"]
        }
        """;

    [Microsoft.SemanticKernel.KernelFunction("crop_image")]
    public async Task<string> CropKernelAsync(
        string inputPath,
        string outputPath,
        int x,
        int y,
        int width,
        int height)
    {
        var args = JsonSerializer.Serialize(new { inputPath, outputPath, x, y, width, height });
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
        CropArgs args;
        try { args = JsonSerializer.Deserialize<CropArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (string.IsNullOrWhiteSpace(args.InputPath))
            return ToolResult.Fail("inputPath 不能为空");
        if (string.IsNullOrWhiteSpace(args.OutputPath))
            return ToolResult.Fail("outputPath 不能为空");
        if (!File.Exists(args.InputPath))
            return ToolResult.Fail($"输入文件不存在：{args.InputPath}");
        if (args.X < 0)
            return ToolResult.Fail($"x 必须 >= 0，当前 {args.X}");
        if (args.Y < 0)
            return ToolResult.Fail($"y 必须 >= 0，当前 {args.Y}");
        if (args.Width <= 0)
            return ToolResult.Fail($"width 必须 > 0，当前 {args.Width}");
        if (args.Height <= 0)
            return ToolResult.Fail($"height 必须 > 0，当前 {args.Height}");

        try
        {
            using var img = Image.FromFile(args.InputPath);

            // 自动截断超出源图片边界的部分
            var actualX = Math.Min(args.X, img.Width);
            var actualY = Math.Min(args.Y, img.Height);
            var actualWidth = Math.Min(args.Width, img.Width - actualX);
            var actualHeight = Math.Min(args.Height, img.Height - actualY);

            if (actualWidth <= 0 || actualHeight <= 0)
                return ToolResult.Fail($"裁剪区域完全在源图片之外（源图片 {img.Width}x{img.Height}，裁剪起点 ({args.X}, {args.Y})）");

            var cropRect = new Rectangle(actualX, actualY, actualWidth, actualHeight);
            using var bmp = new Bitmap(cropRect.Width, cropRect.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.DrawImage(img, new Rectangle(0, 0, bmp.Width, bmp.Height), cropRect, GraphicsUnit.Pixel);
            }

            var ext = Path.GetExtension(args.OutputPath).TrimStart('.').ToLowerInvariant();
            var format = ext switch
            {
                "jpg" or "jpeg" => ImageFormat.Jpeg,
                "png" => ImageFormat.Png,
                "bmp" => ImageFormat.Bmp,
                "gif" => ImageFormat.Gif,
                _ => ImageFormat.Png
            };
            bmp.Save(args.OutputPath, format);

            var truncatedNote = (actualWidth != args.Width || actualHeight != args.Height)
                ? $"，原请求 {args.Width}x{args.Height}，已截断为 {actualWidth}x{actualHeight}"
                : "";

            return ToolResult.Ok(
                $"裁剪图片完成：源 {img.Width}x{img.Height} → 裁剪区域 {actualWidth}x{actualHeight}{truncatedNote}",
                new
                {
                    outputPath = args.OutputPath,
                    sourceSize = new { width = img.Width, height = img.Height },
                    cropRect = new { x = actualX, y = actualY, width = actualWidth, height = actualHeight },
                    truncated = actualWidth != args.Width || actualHeight != args.Height
                });
        }
        catch (Exception ex) { return ToolResult.Fail($"图片裁剪失败：{ex.Message}"); }
    }

    private sealed record CropArgs(string InputPath, string OutputPath, int X, int Y, int Width, int Height);
}