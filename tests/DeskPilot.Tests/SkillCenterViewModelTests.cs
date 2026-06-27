using DeskPilot.App.ViewModels;
using DeskPilot.Core.Models;
using DeskPilot.Core.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>v0.15 D2: SkillCenterViewModel 业务逻辑测试。
/// 用 Stub 服务避免真实网络：StubMarketplaceSourceService（3 源）+ StubSkillMarket + StubSkillService。
/// CommunityToolkit.Mvvm [RelayCommand] 生成规则：
///   async Task XxxAsync(...) → IAsyncRelayCommand 属性名 XxxCommand（去 Async 后缀）
///   void Xxx()             → IRelayCommand 属性名 XxxCommand
/// 测试必须用 ExecuteAsync(null) 调 IAsyncRelayCommand。</summary>
public class SkillCenterViewModelTests
{
    [Fact]
    public void Ctor_DoesNotThrow_AndPopulatesSources()
    {
        var vm = CreateViewModel(out _, out _);
        Assert.NotNull(vm);
        Assert.Equal("QwenPaw", vm.SelectedMarketSource);
        Assert.Equal(3, vm.MarketplaceSourceNames.Count);
        Assert.Contains("QwenPaw", vm.MarketplaceSourceNames);
        Assert.Contains("ClawHub", vm.MarketplaceSourceNames);
        Assert.Contains("ModelScope", vm.MarketplaceSourceNames);
        Assert.Equal(8, vm.MarketCategories.Count);
        Assert.Empty(vm.MarketSkillRows);
        Assert.Empty(vm.InstalledSkills);
        Assert.Empty(vm.UpdateAvailableSkills);
    }

    [Fact]
    public async Task LoadMarket_PopulatesMarketSkillRows()
    {
        var vm = CreateViewModel(out var stubMarket, out _);
        stubMarket.IndexSkills = new List<SkillManifest>
        {
            new SkillManifest(
                Id: "skill-a",
                Name: "技能 A",
                Description: "测试 A",
                Icon: "🧩",
                Category: "开发工具",
                Author: "test",
                Version: "1.0.0",
                Tags: new List<string>()),
            new SkillManifest(
                Id: "skill-b",
                Name: "技能 B",
                Description: "测试 B",
                Icon: "🧩",
                Category: "文档处理",
                Author: "test",
                Version: "1.0.0",
                Tags: new List<string>()),
        };

        await vm.LoadMarketCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.MarketSkillRows.Count);
        Assert.Contains("技能 A", vm.MarketSkillRows.Select(r => r.Name));
        Assert.Contains("技能 B", vm.MarketSkillRows.Select(r => r.Name));
        Assert.Contains("拉到", vm.StatusMessage);
    }

    [Fact]
    public async Task Install_DelegatesToSkillService()
    {
        var vm = CreateViewModel(out var stubMarket, out var stubSvc);
        stubMarket.SkillToReturn = new Skill(
            Id: "test-skill",
            Name: "测试技能",
            Description: "",
            Icon: "🧩",
            PromptTemplate: "",
            Tools: new List<string>(),
            Version: "1.0.0");

        await vm.InstallCommand.ExecuteAsync("test-skill");

        Assert.Single(stubSvc.Installed);
        Assert.Equal("test-skill", stubSvc.Installed[0].Id);
        Assert.Contains("安装", vm.StatusMessage);
    }

    [Fact]
    public async Task Uninstall_DelegatesToSkillService()
    {
        var vm = CreateViewModel(out _, out var stubSvc);
        stubSvc.CustomSkills.Add(new Skill(
            Id: "custom-skill",
            Name: "X",
            Description: "",
            Icon: "🧩",
            PromptTemplate: "",
            Tools: new List<string>(),
            Version: "1.0.0",
            IsBuiltIn: false));

        await vm.UninstallCommand.ExecuteAsync("custom-skill");

        Assert.Contains("custom-skill", stubSvc.Uninstalled);
        Assert.Contains("已卸载", vm.StatusMessage);
    }

    [Fact]
    public async Task LoadUpdates_FiltersOnlyHasUpdateTrue()
    {
        var vm = CreateViewModel(out _, out var stubSvc);
        stubSvc.UpdatesToReturn = new Dictionary<string, SkillUpdateInfo>
        {
            ["updatable"] = new SkillUpdateInfo("updatable", "1.0.0", "2.0.0", HasUpdate: true),
            ["latest"] = new SkillUpdateInfo("latest", "1.0.0", "1.0.0", HasUpdate: false),
            ["outdated"] = new SkillUpdateInfo("outdated", "1.0.0", "1.5.0", HasUpdate: true),
        };

        await vm.LoadUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.UpdateAvailableSkills.Count);
        Assert.All(vm.UpdateAvailableSkills, u => Assert.True(u.HasUpdate));
        Assert.Contains(vm.UpdateAvailableSkills, u => u.Id == "updatable");
        Assert.Contains(vm.UpdateAvailableSkills, u => u.Id == "outdated");
        Assert.DoesNotContain(vm.UpdateAvailableSkills, u => u.Id == "latest");
    }

    // ========== Stubs ==========

    private static SkillCenterViewModel CreateViewModel(out StubMarketplaceSourceService stubMarket, out StubSkillService stubSvc)
    {
        stubMarket = new StubMarketplaceSourceService();
        stubSvc = new StubSkillService();
        return new SkillCenterViewModel(stubMarket, stubSvc);
    }

    private sealed class StubMarketplaceSourceService : IMarketplaceSourceService
    {
        public List<SkillManifest> IndexSkills { get; set; } = new();
        public Skill? SkillToReturn { get; set; }

        // 共享的 StubSkillMarket（按 sourceName 缓存），让测试可以预填 IndexSkills
        private readonly Dictionary<string, StubSkillMarket> _markets = new();

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public IReadOnlyList<string> SourceNames { get; } = new[] { "QwenPaw", "ClawHub", "ModelScope" };

        public ISkillMarket DefaultMarket => GetMarket("QwenPaw");

        public ISkillMarket GetMarket(string sourceName)
        {
            if (!_markets.TryGetValue(sourceName, out var m))
            {
                m = new StubSkillMarket { IndexSkills = IndexSkills, SkillToReturn = SkillToReturn };
                _markets[sourceName] = m;
            }
            // 始终同步最新引用
            m.IndexSkills = IndexSkills;
            m.SkillToReturn = SkillToReturn;
            return m;
        }

        public bool AddCustomSource(string name, string baseUrl) => false;
    }

    private sealed class StubSkillMarket : ISkillMarket
    {
        public List<SkillManifest> IndexSkills { get; set; } = new();
        public Skill? SkillToReturn { get; set; }
        public string BaseUrl => "https://stub.local";
        public string SourceName => "Stub";

        public Task<SkillIndex> FetchIndexAsync(CancellationToken ct = default)
            => Task.FromResult(new SkillIndex { Skills = IndexSkills });

        public Task<Skill> FetchSkillAsync(string id, CancellationToken ct = default)
            => Task.FromResult(SkillToReturn ?? new Skill(
                Id: id,
                Name: id,
                Description: "",
                Icon: "🧩",
                PromptTemplate: "",
                Tools: new List<string>(),
                Version: "1.0.0"));

        public Task<IReadOnlyDictionary<string, SkillUpdateInfo>> CheckUpdatesAsync(
            IEnumerable<Skill> installed, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, SkillUpdateInfo>>(new Dictionary<string, SkillUpdateInfo>());
    }

    private sealed class StubSkillService : ISkillService
    {
        public List<Skill> CustomSkills { get; } = new();
        public List<Skill> Installed { get; } = new();
        public List<string> Uninstalled { get; } = new();
        public Dictionary<string, SkillUpdateInfo> UpdatesToReturn { get; set; } = new();

        public IReadOnlyList<Skill> All => new List<Skill>
        {
            new Skill(
                Id: "builtin-1",
                Name: "内置 1",
                Description: "",
                Icon: "🧩",
                PromptTemplate: "",
                Tools: new List<string>(),
                Version: "1.0.0",
                IsBuiltIn: true)
        }.Concat(CustomSkills).ToList();

        public IReadOnlyList<Skill> Enabled => All;
        public IReadOnlyList<Skill> BuiltIn => All.Where(s => s.IsBuiltIn).ToList();
        public IReadOnlyList<Skill> Custom => CustomSkills;

        public event System.EventHandler? SkillsChanged;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ToggleAsync(string skillId, bool? enable = null, CancellationToken ct = default) => Task.CompletedTask;

        public Task InstallAsync(Skill skill, CancellationToken ct = default)
        {
            Installed.Add(skill);
            SkillsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task UninstallAsync(string skillId, CancellationToken ct = default)
        {
            Uninstalled.Add(skillId);
            CustomSkills.RemoveAll(s => s.Id == skillId);
            SkillsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, SkillUpdateInfo>> CheckUpdatesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, SkillUpdateInfo>>(UpdatesToReturn);

        public Skill? FindById(string id) => All.FirstOrDefault(s => s.Id == id);
    }
}