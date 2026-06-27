using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>v0.15 D3: SkillCenterWindow Market Tab XAML 文本验证。
/// 不实例化 WPF Window（xUnit 默认 MTA 线程会触发 STA 要求 + 拖死测试），
/// 改用 XAML 文本 + Regex 验证关键结构：
/// ① Market TabItem 存在 + Header 含「技能市场」
/// ② WrapPanel 卡片网格存在
/// ③ 搜索 TextBox x:Name=MarketSearchBox 绑定 MarketSearchText
/// ④ 源 Tab 横排 ItemsControl 绑定 MarketplaceSourceNames
/// ⑤ 分类 chips ItemsControl 绑定 MarketCategories。</summary>
public class SkillCenterMarketTabTests
{
    private static readonly string XamlPath =
        @"D:\opensource\DeskPilot\src\DeskPilot.App\Views\SkillCenterWindow.xaml";

    private static string LoadXaml()
    {
        Assert.True(File.Exists(XamlPath), $"XAML not found: {XamlPath}");
        return File.ReadAllText(XamlPath);
    }

    [Fact]
    public void Market_TabItem_Exists_With_Correct_Header()
    {
        var xaml = LoadXaml();
        // Market TabItem 第一个 Header 应该是「🌐  技能市场」
        var match = Regex.Match(xaml, @"<TabItem\s+Header=\""([^""]+)\""\s*>");
        Assert.True(match.Success, "Market TabItem not found");
        var header = match.Groups[1].Value;
        Assert.Contains("技能市场", header);
    }

    [Fact]
    public void Market_Tab_Contains_WrapPanel_Card_Grid_Binding_MarketSkillRows()
    {
        var xaml = LoadXaml();
        // 必须有 WrapPanel
        Assert.Contains("<WrapPanel Orientation=\"Horizontal\"", xaml);
        // 必须有 ItemsControl x:Name="MarketSkillGrid" 绑定 MarketSkillRows
        Assert.Contains("x:Name=\"MarketSkillGrid\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding MarketSkillRows}\"", xaml);
        // 卡片样式 MarketSkillCard 必须存在
        Assert.Contains("Style=\"{StaticResource MarketSkillCard}\"", xaml);
    }

    [Fact]
    public void Market_Tab_SearchBox_Binds_MarketSearchText()
    {
        var xaml = LoadXaml();
        Assert.Contains("x:Name=\"MarketSearchBox\"", xaml);
        Assert.Contains("Text=\"{Binding MarketSearchText", xaml);
    }

    [Fact]
    public void Market_Tab_SourceTabs_Bind_MarketplaceSourceNames_With_SourceTabButton_Style()
    {
        var xaml = LoadXaml();
        // 第一个 ItemsControl 应该绑定 MarketplaceSourceNames（源 Tab）
        Assert.Contains("ItemsSource=\"{Binding MarketplaceSourceNames}\"", xaml);
        // 源 Tab 横排用 StackPanel Orientation=Horizontal
        Assert.Matches(@"<ItemsControl[^>]*MarketplaceSourceNames[^>]*>[\s\S]*?<ItemsControl\.ItemsPanel>[\s\S]*?<StackPanel Orientation=\""Horizontal\""", xaml);
        // 源 Tab 用 SourceTabButton 样式（v0.15.1 hotfix 改 RadioButton.Style + BasedOn 模式支持 MultiBinding）
        Assert.Contains("BasedOn=\"{StaticResource SourceTabButton}\"", xaml);
    }

    [Fact]
    public void Market_Tab_CategoriesChips_Bind_MarketCategories_With_CategoryChipButton_Style()
    {
        var xaml = LoadXaml();
        Assert.Contains("ItemsSource=\"{Binding MarketCategories}\"", xaml);
        Assert.Contains("BasedOn=\"{StaticResource CategoryChipButton}\"", xaml);
        // 分类 chips 用 WrapPanel
        Assert.Matches(@"<ItemsControl[^>]*MarketCategories[^>]*>[\s\S]*?<ItemsControl\.ItemsPanel>[\s\S]*?<WrapPanel", xaml);
    }

    [Fact]
    public void Market_Tab_Has_Refresh_Button_Binding_LoadMarketCommand()
    {
        var xaml = LoadXaml();
        // 刷新按钮：Content 含「刷新市场」+ Command 绑定 LoadMarketCommand
        Assert.Contains("🔄 刷新市场", xaml);
        Assert.Contains("Command=\"{Binding LoadMarketCommand}\"", xaml);
    }

    [Fact]
    public void Market_Tab_Has_Three_Installed_Updates_TabItems()
    {
        var xaml = LoadXaml();
        // 必须有 3 个 TabItem：市场 + 已安装 + 有更新
        var tabItems = Regex.Matches(xaml, @"<TabItem\s+Header=\""([^""]+)\""").Cast<Match>().ToList();
        Assert.Equal(3, tabItems.Count);
        Assert.Contains(tabItems, m => m.Groups[1].Value.Contains("技能市场"));
        Assert.Contains(tabItems, m => m.Groups[1].Value.Contains("已安装"));
        Assert.Contains(tabItems, m => m.Groups[1].Value.Contains("有更新"));
    }
}