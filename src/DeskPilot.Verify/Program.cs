using DeskPilot.Core.Tools;
using System.Text.Json;

namespace DeskPilot.Verify;

/// <summary>
/// DeskPilot 工具离线验证程序。
///
/// 用途：在没有 API Key 的情况下，端到端验证 ArchiveByDateTool 的真实归档行为。
/// 用法：
///   dotnet run --project src/DeskPilot.Verify -- "D:\deskpilot_e2e_test\invoices" Month Created
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=========================================");
        Console.WriteLine("  DeskPilot 工具验证程序 v0.1.1");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        // 默认参数
        var sourceDir = args.Length > 0 ? args[0] : @"D:\deskpilot_e2e_test\invoices";
        var granularity = args.Length > 1 ? args[1] : "Month";
        var dateField = args.Length > 2 ? args[2] : "Created";

        Console.WriteLine($"📂 源目录:   {sourceDir}");
        Console.WriteLine($"📅 日期字段: {dateField}");
        Console.WriteLine($"📊 粒度:     {granularity}");
        Console.WriteLine();

        // 1) 检查源目录
        if (!Directory.Exists(sourceDir))
        {
            Console.WriteLine($"❌ 源目录不存在: {sourceDir}");
            return 1;
        }

        // 2) 列出原始文件
        var originalFiles = Directory.GetFiles(sourceDir);
        Console.WriteLine($"📄 原始文件数: {originalFiles.Length}");
        foreach (var f in originalFiles)
        {
            var created = File.GetCreationTime(f);
            var modified = File.GetLastWriteTime(f);
            Console.WriteLine($"   - {Path.GetFileName(f),-20} 创建:{created:yyyy-MM-dd HH:mm} 修改:{modified:yyyy-MM-dd HH:mm}");
        }
        Console.WriteLine();

        // 3) DryRun 预览
        Console.WriteLine("━━━ Step 1: DryRun 预览 ━━━");
        var tool = new ArchiveByDateTool();
        var dryRunArgs = JsonSerializer.Serialize(new
        {
            sourceDirectory = sourceDir,
            dateField,
            granularity,
            dryRun = true
        });
        var dryRunResult = await tool.ExecuteAsync(dryRunArgs);
        Console.WriteLine(dryRunResult.Success ? "✅" : "❌");
        Console.WriteLine(dryRunResult.Summary);
        if (dryRunResult.Data is ArchiveReport dryReport)
        {
            Console.WriteLine($"   扫描: {dryReport.Scanned}, 将移动: {dryReport.WouldMove}, 子目录: {dryReport.Subdirectories}");
            foreach (var d in dryReport.Details.Take(5))
            {
                Console.WriteLine($"   · {Path.GetFileName(d.SourcePath)} → {Path.GetFileName(Path.GetDirectoryName(d.TargetPath))}");
            }
            if (dryReport.Details.Count > 5)
                Console.WriteLine($"   ... 还有 {dryReport.Details.Count - 5} 个");
        }
        Console.WriteLine();

        // 4) 真实归档（自动确认模式：除非传 --no 跳过）
        if (args.Contains("--no"))
        {
            Console.WriteLine("⏸️  --no 模式，跳过真实归档");
            return 0;
        }
        Console.WriteLine("▶️  执行真实归档（自动确认模式）...");
        Console.WriteLine();

        Console.WriteLine("━━━ Step 2: 真实归档 ━━━");
        var realArgs = JsonSerializer.Serialize(new
        {
            sourceDirectory = sourceDir,
            dateField,
            granularity,
            dryRun = false
        });
        var realResult = await tool.ExecuteAsync(realArgs);
        Console.WriteLine(realResult.Success ? "✅" : "❌");
        Console.WriteLine(realResult.Summary);

        Console.WriteLine();

        // 5) 验证归档结果
        Console.WriteLine("━━━ Step 3: 验证归档结果 ━━━");
        var archiveDir = Path.Combine(sourceDir, "archive");
        if (Directory.Exists(archiveDir))
        {
            var subDirs = Directory.GetDirectories(archiveDir);
            Console.WriteLine($"📂 archive/ 下有 {subDirs.Length} 个子目录:");
            foreach (var sub in subDirs.OrderBy(s => s))
            {
                var dirName = Path.GetFileName(sub);
                var files = Directory.GetFiles(sub);
                Console.WriteLine($"   📁 {dirName}/ ({files.Length} 个文件)");
                foreach (var f in files)
                {
                    Console.WriteLine($"      - {Path.GetFileName(f)}");
                }
            }

            // 验证源目录已清空
            var remainingFiles = Directory.GetFiles(sourceDir);
            if (remainingFiles.Length == 0)
            {
                Console.WriteLine();
                Console.WriteLine("✅ 源目录已全部清空，所有文件已归档");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"⚠️  源目录还有 {remainingFiles.Length} 个文件未归档:");
                foreach (var f in remainingFiles)
                    Console.WriteLine($"   - {Path.GetFileName(f)}");
            }
        }
        else
        {
            Console.WriteLine("❌ archive/ 目录未创建！");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("=========================================");
        Console.WriteLine("  ✅ 验证完成");
        Console.WriteLine("=========================================");
        return 0;
    }
}