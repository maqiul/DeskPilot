using DeskPilot.App.Models;
using DeskPilot.App.Services;
using System.IO;
using System.Text;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// SecureSettingsService 的单元测试。
/// 验证 DPAPI 加解密往返、文件 IO 边界、异常恢复。
/// </summary>
public class SecureSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TestableSecureSettingsService _service;

    public SecureSettingsServiceTests()
    {
        // 每个测试用独立的临时目录，避免污染
        _tempDir = Path.Combine(Path.GetTempPath(), "DeskPilotTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _service = new TestableSecureSettingsService(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { /* 忽略清理失败 */ }
    }

    [Fact]
    public void Load_FileDoesNotExist_ReturnsDefault()
    {
        var result = _service.Load();
        Assert.NotNull(result);
        Assert.Equal(AiProvider.OpenAI, result.Provider);
        Assert.Empty(result.OpenAiApiKey);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesAllFields()
    {
        var original = new AppSettings
        {
            Provider = AiProvider.DeepSeek,
            OpenAiApiKey = "sk-openai-test",
            OpenAiModel = "gpt-4o",
            DeepSeekApiKey = "sk-deepseek-test",
            DeepSeekModel = "deepseek-reasoner",
            OllamaEndpoint = "http://192.168.1.100:11434",
            OllamaModel = "llama3.1:8b"
        };

        _service.Save(original);
        var loaded = _service.Load();

        Assert.Equal(original.Provider, loaded.Provider);
        Assert.Equal(original.OpenAiApiKey, loaded.OpenAiApiKey);
        Assert.Equal(original.OpenAiModel, loaded.OpenAiModel);
        Assert.Equal(original.DeepSeekApiKey, loaded.DeepSeekApiKey);
        Assert.Equal(original.DeepSeekModel, loaded.DeepSeekModel);
        Assert.Equal(original.OllamaEndpoint, loaded.OllamaEndpoint);
        Assert.Equal(original.OllamaModel, loaded.OllamaModel);
    }

    [Fact]
    public void Save_OverwritesPreviousSettings()
    {
        _service.Save(new AppSettings
        {
            Provider = AiProvider.OpenAI,
            OpenAiApiKey = "first-key"
        });
        _service.Save(new AppSettings
        {
            Provider = AiProvider.Ollama,
            OllamaModel = "qwen2.5:14b"
        });

        var loaded = _service.Load();
        Assert.Equal(AiProvider.Ollama, loaded.Provider);
        Assert.Empty(loaded.OpenAiApiKey);
        Assert.Equal("qwen2.5:14b", loaded.OllamaModel);
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        var nestedDir = Path.Combine(_tempDir, "deep", "nested", "dir");
        var service = new TestableSecureSettingsService(nestedDir);

        service.Save(new AppSettings { OpenAiApiKey = "x" });

        Assert.True(Directory.Exists(nestedDir));
        Assert.True(File.Exists(service.SettingsFilePath));
    }

    [Fact]
    public void SavedFile_IsEncrypted_NotPlainJson()
    {
        var sensitiveKey = "sk-SUPER-SECRET-KEY-DO-NOT-LEAK";
        _service.Save(new AppSettings { OpenAiApiKey = sensitiveKey });

        // 读取原始字节验证没有明文
        var rawBytes = File.ReadAllBytes(_service.SettingsFilePath);
        var rawText = Encoding.UTF8.GetString(rawBytes);

        // 加密文件里不应该出现敏感 Key
        Assert.DoesNotContain(sensitiveKey, rawText);
        Assert.DoesNotContain("openAiApiKey", rawText); // 也没有明文 JSON 字段名
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsDefault()
    {
        // 写入垃圾数据
        File.WriteAllBytes(_service.SettingsFilePath, new byte[] { 0xFF, 0xFE, 0x00, 0x01 });

        var result = _service.Load();

        // 解密失败 → 回退到默认
        Assert.NotNull(result);
        Assert.Equal(AiProvider.OpenAI, result.Provider);
        Assert.Empty(result.OpenAiApiKey);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefault()
    {
        File.WriteAllBytes(_service.SettingsFilePath, Array.Empty<byte>());

        var result = _service.Load();

        Assert.NotNull(result);
        Assert.Equal(AiProvider.OpenAI, result.Provider);
    }

    [Fact]
    public void SettingsFilePath_IsUnderAppData()
    {
        var defaultService = new SecureSettingsService();
        var path = defaultService.SettingsFilePath;

        Assert.EndsWith("settings.dat", path);
        Assert.Contains("DeskPilot", path);
        // %APPDATA% 路径通常包含 "Roaming"
        Assert.True(Path.IsPathRooted(path));
    }

    /// <summary>
    /// 测试用子类：允许注入自定义目录（生产环境用 %APPDATA%，测试用临时目录）。
    /// </summary>
    private class TestableSecureSettingsService : SecureSettingsService
    {
        public TestableSecureSettingsService(string directory)
            : base(Path.Combine(directory, "settings.dat"))
        {
        }
    }
}