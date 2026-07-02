using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;

namespace DeskPilot.Core.Tools;

/// <summary>
/// 按图片 EXIF DateTimeOriginal 批量重命名工具。
/// 适用于「相机/手机照片按拍摄时间整理」「扫描件按 EXIF 时间重命名」等场景。
///
/// AI 调用示例：
/// {
///   "directory": "C:\\Users\\me\\Photos",
///   "pattern": "*.jpg",
///   "dateFormat": "yyyy-MM-dd_HH-mm-ss",
///   "prefix": "IMG_",
///   "fallbackToFileDate": true,
///   "dryRun": false
/// }
///
/// 设计：
/// - 使用 System.Drawing.Common（v0.5 已引入，零新依赖）
/// - 支持 JPG/JPEG/PNG（PNG 无 EXIF 自动 fallback 到文件修改时间）
/// - 冲突解决同 RenameByPatternTool（_2 / _3 后缀）
/// - DryRun 模式只预览不重命名
/// </summary>
public sealed class RenameByExifTool : ITool
{
    public RiskLevel Risk => RiskLevel.Destructive;

    public string Name => "rename_by_exif";
    public string Description =>
        "按图片 EXIF DateTimeOriginal 批量重命名照片。" +
        "dateFormat 默认 'yyyy-MM-dd_HH-mm-ss'，可改成 'yyyyMMdd_HHmmss' 等。" +
        "可加 prefix 前缀（如 'IMG_'）。fallbackToFileDate=true 时无 EXIF 用文件修改时间。" +
        "DryRun 模式只预览不实际改名。" +
        "适用于「相机照片按拍摄时间整理」「扫描件按 EXIF 时间重命名」等场景。";

    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "directory": { "type": "string", "description": "目标目录绝对路径" },
            "pattern": { "type": "string", "description": "glob 过滤（默认 *.jpg，可改 *.png 或 *.jpeg）" },
            "dateFormat": { "type": "string", "description": "日期格式（默认 yyyy-MM-dd_HH-mm-ss）" },
            "prefix": { "type": "string", "description": "可选前缀（如 IMG_）" },
            "fallbackToFileDate": { "type": "boolean", "description": "无 EXIF 时是否用文件修改时间（默认 true）" },
            "dryRun": { "type": "boolean", "description": "true 只预览不重命名，默认 false" }
          },
          "required": ["directory"]
        }
        """;

    [Microsoft.SemanticKernel.KernelFunction("rename_by_exif")]
    public async Task<string> RenameKernelAsync(
        string directory,
        string? pattern = null,
        string? dateFormat = null,
        string? prefix = null,
        bool fallbackToFileDate = true,
        bool dryRun = false)
    {
        var args = JsonSerializer.Serialize(new
        {
            directory,
            pattern,
            dateFormat,
            prefix,
            fallbackToFileDate,
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
        RenameArgs args;
        try { args = JsonSerializer.Deserialize<RenameArgs>(argumentsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
        catch (Exception ex) { return ToolResult.Fail($"参数解析失败：{ex.Message}"); }

        if (!Directory.Exists(args.Directory))
            return ToolResult.Fail($"目录不存在：{args.Directory}");

        var dateFormat = string.IsNullOrWhiteSpace(args.DateFormat) ? "yyyy-MM-dd_HH-mm-ss" : args.DateFormat;
        var pattern = string.IsNullOrWhiteSpace(args.Pattern) ? "*.jpg" : args.Pattern;

        try
        {
            var options = new EnumerationOptions { IgnoreInaccessible = true };
            var files = Directory.EnumerateFiles(args.Directory, pattern, options).ToList();

            var report = new RenameByExifReport
            {
                Directory = args.Directory,
                Pattern = pattern,
                DateFormat = dateFormat,
                Prefix = args.Prefix ?? string.Empty,
                Scanned = files.Count,
                DryRun = args.DryRun,
                Details = new List<RenameByExifDetail>()
            };

            foreach (var src in files)
            {
                ct.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(src);
                var ext = Path.GetExtension(src);

                var exifDate = ReadExifDateTimeOriginal(src);
                DateTime actualDate;
                string source;

                if (exifDate.HasValue)
                {
                    actualDate = exifDate.Value;
                    source = "EXIF";
                }
                else if (args.FallbackToFileDate)
                {
                    actualDate = File.GetLastWriteTime(src);
                    source = "FileDate";
                }
                else
                {
                    report.Skipped++;
                    report.Details.Add(new RenameByExifDetail
                    {
                        OldPath = src,
                        NewPath = src,
                        Status = "Skipped",
                        Message = "无 EXIF 且 fallbackToFileDate=false"
                    });
                    continue;
                }

                var newName = (args.Prefix ?? string.Empty) + actualDate.ToString(dateFormat) + ext;
                var dst = Path.Combine(args.Directory, newName);
                var finalDst = ResolveCollision(dst);

                if (finalDst == src) continue; // 无变化

                try
                {
                    if (!args.DryRun)
                        File.Move(src, finalDst);
                    var status = args.DryRun ? "WouldRename" : "Renamed";
                    report.Renamed++;
                    report.Details.Add(new RenameByExifDetail
                    {
                        OldPath = src,
                        NewPath = finalDst,
                        Status = status,
                        DateSource = source,
                        ExifDate = actualDate
                    });
                }
                catch (Exception ex)
                {
                    report.Failed++;
                    report.Details.Add(new RenameByExifDetail
                    {
                        OldPath = src,
                        NewPath = finalDst,
                        Status = "Failed",
                        Message = ex.Message
                    });
                }

                await Task.Yield();
            }

            return ToolResult.Ok(
                args.DryRun
                    ? $"📋 [预览] 共 {report.Scanned} 个文件，将重命名 {report.Renamed} 个，跳过 {report.Skipped} 个，失败 {report.Failed} 个"
                    : $"✅ EXIF 重命名完成：改名 {report.Renamed} 个，跳过 {report.Skipped} 个，失败 {report.Failed} 个（扫描 {report.Scanned}）",
                report);
        }
        catch (OperationCanceledException) { return ToolResult.Fail("用户取消"); }
        catch (Exception ex) { return ToolResult.Fail($"EXIF 重命名异常：{ex.Message}"); }
    }

    /// <summary>
    /// 读取图片 EXIF DateTimeOriginal 字段。
    /// PropertyItem 0x9003 = DateTimeOriginal（拍摄时间）。
    /// PropertyItem 0x0132 = DateTime（修改时间，备用）。
    /// </summary>
    private static DateTime? ReadExifDateTimeOriginal(string imagePath)
    {
        try
        {
            using var img = Image.FromFile(imagePath);
            if (!img.PropertyIdList.Contains(0x9003))
                return null;

            var prop = img.GetPropertyItem(0x9003);
            if (prop?.Value is null || prop.Value.Length < 19)
                return null;

            // EXIF DateTime 格式："YYYY:MM:DD HH:MM:SS"（19 字节 ASCII）
            var raw = System.Text.Encoding.ASCII.GetString(prop.Value, 0, 19).Trim('\0').Trim();
            if (DateTime.TryParseExact(raw, "yyyy:MM:dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
                return dt;
            return null;
        }
        catch
        {
            // 图片无 EXIF / 损坏 / 格式不支持 → 返回 null 让调用方 fallback
            return null;
        }
    }

    private static string ResolveCollision(string targetPath)
    {
        if (!File.Exists(targetPath)) return targetPath;
        var dir = Path.GetDirectoryName(targetPath)!;
        var name = Path.GetFileNameWithoutExtension(targetPath);
        var ext = Path.GetExtension(targetPath);
        for (int i = 2; i < 1000; i++)
        {
            var c = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(c)) return c;
        }
        return Path.Combine(dir, $"{name}_{DateTime.Now:HHmmssfff}{ext}");
    }

    private sealed class RenameArgs
    {
        public string Directory { get; set; } = string.Empty;
        public string? Pattern { get; set; }
        public string? DateFormat { get; set; }
        public string? Prefix { get; set; }
        public bool FallbackToFileDate { get; set; } = true;
        public bool DryRun { get; set; }
    }
}

public sealed class RenameByExifReport
{
    public string Directory { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string DateFormat { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public int Scanned { get; set; }
    public int Renamed { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public bool DryRun { get; set; }
    public List<RenameByExifDetail> Details { get; set; } = new();
}

public sealed class RenameByExifDetail
{
    public string OldPath { get; set; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? DateSource { get; set; }
    public DateTime? ExifDate { get; set; }
}