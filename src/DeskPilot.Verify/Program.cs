using DeskPilot.Core.Tools;
using System.Text.Json;

namespace DeskPilot.Verify;

/// <summary>
/// DeskPilot 工具离线验证程序。
///
/// 用途：在没有 API Key 的情况下，端到端验证 4 个核心工具的真实行为：
/// 1. ArchiveByDateTool（按日期归档）
/// 2. MoveFilesTool（批量移动）
/// 3. FindDuplicatesTool（找重复文件）
/// 4. RenameByPatternTool（批量重命名）
///
/// 用法：
///   dotnet run --project src/DeskPilot.Verify -- "D:\deskpilot_e2e_test"
///   dotnet run --project src/DeskPilot.Verify -- "D:\deskpilot_e2e_test" --no
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=========================================");
        Console.WriteLine("  DeskPilot 工具验证程序 v0.2");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        var sourceDir = args.Length > 0 ? args[0] : @"D:\deskpilot_e2e_test";
        var dryRunOnly = args.Contains("--no");
        var specificTool = GetArgValue(args, "--tool");

        Console.WriteLine($"📂 工作目录: {sourceDir}");
        Console.WriteLine($"🔍 模式: {(dryRunOnly ? "DryRun 预览（不修改）" : "真实执行")}");
        if (specificTool != null) Console.WriteLine($"🛠️  指定工具: {specificTool}");
        Console.WriteLine();

        if (!Directory.Exists(sourceDir))
        {
            Console.WriteLine($"❌ 工作目录不存在: {sourceDir}");
            return 1;
        }

        var results = new List<(string tool, bool success, string summary)>();

        // ========== 1. ArchiveByDateTool ==========
        if (ShouldRun(specificTool, "archive"))
            results.Add(await TestArchiveByDate(sourceDir, dryRunOnly));

        // ========== 2. MoveFilesTool ==========
        if (ShouldRun(specificTool, "move"))
            results.Add(await TestMoveFiles(sourceDir, dryRunOnly));

        // ========== 3. FindDuplicatesTool ==========
        if (ShouldRun(specificTool, "find"))
            results.Add(await TestFindDuplicates(sourceDir));

        // ========== 4. RenameByPatternTool ==========
        if (ShouldRun(specificTool, "rename"))
            results.Add(await TestRenameByPattern(sourceDir, dryRunOnly));

        // ========== 总结 ==========
        Console.WriteLine();
        Console.WriteLine("=========================================");
        Console.WriteLine("  📊 验证总结");
        Console.WriteLine("=========================================");
        foreach (var (tool, success, summary) in results)
        {
            var icon = success ? "✅" : "❌";
            Console.WriteLine($"  {icon} {tool,-30} {summary}");
        }
        Console.WriteLine();
        var allOk = results.All(r => r.success);
        Console.WriteLine(allOk ? "  🎉 全部通过" : "  ⚠️  有失败");
        Console.WriteLine("=========================================");
        return allOk ? 0 : 1;
    }

    private static string? GetArgValue(string[] args, string flag)
    {
        var idx = Array.IndexOf(args, flag);
        if (idx >= 0 && idx + 1 < args.Length) return args[idx + 1];
        return null;
    }

    private static bool ShouldRun(string? specific, string name)
        => specific == null || string.Equals(specific, name, StringComparison.OrdinalIgnoreCase);

    private static async Task<(string tool, bool success, string summary)> TestArchiveByDate(string sourceDir, bool dryRunOnly)
    {
        Console.WriteLine("━━━ [1/4] ArchiveByDateTool: 按月归档 ━━━");
        var tool = new ArchiveByDateTool();
        var args = JsonSerializer.Serialize(new
        {
            sourceDirectory = sourceDir,
            dateField = "Created",
            granularity = "Month",
            dryRun = dryRunOnly
        });
        var result = await tool.ExecuteAsync(args);
        var data = result.Data as ArchiveReport;
        var summary = data != null
            ? $"扫描 {data.Scanned}, 移动 {data.Moved}, 失败 {data.Failed}"
            : result.Summary;
        Console.WriteLine($"  {result.Summary}");
        Console.WriteLine($"  📊 {summary}");
        Console.WriteLine();
        return ("ArchiveByDateTool", result.Success, summary);
    }

    private static async Task<(string tool, bool success, string summary)> TestMoveFiles(string sourceDir, bool dryRunOnly)
    {
        Console.WriteLine("━━━ [2/4] MoveFilesTool: 批量移动 ━━━");
        // 准备：在 sourceDir/move_src 里放 3 个测试文件，移到 sourceDir/move_dst
        var src = Path.Combine(sourceDir, "move_src");
        var dst = Path.Combine(sourceDir, "move_dst");
        if (!Directory.Exists(src)) Directory.CreateDirectory(src);
        if (!Directory.Exists(dst)) Directory.CreateDirectory(dst);
        for (int i = 1; i <= 3; i++)
            File.WriteAllText(Path.Combine(src, $"file_{i}.txt"), $"content {i}");

        var tool = new MoveFilesTool();
        var args = JsonSerializer.Serialize(new { sourceDirectory = src, targetDirectory = dst });
        var result = await tool.ExecuteAsync(args);
        var data = result.Data as MoveReport;
        var summary = data != null
            ? $"扫描 {data.Scanned}, 移动 {data.Moved}, 失败 {data.Failed}"
            : result.Summary;
        Console.WriteLine($"  {result.Summary}");
        Console.WriteLine($"  📊 {summary}");
        Console.WriteLine();
        return ("MoveFilesTool", result.Success, summary);
    }

    private static async Task<(string tool, bool success, string summary)> TestFindDuplicates(string sourceDir)
    {
        Console.WriteLine("━━━ [3/4] FindDuplicatesTool: 找重复 ━━━");
        // 准备：3 个文件，其中 2 个内容相同
        var dir = Path.Combine(sourceDir, "dup_test");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.txt"), "hello world");
        File.WriteAllText(Path.Combine(dir, "b.txt"), "hello world");
        File.WriteAllText(Path.Combine(dir, "c.txt"), "different");

        var tool = new FindDuplicatesTool();
        var args = JsonSerializer.Serialize(new { directory = dir });
        var result = await tool.ExecuteAsync(args);
        var data = result.Data as DuplicateReport;
        var summary = data != null
            ? $"扫描 {data.Scanned}, 重复组 {data.DuplicateGroups}, 重复文件 {data.DuplicateFiles}"
            : result.Summary;
        Console.WriteLine($"  {result.Summary}");
        Console.WriteLine($"  📊 {summary}");
        Console.WriteLine();
        return ("FindDuplicatesTool", result.Success, summary);
    }

    private static async Task<(string tool, bool success, string summary)> TestRenameByPattern(string sourceDir, bool dryRunOnly)
    {
        Console.WriteLine("━━━ [4/4] RenameByPatternTool: 批量重命名 ━━━");
        // 准备：3 个 IMG_xxx.jpg，用正则改成 photo_xxx.jpg
        var dir = Path.Combine(sourceDir, "rename_test");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        for (int i = 1; i <= 3; i++)
            File.WriteAllText(Path.Combine(dir, $"IMG_{i:D3}.jpg"), $"image {i}");

        var tool = new RenameByPatternTool();
        var args = JsonSerializer.Serialize(new
        {
            directory = dir,
            find = "IMG_",
            replace = "photo_",
            dryRun = dryRunOnly
        });
        var result = await tool.ExecuteAsync(args);
        var data = result.Data as RenameReport;
        var summary = data != null
            ? $"扫描 {data.Scanned}, 重命名 {data.Renamed}, 失败 {data.Failed}"
            : result.Summary;
        Console.WriteLine($"  {result.Summary}");
        Console.WriteLine($"  📊 {summary}");
        Console.WriteLine();
        return ("RenameByPatternTool", result.Success, summary);
    }
}