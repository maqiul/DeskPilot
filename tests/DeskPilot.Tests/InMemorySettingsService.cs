using DeskPilot.App.Models;
using DeskPilot.App.Services;

namespace DeskPilot.Tests;

/// <summary>
/// 内存版设置服务（共享给所有 SettingsViewModel 测试）。
/// 用计数 + Stored 属性替代真实 DPAPI，避免文件 IO。
/// </summary>
public sealed class InMemorySettingsService : ISettingsService
{
    public AppSettings Stored { get; set; } = AppSettings.Default;
    public int SaveCallCount { get; private set; }
    public int LoadCallCount { get; private set; }
    public string SettingsFilePath => "<in-memory>";

    public AppSettings Load()
    {
        LoadCallCount++;
        return Stored;
    }

    public void Save(AppSettings settings)
    {
        SaveCallCount++;
        Stored = settings;
    }
}