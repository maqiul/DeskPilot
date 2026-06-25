using DeskPilot.Core.Tools;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace DeskPilot.Tests;

public class ExtractArchiveToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly ExtractArchiveTool _tool = new();

    public ExtractArchiveToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "extract_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    private string CreateTestZip(string zipName, Dictionary<string, string> files)
    {
        var zipPath = Path.Combine(_testDir, zipName);
        using var fs = File.Create(zipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var kvp in files)
        {
            var entry = archive.CreateEntry(kvp.Key);
            using var es = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(kvp.Value);
            es.Write(bytes, 0, bytes.Length);
        }
        return zipPath;
    }

    private static string E(string path) => path.Replace("\\", "\\\\");

    [Fact]
    public async Task Extract_ArchiveNotFound_ReturnsFail()
    {
        var missing = E(Path.Combine(_testDir, "nonexistent.zip"));
        var json = string.Format("{{ \"archivePath\": \"{0}\" }}", missing);
        var result = await _tool.ExecuteAsync(json);
        Assert.False(result.Success);
        Assert.Contains("压缩文件不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task Extract_NonZipFile_ReturnsFail()
    {
        var txt = E(Path.Combine(_testDir, "test.txt"));
        File.WriteAllText(Path.Combine(_testDir, "test.txt"), "hello");
        var json = string.Format("{{ \"archivePath\": \"{0}\" }}", txt);
        var result = await _tool.ExecuteAsync(json);
        Assert.False(result.Success);
        Assert.Contains("zip", result.ErrorMessage!);
    }

    [Fact]
    public async Task Extract_DefaultOutputDir_ExtractsToSiblingDir()
    {
        var zip = CreateTestZip("receipts.zip", new Dictionary<string, string>
        {
            ["jan/inv1.txt"] = "invoice 1 content",
            ["jan/inv2.txt"] = "invoice 2 content",
            ["readme.txt"] = "top-level readme"
        });
        var zipEsc = E(zip);
        var json = string.Format("{{ \"archivePath\": \"{0}\" }}", zipEsc);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var report = Assert.IsType<ExtractReport>(result.Data);
        Assert.Equal(3, report.Extracted);
        Assert.True(File.Exists(Path.Combine(_testDir, "receipts", "jan", "inv1.txt")));
        Assert.True(File.Exists(Path.Combine(_testDir, "receipts", "jan", "inv2.txt")));
        Assert.True(File.Exists(Path.Combine(_testDir, "receipts", "readme.txt")));
    }

    [Fact]
    public async Task Extract_CustomOutputDir_ExtractsThere()
    {
        var zip = CreateTestZip("a.zip", new Dictionary<string, string>
        {
            ["f.txt"] = "hello"
        });
        var outDir = E(Path.Combine(_testDir, "custom_out"));
        var zipEsc = E(zip);
        var json = string.Format("{{ \"archivePath\": \"{0}\", \"outputDirectory\": \"{1}\" }}", zipEsc, outDir);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(_testDir, "custom_out", "f.txt")));
    }

    [Fact]
    public async Task Extract_FileExistsNoOverwrite_SkipsFile()
    {
        var zip = CreateTestZip("a.zip", new Dictionary<string, string>
        {
            ["existing.txt"] = "new content"
        });
        var extractDir = Path.Combine(_testDir, "out");
        Directory.CreateDirectory(extractDir);
        var existingFile = Path.Combine(extractDir, "existing.txt");
        File.WriteAllText(existingFile, "old content");

        var zipEsc = E(zip);
        var outEsc = E(extractDir);
        var json = string.Format("{{ \"archivePath\": \"{0}\", \"outputDirectory\": \"{1}\", \"overwrite\": false }}", zipEsc, outEsc);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        Assert.Equal("old content", File.ReadAllText(existingFile));
        var report = Assert.IsType<ExtractReport>(result.Data);
        Assert.Equal(1, report.Skipped);
    }

    [Fact]
    public async Task Extract_FileExistsOverwriteTrue_OverwritesFile()
    {
        var zip = CreateTestZip("a.zip", new Dictionary<string, string>
        {
            ["existing.txt"] = "new content"
        });
        var extractDir = Path.Combine(_testDir, "out");
        Directory.CreateDirectory(extractDir);
        var existingFile = Path.Combine(extractDir, "existing.txt");
        File.WriteAllText(existingFile, "old content");

        var zipEsc = E(zip);
        var outEsc = E(extractDir);
        var json = string.Format("{{ \"archivePath\": \"{0}\", \"outputDirectory\": \"{1}\", \"overwrite\": true }}", zipEsc, outEsc);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        Assert.Equal("new content", File.ReadAllText(existingFile));
    }

    [Fact]
    public async Task Extract_ZipSlip_RejectsMaliciousEntry()
    {
        var zipPath = Path.Combine(_testDir, "evil.zip");
        using (var fs = File.Create(zipPath))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../../../etc/passwd");
            using var es = entry.Open();
            var bytes = Encoding.UTF8.GetBytes("malicious");
            es.Write(bytes, 0, bytes.Length);
        }

        var zipEsc = E(zipPath);
        var outEsc = E(Path.Combine(_testDir, "safe"));
        var json = string.Format("{{ \"archivePath\": \"{0}\", \"outputDirectory\": \"{1}\" }}", zipEsc, outEsc);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var report = Assert.IsType<ExtractReport>(result.Data);
        Assert.Equal(1, report.Failed);
        Assert.Contains("Zip Slip", report.Details[0].Message!);
    }

    [Fact]
    public async Task Extract_InvalidZip_ReturnsFail()
    {
        var fakeZip = Path.Combine(_testDir, "fake.zip");
        File.WriteAllText(fakeZip, "not a real zip file");
        var json = string.Format("{{ \"archivePath\": \"{0}\" }}", E(fakeZip));
        var result = await _tool.ExecuteAsync(json);
        Assert.False(result.Success);
        Assert.Contains("zip", result.ErrorMessage!);
    }
}