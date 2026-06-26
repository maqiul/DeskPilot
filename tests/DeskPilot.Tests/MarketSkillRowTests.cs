using System.Collections.Generic;
using DeskPilot.App.ViewModels;
using DeskPilot.Core.Models;
using DeskPilot.Core.Services;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>v0.11: MarketSkillRow.FromManifest 字段映射测试。</summary>
public class MarketSkillRowTests
{
    [Fact]
    public void FromManifest_BasicFields_PassThrough()
    {
        var m = new SkillManifest(
            Id: "demo", Name: "Demo", Description: "示例",
            Icon: "🧪", Category: "测试", Author: "maqiul",
            Version: "1.2.3", Tags: new[] { "demo" });
        var row = MarketSkillRow.FromManifest(m, null);
        Assert.Equal("demo", row.Id);
        Assert.Equal("Demo", row.Name);
        Assert.Equal("示例", row.Description);
        Assert.Equal("🧪", row.Icon);
        Assert.Equal("测试", row.Category);
        Assert.Equal("maqiul", row.Author);
        Assert.Equal("1.2.3", row.Version);
        Assert.False(row.IsInstalled);
        Assert.False(row.HasUpdate);
        Assert.Equal("", row.InstalledVersion);
        Assert.Equal("1.2.3", row.LatestVersion);
    }

    [Fact]
    public void FromManifest_V11Fields_RatingDownloadsScreenshotUrl()
    {
        var m = new SkillManifest(
            Id: "demo", Name: "Demo", Description: "示例",
            Icon: "🧪", Category: "测试", Author: "QwenPaw",
            Version: "1.0.0", Tags: new[] { "demo" },
            ScreenshotUrl: "https://example.com/s.png",
            Rating: 4.7, Downloads: 999,
            AuthorUrl: "https://example.com/author",
            AuthorName: "QwenPaw Studio");
        var row = MarketSkillRow.FromManifest(m, null);
        Assert.Equal("https://example.com/s.png", row.ScreenshotUrl);
        Assert.Equal(4.7, row.Rating);
        Assert.Equal(999, row.Downloads);
        Assert.Equal("QwenPaw Studio", row.AuthorName);
        Assert.Equal("https://example.com/author", row.AuthorUrl);
    }

    [Fact]
    public void FromManifest_SourceName_DefaultsToAuthorForCardBadge()
    {
        var m = new SkillManifest(
            Id: "demo", Name: "D", Description: "x",
            Icon: "📦", Category: "c", Author: "QwenPaw",
            Version: "1.0.0", Tags: new[] { "t" });
        var row = MarketSkillRow.FromManifest(m, null);
        // SourceName 兜底用 Author 作为卡片右上角徽章
        Assert.Equal("QwenPaw", row.SourceName);
    }

    [Fact]
    public void FromManifest_AuthorNameFallback_WhenEmpty()
    {
        var m = new SkillManifest(
            Id: "demo", Name: "D", Description: "x",
            Icon: "📦", Category: "c", Author: "maqiul",
            Version: "1.0.0", Tags: new[] { "t" });
        var row = MarketSkillRow.FromManifest(m, null);
        // AuthorName 为空时显示 Author
        Assert.Equal("maqiul", row.AuthorName);
    }

    [Fact]
    public void FromManifest_EmptyIcon_FallsBackToDefault()
    {
        var m = new SkillManifest(
            Id: "demo", Name: "D", Description: "x",
            Icon: "", Category: "c", Author: "x",
            Version: "1.0.0", Tags: new[] { "t" });
        var row = MarketSkillRow.FromManifest(m, null);
        Assert.Equal("🧩", row.Icon);
    }
}