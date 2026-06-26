using DeskPilot.Core.Models;
using DeskPilot.Core.Services;

namespace DeskPilot.Tests;

/// <summary>测试用：构造一个来自市场的 Skill（IsBuiltIn=false, Source=market:community）。</summary>
internal static class TestSkills
{
    public static Skill Market(string id, string version = "1.0.0") => new(
        Id: id, Name: id, Description: "desc", Icon: "🧩",
        PromptTemplate: "请帮我", Tools: Array.Empty<string>(),
        Category: "测试", IsEnabled: true, IsBuiltIn: false,
        Source: "market:community", Version: version);
}

/// <summary>
/// v0.9: SkillService 测试（基于临时文件，不污染 AppData）。
/// v0.10: 扩展 — InstallAsync / UninstallAsync / CheckUpdatesAsync / BuiltIn / Custom。
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

    // ---- v0.10: 技能市场相关 ----

    [Fact]
    public async Task BuiltIn_ReturnsOnlyBuiltinSkills()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();
        Assert.Equal(8, svc.BuiltIn.Count);
        Assert.All(svc.BuiltIn, s => Assert.True(s.IsBuiltIn));
    }

    [Fact]
    public async Task Custom_EmptyBeforeInstall()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();
        Assert.Empty(svc.Custom);
    }

    [Fact]
    public async Task InstallAsync_AddsNewSkill_AndFiresChanged()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();

        int changedCount = 0;
        svc.SkillsChanged += (_, _) => changedCount++;

        var market = TestSkills.Market("my-test-skill");
        await svc.InstallAsync(market);

        Assert.Equal(9, svc.All.Count);
        Assert.Single(svc.Custom);
        Assert.True(svc.FindById("my-test-skill")!.IsEnabled);
        Assert.False(svc.FindById("my-test-skill")!.IsBuiltIn);
        Assert.Equal("market:community", svc.FindById("my-test-skill")!.Source);
        Assert.True(changedCount >= 1);
    }

    [Fact]
    public async Task InstallAsync_UpgradeExisting_ReplacesVersion()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();
        await svc.InstallAsync(TestSkills.Market("my-skill", "1.0.0"));
        await svc.InstallAsync(TestSkills.Market("my-skill", "1.1.0"));

        Assert.Single(svc.Custom);
        Assert.Equal("1.1.0", svc.FindById("my-skill")!.Version);
    }

    [Fact]
    public async Task InstallAsync_RejectsBuiltInSkill()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();

        var builtin = new Skill("test-builtin", "T", "", "🧪", "", Array.Empty<string>(),
            IsBuiltIn: true, Source: "builtin");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.InstallAsync(builtin));
    }

    [Fact]
    public async Task InstallAsync_RejectsNullOrEmptyId()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => svc.InstallAsync(null!));

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.InstallAsync(new Skill("", "T", "", "🧪", "", Array.Empty<string>())));
    }

    [Fact]
    public async Task InstallAsync_PersistsToFile()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();
        await svc.InstallAsync(TestSkills.Market("persisted-skill"));

        var svc2 = SkillService.ForTesting(_userFile);
        await svc2.LoadAsync();
        Assert.NotNull(svc2.FindById("persisted-skill"));
    }

    [Fact]
    public async Task UninstallAsync_RemovesCustomSkill_AndFiresChanged()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();
        await svc.InstallAsync(TestSkills.Market("temp-skill"));

        int changedCount = 0;
        svc.SkillsChanged += (_, _) => changedCount++;

        await svc.UninstallAsync("temp-skill");

        Assert.Null(svc.FindById("temp-skill"));
        Assert.Empty(svc.Custom);
        Assert.True(changedCount >= 1);
    }

    [Fact]
    public async Task UninstallAsync_RejectsBuiltInSkill()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UninstallAsync("organize-downloads"));

        // 内置技能仍在
        Assert.NotNull(svc.FindById("organize-downloads"));
    }

    [Fact]
    public async Task UninstallAsync_UnknownId_NoOp()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();

        await svc.UninstallAsync("nonexistent-skill"); // 不应抛

        Assert.Equal(8, svc.All.Count);
    }

    [Fact]
    public async Task UninstallAsync_PersistsToFile()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();
        await svc.InstallAsync(TestSkills.Market("temp-skill"));
        await svc.UninstallAsync("temp-skill");

        var svc2 = SkillService.ForTesting(_userFile);
        await svc2.LoadAsync();
        Assert.Null(svc2.FindById("temp-skill"));
    }

    [Fact]
    public async Task CheckUpdatesAsync_NoMarket_ReturnsEmpty()
    {
        var svc = SkillService.ForTesting(_userFile);
        await svc.LoadAsync();

        var updates = await svc.CheckUpdatesAsync();
        Assert.Empty(updates);
    }
}

// ---- v0.10: 技能市场 Markdown 解析测试 ----

public class SkillMarketServiceTests
{
    [Fact]
    public void ParseIndexFromMarkdown_ParsesValidTable()
    {
        var md = """
            | id | name | description | icon | category | author | version |
            | --- | --- | --- | --- | --- | --- | --- |
            | scan-invoices | 扫描发票 | 扫描 PDF | 🧾 | 财务 | community | 1.0.0 |
            | weekly-report | 周报助手 | 生成周报 | 📝 | 文档 | maqiul | 0.9.0 |
            """;

        var index = SkillMarketService.ParseIndexFromMarkdown(md);

        Assert.Equal(2, index.Skills.Count);
        Assert.Equal("scan-invoices", index.Skills[0].Id);
        Assert.Equal("🧾", index.Skills[0].Icon);
        Assert.Equal("财务", index.Skills[0].Category);
        Assert.Equal("maqiul", index.Skills[1].Author);
    }

    [Fact]
    public void ParseIndexFromMarkdown_SkipsHeaderAndSeparator()
    {
        var md = """
            | id | name | description | icon | category | author | version |
            | --- | --- | --- | --- | --- | --- | --- |
            | real-skill | 真实技能 | 描述 | 🅰 | 测试 | author | 1.0.0 |
            """;

        var index = SkillMarketService.ParseIndexFromMarkdown(md);
        Assert.Single(index.Skills);
        Assert.Equal("real-skill", index.Skills[0].Id);
    }

    [Fact]
    public void ParseIndexFromMarkdown_IgnoresEmptyAndInvalidLines()
    {
        var md = """
            这是介绍文字，不以 | 开头

            | id | name | description | icon | category | author | version |
            | --- | --- | --- | --- | --- | --- | --- |
            | good | 好 | desc | ✅ | 测试 | a | 1.0.0 |
            | 只有四列 | 不够 |
            """;

        var index = SkillMarketService.ParseIndexFromMarkdown(md);
        Assert.Single(index.Skills);
        Assert.Equal("good", index.Skills[0].Id);
    }

    [Fact]
    public void CompareVersions_ReturnsCorrectOrder()
    {
        Assert.True(SkillMarketService.CompareVersions("1.0.0", "1.0.1") < 0);
        Assert.True(SkillMarketService.CompareVersions("1.0.1", "1.0.0") > 0);
        Assert.Equal(0, SkillMarketService.CompareVersions("1.0.0", "1.0.0"));
        Assert.True(SkillMarketService.CompareVersions("1.0", "1.0.0") == 0);  // 短版本补 0
        Assert.True(SkillMarketService.CompareVersions("", "1.0.0") < 0);
        Assert.Equal(0, SkillMarketService.CompareVersions("", ""));
    }

    [Fact]
    public async Task FetchSkillAsync_MockHttp_ReturnsParsedSkill()
    {
        // 用 DelegatingHandler mock 拉取单个技能
        var handler = new MockHttpHandler((req) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/my-skill.json"))
            {
                return """{"Id":"my-skill","Name":"我的","Description":"d","Icon":"🧩","PromptTemplate":"p","Tools":[],"Category":"测试","Version":"1.0.0","Source":"market:test"}""";
            }
            return "{}";
        });
        var http = new HttpClient(handler);
        var market = new SkillMarketService(http, "https://example.com/skills");

        var skill = await market.FetchSkillAsync("my-skill");
        Assert.Equal("my-skill", skill.Id);
        Assert.Equal("market:test", skill.Source);
    }

    [Fact]
    public async Task FetchSkillAsync_404_ThrowsNotFound()
    {
        var handler = new MockHttpHandler((req) => throw new HttpRequestException("404"));
        var http = new HttpClient(handler);
        var market = new SkillMarketService(http, "https://example.com/skills");

        await Assert.ThrowsAsync<SkillNotFoundException>(
            () => market.FetchSkillAsync("missing"));
    }

    [Fact]
    public async Task FetchSkillAsync_NetworkError_ThrowsMarketFetch()
    {
        var handler = new MockHttpHandler((req) => throw new HttpRequestException("连接超时"));
        var http = new HttpClient(handler);
        var market = new SkillMarketService(http, "https://example.com/skills");

        await Assert.ThrowsAsync<MarketFetchException>(
            () => market.FetchSkillAsync("any"));
    }

    [Fact]
    public async Task FetchIndexAsync_ParsesReadmeTable()
    {
        var md = """
            | id | name | description | icon | category | author | version |
            | --- | --- | --- | --- | --- | --- | --- |
            | a | A | d | 🅰 | t | x | 1.0.0 |
            """;
        var handler = new MockHttpHandler((req) =>
            req.RequestUri!.AbsolutePath.EndsWith("README.md") ? md : "{}");
        var http = new HttpClient(handler);
        var market = new SkillMarketService(http, "https://example.com/skills");

        var index = await market.FetchIndexAsync();
        Assert.Single(index.Skills);
        Assert.Equal("a", index.Skills[0].Id);
    }

    [Fact]
    public async Task CheckUpdatesAsync_DetectsNewerVersion()
    {
        var md = """
            | id | name | description | icon | category | author | version |
            | --- | --- | --- | --- | --- | --- | --- |
            | my-skill | 我的 | d | 🧩 | 测试 | x | 2.0.0 |
            """;
        var handler = new MockHttpHandler((req) =>
            req.RequestUri!.AbsolutePath.EndsWith("README.md") ? md : """{"Id":"my-skill","Name":"我的","Description":"d","Icon":"🧩","PromptTemplate":"p","Tools":[],"Category":"测试","Version":"2.0.0","Source":"market:x"}""");
        var http = new HttpClient(handler);
        var market = new SkillMarketService(http, "https://example.com/skills");

        var installed = new[] { TestSkills.Market("my-skill", "1.0.0") };
        var updates = await market.CheckUpdatesAsync(installed);

        Assert.Single(updates);
        Assert.True(updates["my-skill"].HasUpdate);
        Assert.Equal("1.0.0", updates["my-skill"].InstalledVersion);
        Assert.Equal("2.0.0", updates["my-skill"].LatestVersion);
    }
}

/// <summary>HttpClient 测试用 mock handler。</summary>
internal sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string> _responder;
    public MockHttpHandler(Func<HttpRequestMessage, string> responder) => _responder = responder;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var body = _responder(request);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
        }
        catch (HttpRequestException ex)
        {
            return Task.FromException<HttpResponseMessage>(ex);
        }
    }
}
