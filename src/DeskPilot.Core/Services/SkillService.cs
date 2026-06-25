using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.Core.Models;

namespace DeskPilot.Core.Services;

/// <summary>
/// 技能服务默认实现：
/// 1) 启动时加载嵌入式默认技能（8 个内置）；
/// 2) 合并用户文件（%AppData%/DeskPilot/skills.json）里的禁用/启用状态；
/// 3) ToggleAsync 写回用户文件；
/// 容错：用户文件不存在 → 用默认；损坏 → 备份 + 用默认。
/// </summary>
public sealed class SkillService : ISkillService
{
    private const string DefaultJsonResource = "DeskPilot.Core.Resources.default-skills.json";
    private const string FileName = "skills.json";

    private readonly string _userFilePath;
    private readonly List<Skill> _all = new();

    public SkillService(string? appDataDirectory = null)
    {
        var dir = appDataDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskPilot");
        Directory.CreateDirectory(dir);
        _userFilePath = Path.Combine(dir, FileName);
    }

    /// <summary>注入用户文件路径（测试用）。</summary>
    public static SkillService ForTesting(string userFilePath)
        => new(userFilePath, _testing: true);

    private SkillService(string userFilePath, bool _testing)
    {
        _userFilePath = userFilePath;
        var dir = Path.GetDirectoryName(userFilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public IReadOnlyList<Skill> All => _all.AsReadOnly();
    public IReadOnlyList<Skill> Enabled => _all.Where(s => s.IsEnabled).ToList().AsReadOnly();
    public event EventHandler? SkillsChanged;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        _all.Clear();
        foreach (var s in LoadDefaultSkills()) _all.Add(s);

        if (File.Exists(_userFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_userFilePath, ct);
                var userSet = JsonSerializer.Deserialize<SkillSet>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (userSet?.Skills != null)
                {
                    foreach (var userSkill in userSet.Skills)
                    {
                        var idx = _all.FindIndex(s => s.Id == userSkill.Id);
                        if (idx >= 0)
                        {
                            // 合并：保留默认信息，只改 IsEnabled
                            _all[idx] = _all[idx] with { IsEnabled = userSkill.IsEnabled };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 容错：备份损坏文件 + 用默认
                var backup = $"{_userFilePath}.corrupted.{DateTime.Now:yyyyMMddHHmmss}";
                try { File.Move(_userFilePath, backup); } catch { /* ignore */ }
                Console.Error.WriteLine($"[SkillService] 用户文件损坏已备份: {backup}, 原因: {ex.Message}");
            }
        }
        else
        {
            // 首次启动：写一份默认配置
            await SaveAsync(ct);
        }

        SkillsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ToggleAsync(string skillId, bool? enable = null, CancellationToken ct = default)
    {
        var idx = _all.FindIndex(s => s.Id == skillId);
        if (idx < 0) return;
        var current = _all[idx];
        var newEnabled = enable ?? !current.IsEnabled;
        _all[idx] = current with { IsEnabled = newEnabled };
        await SaveAsync(ct);
        SkillsChanged?.Invoke(this, EventArgs.Empty);
    }

    public Skill? FindById(string id) => _all.FirstOrDefault(s => s.Id == id);

    private async Task SaveAsync(CancellationToken ct = default)
    {
        var set = new SkillSet { Skills = _all.ToList() };
        var json = JsonSerializer.Serialize(set, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_userFilePath, json, ct);
    }

    private static List<Skill> LoadDefaultSkills()
    {
        var asm = typeof(SkillService).Assembly;
        using var stream = asm.GetManifestResourceStream(DefaultJsonResource)
            ?? throw new InvalidOperationException(
                $"找不到嵌入资源 {DefaultJsonResource}。已注册: " +
                string.Join(", ", asm.GetManifestResourceNames()));
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<List<Skill>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }
}
