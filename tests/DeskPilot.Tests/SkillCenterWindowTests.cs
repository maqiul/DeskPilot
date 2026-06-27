using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.App.ViewModels;
using DeskPilot.Core.Models;
using DeskPilot.Core.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>v0.15 D1+D2: SkillCenterWindow / ViewModel 基础测试。
/// D1 验证 ObservableObject 结构（MarketplaceSourceNames + 3 ObservableCollection + StatusMessage）
/// D2 升级构造：注入 IMarketplaceSourceService + ISkillService 两个 Stub（避免真实网络）。</summary>
public class SkillCenterWindowTests
{
    [Fact]
    public void SkillCenterViewModel_Ctor_DoesNotThrow()
    {
        var vm = new SkillCenterViewModel(new StubMarketplace(), new StubSkillSvc());
        Assert.NotNull(vm);
        Assert.NotNull(vm.MarketplaceSourceNames);
        Assert.NotNull(vm.MarketSkillRows);
        Assert.NotNull(vm.InstalledSkills);
        Assert.NotNull(vm.UpdateAvailableSkills);
        Assert.NotNull(vm.MarketCategories);
    }

    [Fact]
    public void SkillCenterViewModel_HasThreeMarketplaceSources()
    {
        var vm = new SkillCenterViewModel(new StubMarketplace(), new StubSkillSvc());
        Assert.Equal(3, vm.MarketplaceSourceNames.Count);
        Assert.Contains("QwenPaw", vm.MarketplaceSourceNames);
        Assert.Contains("ClawHub", vm.MarketplaceSourceNames);
        Assert.Contains("ModelScope", vm.MarketplaceSourceNames);
    }

    [Fact]
    public void SkillCenterViewModel_HasThreeObservableCollections()
    {
        var vm = new SkillCenterViewModel(new StubMarketplace(), new StubSkillSvc());
        Assert.Equal(0, vm.MarketSkillRows.Count);
        Assert.Equal(0, vm.InstalledSkills.Count);
        Assert.Equal(0, vm.UpdateAvailableSkills.Count);
        // 默认 status 包含就绪或 emoji
        Assert.False(string.IsNullOrEmpty(vm.StatusMessage));
    }

    // ========== Minimal Stubs ==========

    private sealed class StubMarketplace : IMarketplaceSourceService
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public IReadOnlyList<string> SourceNames { get; } = new[] { "QwenPaw", "ClawHub", "ModelScope" };
        public ISkillMarket DefaultMarket => GetMarket("QwenPaw");
        public ISkillMarket GetMarket(string sourceName) => new StubMarket();
        public bool AddCustomSource(string name, string baseUrl) => false;
    }

    private sealed class StubMarket : ISkillMarket
    {
        public string BaseUrl => "stub";
        public string SourceName => "stub";
        public Task<SkillIndex> FetchIndexAsync(CancellationToken ct = default)
            => Task.FromResult(new SkillIndex { Skills = new List<SkillManifest>() });
        public Task<Skill> FetchSkillAsync(string id, CancellationToken ct = default)
            => Task.FromResult(new Skill(id, id, "", "🧩", "", new List<string>(), Version: "1.0.0"));
        public Task<IReadOnlyDictionary<string, SkillUpdateInfo>> CheckUpdatesAsync(
            IEnumerable<Skill> installed, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, SkillUpdateInfo>>(new Dictionary<string, SkillUpdateInfo>());
    }

    private sealed class StubSkillSvc : ISkillService
    {
        public IReadOnlyList<Skill> All => new List<Skill>();
        public IReadOnlyList<Skill> Enabled => new List<Skill>();
        public IReadOnlyList<Skill> BuiltIn => new List<Skill>();
        public IReadOnlyList<Skill> Custom => new List<Skill>();
        public event System.EventHandler? SkillsChanged;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ToggleAsync(string skillId, bool? enable = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task InstallAsync(Skill skill, CancellationToken ct = default) => Task.CompletedTask;
        public Task UninstallAsync(string skillId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<string, SkillUpdateInfo>> CheckUpdatesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, SkillUpdateInfo>>(new Dictionary<string, SkillUpdateInfo>());
        public Skill? FindById(string id) => null;
    }
}