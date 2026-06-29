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

    // ========== v0.16 C: SkillDetailWindow 集成测试（XAML 文本验证 + cs 代码验证） ==========

    [Fact]
    public void SkillCenterWindow_Xaml_HasMarketCardClickHandler()
    {
        // v0.16 C: Market Tab 卡片必须绑定 MouseLeftButtonUp 事件 + Tag 传 skill id
        var xaml = File.ReadAllText(GetSkillCenterWindowXamlPath());
        Assert.Contains("MouseLeftButtonUp=\"MarketSkillCard_Click\"", xaml);
        Assert.Contains("Tag=\"{Binding Id}\"", xaml);
        Assert.Contains("Cursor=\"Hand\"", xaml);
    }

    [Fact]
    public void SkillCenterWindow_CodeBehind_HasMarketCardClickHandler()
    {
        // v0.16 C: code-behind 必须有 MarketSkillCard_Click 方法 + 创建 SkillDetailWindow + ShowDialog
        var cs = File.ReadAllText(GetSkillCenterWindowCsPath());
        Assert.Contains("MarketSkillCard_Click", cs);
        Assert.Contains("new SkillDetailWindow(detailVm)", cs);
        Assert.Contains("ShowDialog()", cs);
    }

    [Fact]
    public void SkillCenterWindow_CodeBehind_ResolvesSkillServicesFromApp()
    {
        // v0.16 C: 通过 App.Services 拿 ISkillService + ISkillMarket（不污染 ViewModel）
        var cs = File.ReadAllText(GetSkillCenterWindowCsPath());
        Assert.Contains("App.Services", cs);
        Assert.Contains("GetService<ISkillService>", cs);
        Assert.Contains("GetService<ISkillMarket>", cs);
    }

    private static string GetSkillCenterWindowXamlPath()
    {
        // 从 tests/DeskPilot.Tests/ 向上找到 src/DeskPilot.App/Views/SkillCenterWindow.xaml
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "DeskPilot.App", "Views", "SkillCenterWindow.xaml");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("SkillCenterWindow.xaml not found");
    }

    private static string GetSkillCenterWindowCsPath()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "DeskPilot.App", "Views", "SkillCenterWindow.xaml.cs");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("SkillCenterWindow.xaml.cs not found");
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