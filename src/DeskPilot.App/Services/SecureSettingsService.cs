using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeskPilot.App.Models;

namespace DeskPilot.App.Services;

/// <summary>
/// 设置服务接口。
/// </summary>
public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
    string SettingsFilePath { get; }
}

/// <summary>
/// 基于 Windows DPAPI 加密的设置服务实现。
/// 存储位置：%APPDATA%\DeskPilot\settings.dat
/// 只有当前 Windows 用户能解密。
/// </summary>
public class SecureSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _settingsFilePath;

    /// <summary>
    /// 生产环境默认路径：%APPDATA%\DeskPilot\settings.dat
    /// </summary>
    public virtual string SettingsFilePath => _settingsFilePath;

    /// <summary>
    /// 生产构造：使用默认 %APPDATA% 路径。
    /// </summary>
    public SecureSettingsService()
    {
        _settingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DeskPilot",
            "settings.dat");
    }

    /// <summary>
    /// 测试构造：允许注入自定义路径（仅供单元测试使用）。
    /// </summary>
    protected SecureSettingsService(string overridePath)
    {
        _settingsFilePath = overridePath;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
            return AppSettings.Default;

        var encryptedBytes = File.ReadAllBytes(_settingsFilePath);

        try
        {
            var plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.Default;
        }
        catch (CryptographicException)
        {
            // 解密失败（用户切换 / 文件损坏 / DPAPI 主密钥缺失）→ 返回默认
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encryptedBytes = ProtectedData.Protect(
            plainBytes,
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_settingsFilePath, encryptedBytes);
    }
}