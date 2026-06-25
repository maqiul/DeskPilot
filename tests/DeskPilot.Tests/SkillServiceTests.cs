using DeskPilot.Core.Services;

namespace DeskPilot.Tests;

/// <summary>
/// v0.9: SkillService 测试（基于临时文件，不污染 AppData）。
/// </summary>
public class SkillServiceTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly string _userFile;

    public SkillServiceTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "DeskPilotTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
        _userFile = Path.Combine(_tmpDir, "skills.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmpDir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task LoadAsync_Loads8DefaultSkills()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();

        Assert.Equal(8, svc.All.Count);
        Assert.True(File.Exists(_userFile), "首次加载应该创建用户文件");
    }

    [Fact]
    public async Task LoadAsync_AllEnabled_ByDefault()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();

        Assert.All(svc.All, s => Assert.True(s.IsEnabled, $"默认应该启用: {s.Id}"));
        Assert.Equal(8, svc.Enabled.Count);
    }

    [Fact]
    public async Task ToggleAsync_DisablesSkill_AndPersists()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();

        await svc.ToggleAsync("organize-downloads", enable: false);

        Assert.False(svc.FindById("organize-downloads")!.IsEnabled);
        Assert.Equal(7, svc.Enabled.Count);

        // 新实例应该能恢复禁用状态
        var svc2 = SkillService.ForTesting(_userFile);
        await svc2.LoadAsync();
        Assert.False(svc2.FindById("organize-downloads")!.IsEnabled);
        Assert.Equal(7, svc2.Enabled.Count);
    }

    [Fact]
    public async Task ToggleAsync_NullFlipsCurrentState()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();

        var original = svc.FindById("find-duplicate-photos")!.IsEnabled;
        await svc.ToggleAsync("find-duplicate-photos");

        Assert.Equal(!original, svc.FindById("find-duplicate-photos")!.IsEnabled);
    }

    [Fact]
    public async Task ToggleAsync_UnknownId_NoOp()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();

        await svc.ToggleAsync("nonexistent-skill"); // 不应抛异常

        Assert.Equal(8, svc.All.Count);
    }

    [Fact]
    public async Task LoadAsync_CorruptedFile_BackupAndFallback()
    {
        await File.WriteAllTextAsync(_userFile, "{这不是合法 JSON");

        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync(); // 不应抛异常

        Assert.Equal(8, svc.All.Count);
        // 损坏文件应该被备份
        var backups = Directory.GetFiles(_tmpDir, "skills.json.corrupted.*");
        Assert.NotEmpty(backups);
    }

    [Fact]
    public async Task SkillsChanged_FiresOnLoadAndToggle()
    {
        var svc = SkillService.ForTesting(_userFile);
        int count = 0;
        svc.SkillsChanged += (_, _) => count++;

        await svc.LoadAsync();
        Assert.True(count >= 1, "LoadAsync 应触发 SkillsChanged");

        await svc.ToggleAsync("organize-downloads");
        Assert.True(count >= 2, "ToggleAsync 应触发 SkillsChanged");
    }
}
