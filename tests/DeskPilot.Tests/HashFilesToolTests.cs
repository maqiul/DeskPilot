using DeskPilot.Core.Tools;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace DeskPilot.Tests;

public class HashFilesToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly HashFilesTool _tool = new();

    public HashFilesToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "hash_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    private static string ComputeExpectedHash(string content, string algorithm)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        HashAlgorithm hasher = algorithm switch
        {
            "md5" => MD5.Create(),
            "sha1" => SHA1.Create(),
            "sha256" => SHA256.Create(),
            "sha512" => SHA512.Create(),
            _ => SHA256.Create()
        };
        return Convert.ToHexString(hasher.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static string E(string path) => path.Replace("\\", "\\\\");

    [Fact]
    public async Task Hash_DirectoryNotExists_ReturnsFail()
    {
        var missing = E(Path.Combine(_testDir, "nonexistent"));
        var json = string.Format("{{ \"directory\": \"{0}\" }}", missing);
        var result = await _tool.ExecuteAsync(json);
        Assert.False(result.Success);
        Assert.Contains("目录不存在", result.ErrorMessage);
    }

    [Fact]
    public async Task Hash_InvalidAlgorithm_ReturnsFail()
    {
        File.WriteAllText(Path.Combine(_testDir, "a.txt"), "hello");
        var dir = E(_testDir);
        var json = string.Format("{{ \"directory\": \"{0}\", \"algorithm\": \"fake_algo\" }}", dir);
        var result = await _tool.ExecuteAsync(json);
        Assert.False(result.Success);
        Assert.Contains("不支持的算法", result.ErrorMessage);
    }

    [Fact]
    public async Task Hash_DefaultSha256_CorrectHash()
    {
        File.WriteAllText(Path.Combine(_testDir, "a.txt"), "hello world");
        var dir = E(_testDir);
        var json = string.Format("{{ \"directory\": \"{0}\" }}", dir);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var report = Assert.IsType<HashReport>(result.Data);
        Assert.Equal("sha256", report.Algorithm);
        Assert.Equal(1, report.Hashed);
        var expected = ComputeExpectedHash("hello world", "sha256");
        Assert.Equal(expected, report.Details[0].Hash);
    }

    [Theory]
    [InlineData("md5")]
    [InlineData("sha1")]
    [InlineData("sha512")]
    public async Task Hash_AllAlgorithms_CorrectHash(string algo)
    {
        File.WriteAllText(Path.Combine(_testDir, "a.txt"), "test data");
        var dir = E(_testDir);
        var json = string.Format("{{ \"directory\": \"{0}\", \"algorithm\": \"{1}\" }}", dir, algo);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var report = Assert.IsType<HashReport>(result.Data);
        var expected = ComputeExpectedHash("test data", algo);
        Assert.Equal(expected, report.Details[0].Hash);
    }

    [Fact]
    public async Task Hash_PatternFilter_OnlyMatchingFiles()
    {
        File.WriteAllText(Path.Combine(_testDir, "a.txt"), "x");
        File.WriteAllText(Path.Combine(_testDir, "b.pdf"), "y");
        File.WriteAllText(Path.Combine(_testDir, "c.txt"), "z");
        var dir = E(_testDir);
        var json = string.Format("{{ \"directory\": \"{0}\", \"pattern\": \"*.txt\" }}", dir);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var report = Assert.IsType<HashReport>(result.Data);
        Assert.Equal(2, report.Hashed);
        Assert.All(report.Details, d => Assert.EndsWith(".txt", d.RelativePath));
    }

    [Fact]
    public async Task Hash_RecursiveTrue_FindsSubdirFiles()
    {
        File.WriteAllText(Path.Combine(_testDir, "top.txt"), "a");
        var subDir = Path.Combine(_testDir, "sub", "deep");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "deep.txt"), "b");
        var dir = E(_testDir);
        var json = string.Format("{{ \"directory\": \"{0}\", \"recursive\": true }}", dir);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var report = Assert.IsType<HashReport>(result.Data);
        Assert.Equal(2, report.Hashed);
    }

    [Fact]
    public async Task Hash_RecursiveFalse_OnlyTopDir()
    {
        File.WriteAllText(Path.Combine(_testDir, "top.txt"), "a");
        var subDir = Path.Combine(_testDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "sub.txt"), "b");
        var dir = E(_testDir);
        var json = string.Format("{{ \"directory\": \"{0}\", \"recursive\": false }}", dir);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var report = Assert.IsType<HashReport>(result.Data);
        Assert.Equal(1, report.Hashed);
    }

    [Fact]
    public async Task Hash_MultipleFiles_AllUniqueHashes()
    {
        File.WriteAllText(Path.Combine(_testDir, "a.txt"), "content A");
        File.WriteAllText(Path.Combine(_testDir, "b.txt"), "content B");
        File.WriteAllText(Path.Combine(_testDir, "c.txt"), "content C");
        var dir = E(_testDir);
        var json = string.Format("{{ \"directory\": \"{0}\" }}", dir);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var report = Assert.IsType<HashReport>(result.Data);
        Assert.Equal(3, report.Hashed);
        var hashes = report.Details.Select(d => d.Hash).ToList();
        Assert.Equal(3, hashes.Distinct().Count());
    }

    [Fact]
    public async Task Hash_SameContent_DuplicateHashes()
    {
        File.WriteAllText(Path.Combine(_testDir, "a.txt"), "same content");
        File.WriteAllText(Path.Combine(_testDir, "b.txt"), "same content");
        var dir = E(_testDir);
        var json = string.Format("{{ \"directory\": \"{0}\" }}", dir);
        var result = await _tool.ExecuteAsync(json);
        Assert.True(result.Success);
        var report = Assert.IsType<HashReport>(result.Data);
        var hashes = report.Details.Select(d => d.Hash).ToList();
        Assert.Equal(2, hashes.Count);
        Assert.Equal(hashes[0], hashes[1]);
    }
}