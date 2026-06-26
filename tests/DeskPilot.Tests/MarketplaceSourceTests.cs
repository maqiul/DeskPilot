using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DeskPilot.Core.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>
/// v0.11: 多市场源（QwenPaw/ClawHub/ModelScope）+ SourceName 字段 + Markdown 10 列解析测试。
/// </summary>
public class MarketplaceSourceTests
{
    // === ISkillMarket.SourceName ===

    [Fact]
    public void SkillMarketService_SourceName_DefaultsToQwenPaw()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var svc = new SkillMarketService(http);
        Assert.Equal("QwenPaw", svc.SourceName);
        Assert.StartsWith("https://raw.githubusercontent.com/maqiul/DeskPilot", svc.BaseUrl);
    }

    [Fact]
    public void SkillMarketService_SourceName_CustomOverridable()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var svc = new SkillMarketService(http, "https://example.com/skills", "CustomHub");
        Assert.Equal("CustomHub", svc.SourceName);
        Assert.Equal("https://example.com/skills", svc.BaseUrl);
    }

    // === IMarketplaceSourceService ===

    [Fact]
    public void MarketplaceSourceService_ProvidesThreeSources()
    {
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        Assert.Equal(3, svc.SourceNames.Count);
        Assert.Contains("QwenPaw", svc.SourceNames);
        Assert.Contains("ClawHub", svc.SourceNames);
        Assert.Contains("ModelScope", svc.SourceNames);
    }

    [Fact]
    public void MarketplaceSourceService_GetMarket_ReturnsCorrectOne()
    {
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        Assert.Equal("QwenPaw", svc.GetMarket("QwenPaw").SourceName);
        Assert.Equal("ClawHub", svc.GetMarket("ClawHub").SourceName);
        Assert.Equal("ModelScope", svc.GetMarket("ModelScope").SourceName);
        Assert.Equal("QwenPaw", svc.DefaultMarket.SourceName);
    }

    [Fact]
    public async Task MarketplaceSourceService_ClawHubMarketService_HasIndependentBaseUrl()
    {
        // v0.12 A1.1: ClawHubMarketService 真后端有独立 BaseUrl（指向 maqiul 名下 DeskPilot-clawhub 公开仓库）
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        var clawhub = svc.GetMarket("ClawHub");
        Assert.IsType<ClawHubMarketService>(clawhub);
        Assert.Equal("ClawHub", clawhub.SourceName);
        Assert.Equal(ClawHubMarketService.DefaultBaseUrl, clawhub.BaseUrl);
        Assert.DoesNotContain("/DeskPilot/main/skills", clawhub.BaseUrl);
    }

    [Fact]
    public async Task MarketplaceSourceService_ModelScopeMarketService_HasIndependentBaseUrl()
    {
        // v0.12 A1.1: ModelScopeMarketService 真后端有独立 BaseUrl（指向 maqiul 名下 DeskPilot-modelscope 公开仓库）
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        var modelscope = svc.GetMarket("ModelScope");
        Assert.IsType<ModelScopeMarketService>(modelscope);
        Assert.Equal("ModelScope", modelscope.SourceName);
        Assert.Equal(ModelScopeMarketService.DefaultBaseUrl, modelscope.BaseUrl);
        Assert.DoesNotContain("/DeskPilot/main/skills", modelscope.BaseUrl);
    }

    [Fact]
    public void MarketplaceSourceService_ThreeSources_HaveDifferentBaseUrls()
    {
        // v0.12 A1.1: 3 个真源 BaseUrl 互不相同（隔离 / 独立更新）
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        var urls = svc.SourceNames.Select(n => svc.GetMarket(n).BaseUrl).ToList();
        Assert.Equal(3, urls.Distinct().Count());
        Assert.Equal(3, urls.Count);
    }

    [Fact]
    public async Task MarketplaceSourceService_RealSource_FetchIndex_ThrowsOn404()
    {
        // v0.12 A1.1: 真源没仓库时（404）应抛 MarketFetchException（不像 Stub 永远返回数据）
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        // 仓库 maqiul/DeskPilot-clawhub 还没创建，会 404
        await Assert.ThrowsAsync<MarketFetchException>(async () =>
            await svc.GetMarket("ClawHub").FetchIndexAsync());
    }

    [Fact]
    public void ClawHubMarketService_CustomBaseUrl_OverridesDefault()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var svc = new ClawHubMarketService(http, "https://my-proxy.example.com/skills");
        Assert.Equal("https://my-proxy.example.com/skills", svc.BaseUrl);
        Assert.Equal("ClawHub", svc.SourceName);
    }

    [Fact]
    public void ModelScopeMarketService_CustomBaseUrl_OverridesDefault()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var svc = new ModelScopeMarketService(http, "https://my-proxy.example.com/skills");
        Assert.Equal("https://my-proxy.example.com/skills", svc.BaseUrl);
        Assert.Equal("ModelScope", svc.SourceName);
    }

    // === v0.12 A1.2: 用户动态添加自定义市场源 ===

    [Fact]
    public void AddCustomSource_NewName_AppendsToSourceNames()
    {
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        var before = svc.SourceNames.Count;
        var added = svc.AddCustomSource("MyHub", "https://my-hub.example.com/skills");
        Assert.True(added);
        Assert.Equal(before + 1, svc.SourceNames.Count);
        Assert.Contains("MyHub", svc.SourceNames);
        Assert.Equal("MyHub", svc.GetMarket("MyHub").SourceName);
        Assert.Equal("https://my-hub.example.com/skills", svc.GetMarket("MyHub").BaseUrl);
    }

    [Fact]
    public void AddCustomSource_DuplicateName_ReturnsFalse()
    {
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        Assert.False(svc.AddCustomSource("QwenPaw", "https://other.example.com/skills"));
        // QwenPaw 仍指向自家 GitHub（不被覆盖）
        Assert.Contains("DeskPilot/main/skills", svc.GetMarket("QwenPaw").BaseUrl);
    }

    [Fact]
    public void AddCustomSource_EmptyArgs_ReturnsFalse()
    {
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        Assert.False(svc.AddCustomSource("", "https://x.com"));
        Assert.False(svc.AddCustomSource("X", ""));
        Assert.False(svc.AddCustomSource("  ", "https://x.com"));
    }

    [Fact]
    public void AddCustomSource_TrimsTrailingSlash()
    {
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        svc.AddCustomSource("Test", "https://test.example.com/skills/");
        Assert.Equal("https://test.example.com/skills", svc.GetMarket("Test").BaseUrl);
    }

    [Fact]
    public void AddCustomSource_FiresPropertyChanged()
    {
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        var fired = new List<string?>();
        svc.PropertyChanged += (_, e) => fired.Add(e.PropertyName);
        svc.AddCustomSource("Notify", "https://n.example.com/skills");
        Assert.Contains(nameof(IMarketplaceSourceService.SourceNames), fired);
    }

    [Fact]
    public async Task MarketplaceSourceService_GetUnknown_Throws()
    {
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        await Assert.ThrowsAsync<MarketFetchException>(async () =>
            await svc.GetMarket("NonExistent").FetchIndexAsync());
    }

    // === Markdown 10 列解析（v0.11 新字段） ===

    [Fact]
    public void ParseIndexFromMarkdown_10Columns_ReadsScreenshotRatingDownloads()
    {
        var md = """
            | id | name | description | icon | category | author | version | screenshotUrl | rating | downloads |
            |---|---|---|---|---|---|---|---|---|---|
            | demo-skill | 示例技能 | 这是一个示例 | 🧪 | 测试 | maqiul | 1.2.0 | https://example.com/s.png | 4.7 | 999 |
            """;
        var idx = SkillMarketService.ParseIndexFromMarkdown(md);
        Assert.Single(idx.Skills);
        var s = idx.Skills[0];
        Assert.Equal("demo-skill", s.Id);
        Assert.Equal("https://example.com/s.png", s.ScreenshotUrl);
        Assert.Equal(4.7, s.Rating);
        Assert.Equal(999, s.Downloads);
    }

    [Fact]
    public void ParseIndexFromMarkdown_7Columns_DefaultsNewFieldsToEmpty()
    {
        var md = """
            | id | name | description | icon | category | author | version |
            |---|---|---|---|---|---|---|
            | old-skill | 老技能 | 兼容老索引 | 📦 | 文件 | maqiul | 1.0.0 |
            """;
        var idx = SkillMarketService.ParseIndexFromMarkdown(md);
        var s = idx.Skills[0];
        Assert.Equal("", s.ScreenshotUrl);
        Assert.Equal(0, s.Rating);
        Assert.Equal(0, s.Downloads);
    }

    // === Stub helpers ===

    private sealed class SimpleHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }
}