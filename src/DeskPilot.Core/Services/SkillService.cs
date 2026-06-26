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
/// 技能服务默认实现（v0.10 升级）：
/// 1) 启动时加载嵌入式默认技能（8 个内置，IsBuiltIn=true）；
/// 2) 加载用户文件（%AppData%/DeskPilot/skills.json）里的所有技能（已安装的），
///    按 ID 合并：同 ID 用户字段覆盖默认；新增 ID 加入；
/// 3) ToggleAsync / InstallAsync / UninstallAsync 写回用户文件；
/// 容错：用户文件不存在 → 写默认；损坏 → 备份 + 用默认。
/// </summary>
public sealed class SkillService : ISkillService
{
    private const string DefaultJsonResource = "DeskPilot.Core.Resources.default-skills.json";
    private const string FileName = "skills.json";

    private readonly string _userFilePath;
    private readonly List<Skill> _all = new();
    private ISkillMarket? _market;  // 注入后可检查更新

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

    /// <summary>v0.10: 注入市场服务（用于 CheckUpdatesAsync）。</summary>
    public void SetMarket(ISkillMarket? market) => _market = market;

    public IReadOnlyList<Skill> All => _all.AsReadOnly();
    public IReadOnlyList<Skill> Enabled => _all.Where(s => s.IsEnabled).ToList().AsReadOnly();
    public IReadOnlyList<Skill> BuiltIn => _all.Where(s => s.IsBuiltIn).ToList().AsReadOnly();
    public IReadOnlyList<Skill> Custom => _all.Where(s => !s.IsBuiltIn).ToList().AsReadOnly();
    public event EventHandler? SkillsChanged;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        _all.Clear();
        foreach (var s in LoadDefaultSkills()) _all.Add(s);

        if (File.Exists(_userFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_userFilePath, ct).ConfigureAwait(false);
                var userSet = JsonSerializer.Deserialize<SkillSet>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (userSet?.Skills != null)
                {
                    foreach (var userSkill in userSet.Skills)
                    {
                        var idx = _all.FindIndex(s => s.Id == userSkill.Id);
                        if (idx >= 0)
                        {
                            // 同 ID：用户字段覆盖默认（保留 IsBuiltIn 标志）
                            _all[idx] = userSkill with { IsBuiltIn = _all[idx].IsBuiltIn };
                        }
                        else
                        {
                            // 新 ID（用户从市场安装的）→ 加入
                            _all.Add(userSkill);
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
            await SaveAsync(ct).ConfigureAwait(false);
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
        await SaveAsync(ct).ConfigureAwait(false);
        SkillsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task InstallAsync(Skill skill, CancellationToken ct = default)
    {
        if (skill is null) throw new ArgumentNullException(nameof(skill));
        if (string.IsNullOrWhiteSpace(skill.Id)) throw new ArgumentException("Skill.Id 不能为空", nameof(skill));

        // 内置技能不可"安装"（它们本来就存在）
        if (skill.IsBuiltIn)
            throw new InvalidOperationException($"内置技能 '{skill.Id}' 无需安装");

        // 强制标记为非内置（防御性：market 传 IsBuiltIn=true 也忽略）
        var toInstall = skill with { IsBuiltIn = false };

        var idx = _all.FindIndex(s => s.Id == toInstall.Id);
        if (idx >= 0)
        {
            // 覆盖更新（升级）
            _all[idx] = toInstall;
        }
        else
        {
            _all.Add(toInstall);
        }
        await SaveAsync(ct).ConfigureAwait(false);
        SkillsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task UninstallAsync(string skillId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(skillId)) throw new ArgumentException("skillId 不能为空", nameof(skillId));
        var idx = _all.FindIndex(s => s.Id == skillId);
        if (idx < 0) return;  // 静默：不存在即不报错
        var existing = _all[idx];
        if (existing.IsBuiltIn)
            throw new InvalidOperationException($"内置技能 '{skillId}' 不可卸载");
        _all.RemoveAt(idx);
        await SaveAsync(ct).ConfigureAwait(false);
        SkillsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<IReadOnlyDictionary<string, SkillUpdateInfo>> CheckUpdatesAsync(CancellationToken ct = default)
    {
        if (_market == null)
            return new Dictionary<string, SkillUpdateInfo>();
        return await _market.CheckUpdatesAsync(_all, ct).ConfigureAwait(false);
    }

    public Skill? FindById(string id) => _all.FirstOrDefault(s => s.Id == id);

    private async Task SaveAsync(CancellationToken ct = default)
    {
        var set = new SkillSet { Skills = _all.ToList() };
        var json = JsonSerializer.Serialize(set, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_userFilePath, json, ct).ConfigureAwait(false);
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
