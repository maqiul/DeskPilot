using System.Drawing;
using System.Text.Json;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 图片旋转/翻转工具：旋转 90/180/270 度，或水平/垂直翻转。
/// 使用 System.Drawing.Common（v0.5 已引入）。
///
/// AI 调用示例：
/// {
///   "inputPath": "C:\\photo.jpg",
///   "outputPath": "C:\\photo_rotated.jpg",
///   "rotation": 90
/// }
/// 或：
/// {
///   "inputPath": "C:\\photo.jpg",
///   "outputPath": "C:\\photo_flipped.jpg",
///   "flip": "horizontal"
/// }
/// </summary>
public sealed class RotateImageTool : ITool
{
    public RiskLevel Risk => RiskLevel.Destructive;  // 写新文件

    public string Name => "rotate_image";
    public string Description =>
        "图片旋转/翻转工具。支持：(1) rotation 旋转 90/180/270 度；(2) flip 翻转（horizontal 水平 / vertical 垂直）。" +
        "输入 inputPath（源图片）+ outputPath（目标图片）+ rotation（可选）或 flip（可选）。" +
        "旋转 + 翻转可同时指定，先旋转后翻转。" +
        "适用于「把竖屏照片转横屏」「扫描件倒转」等场景。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "inputPath": { "type": "string", "description": "源图片绝对路径" },
            "outputPath": { "type": "string", "description": "目标图片绝对路径" },
            "rotation": { "type": "integer", "description": "旋转角度：0 / 90 / 180 / 270（默认 0）", "enum": [0, 90, 180, 270] },
            "flip": { "type": "string", "description": "翻转方向：none / horizontal / vertical（默认 none）", "enum": ["none", "horizontal", "vertical"] }
          },
          "required": ["inputPath", "outputPath"]
        }
        """;

    [Microsoft.SemanticKernel.KernelFunction("rotate_image")]
    public async Task<string> RotateKernelAsync(
        string inputPath,
        string outputPath,
        int rotation = 0,
        string flip = "none")
    {
        var args = JsonSerializer.Serialize(new { inputPath, outputPath, rotation, flip });
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
        RotateArgs args;
        try { args = JsonSerializer.Deserialize<RotateArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (string.IsNullOrWhiteSpace(args.InputPath))
            return ToolResult.Fail("inputPath 不能为空");
        if (string.IsNullOrWhiteSpace(args.OutputPath))
            return ToolResult.Fail("outputPath 不能为空");
        if (!File.Exists(args.InputPath))
            return ToolResult.Fail($"输入文件不存在：{args.InputPath}");
        if (args.Rotation != 0 && args.Rotation != 90 && args.Rotation != 180 && args.Rotation != 270)
            return ToolResult.Fail($"rotation 必须是 0/90/180/270 之一，当前 {args.Rotation}");
        if (!new[] { "none", "horizontal", "vertical" }.Contains(args.Flip.ToLowerInvariant()))
            return ToolResult.Fail($"flip 必须是 none/horizontal/vertical 之一，当前 '{args.Flip}'");

        try
        {
            using var img = Image.FromFile(args.InputPath);
            var appliedOps = new List<string>();

            // 旋转
            if (args.Rotation == 90) { img.RotateFlip(RotateFlipType.Rotate90FlipNone); appliedOps.Add("旋转 90°"); }
            else if (args.Rotation == 180) { img.RotateFlip(RotateFlipType.Rotate180FlipNone); appliedOps.Add("旋转 180°"); }
            else if (args.Rotation == 270) { img.RotateFlip(RotateFlipType.Rotate270FlipNone); appliedOps.Add("旋转 270°"); }

            // 翻转
            if (string.Equals(args.Flip, "horizontal", StringComparison.OrdinalIgnoreCase))
            {
                img.RotateFlip(args.Rotation == 0 ? RotateFlipType.RotateNoneFlipX : AppendFlipAfterRotate(args.Rotation, horizontal: true));
                appliedOps.Add("水平翻转");
            }
            else if (string.Equals(args.Flip, "vertical", StringComparison.OrdinalIgnoreCase))
            {
                img.RotateFlip(args.Rotation == 0 ? RotateFlipType.RotateNoneFlipY : AppendFlipAfterRotate(args.Rotation, horizontal: false));
                appliedOps.Add("垂直翻转");
            }

            img.Save(args.OutputPath, GetImageFormat(args.OutputPath));

            return ToolResult.Ok(
                $"图片处理完成：{string.Join(" + ", appliedOps)}，保存到 {args.OutputPath}",
                new { outputPath = args.OutputPath, ops = appliedOps });
        }
        catch (Exception ex) { return ToolResult.Fail($"图片处理失败：{ex.Message}"); }
    }

    /// <summary>在已旋转的基础上追加水平/垂直翻转</summary>
    private static RotateFlipType AppendFlipAfterRotate(int rotation, bool horizontal) => (rotation, horizontal) switch
    {
        (90, true) => RotateFlipType.Rotate90FlipX,
        (90, false) => RotateFlipType.Rotate90FlipY,
        (180, true) => RotateFlipType.Rotate180FlipX,
        (180, false) => RotateFlipType.Rotate180FlipY,
        (270, true) => RotateFlipType.Rotate270FlipX,
        (270, false) => RotateFlipType.Rotate270FlipY,
        _ => RotateFlipType.RotateNoneFlipNone
    };

    private static System.Drawing.Imaging.ImageFormat GetImageFormat(string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "jpg" or "jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
            "png" => System.Drawing.Imaging.ImageFormat.Png,
            "bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
            "gif" => System.Drawing.Imaging.ImageFormat.Gif,
            _ => System.Drawing.Imaging.ImageFormat.Png  // 默认 PNG
        };
    }

    private sealed record RotateArgs(string InputPath, string OutputPath, int Rotation = 0, string Flip = "none");
}