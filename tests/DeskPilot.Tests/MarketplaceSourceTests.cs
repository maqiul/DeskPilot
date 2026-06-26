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
    public async Task MarketplaceSourceService_StubMarkets_ReturnDemoIndex()
    {
        var svc = new MarketplaceSourceService(new SimpleHttpClientFactory());
        var idx = await svc.GetMarket("ClawHub").FetchIndexAsync();
        Assert.Single(idx.Skills);
        Assert.Equal("ClawHub", svc.GetMarket("ClawHub").SourceName);
        Assert.Contains("ClawHub", idx.Skills[0].Name);
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