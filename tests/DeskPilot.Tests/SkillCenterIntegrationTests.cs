using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DeskPilot.Tests;

/// <summary>v0.15 D4: ChatWindow + SkillCenterWindow 集成测试（XAML 文本验证，避免 STA 线程问题）。
/// ① ChatWindow.xaml 顶部 Menu 存在且含「技能」菜单
/// ② ChatWindow.xaml Ctrl+Shift+K InputBinding 存在
/// ③ ChatWindow.xaml 技能菜单有「打开技能中心」子项 + InputGestureText="Ctrl+Shift+K"
/// ④ SkillCenterWindow.xaml 含 3 个 TabItem：技能市场 + 已安装 + 有更新
/// ⑤ Installed Tab 绑定 InstalledSkills（ListView ItemsSource）
/// ⑥ Updates Tab 绑定 UpdateAvailableSkills（ListView ItemsSource）</summary>
public class SkillCenterIntegrationTests
{
    private const string ChatWindowXaml = @"D:\opensource\DeskPilot\src\DeskPilot.App\Views\ChatWindow.xaml";
    private const string SkillCenterXaml = @"D:\opensource\DeskPilot\src\DeskPilot.App\Views\SkillCenterWindow.xaml";

    private static string LoadChatWindow() => File.ReadAllText(ChatWindowXaml);
    private static string LoadSkillCenter() => File.ReadAllText(SkillCenterXaml);

    [Fact]
    public void ChatWindow_HasTopMenu_With_Skills_MenuItem()
    {
        var xaml = LoadChatWindow();
        // 顶部 Menu 必须存在
        Assert.Contains("<Menu Grid.Row=\"0\"", xaml);
        // 技能菜单必须存在
        Assert.Matches(@"<MenuItem\s+Header=\""技能\(_K\)\"">", xaml);
        // 子项「打开技能中心」必须存在
        Assert.Matches(@"<MenuItem\s+Header=\""打开技能中心", xaml);
    }

    [Fact]
    public void ChatWindow_Has_CtrlShiftK_InputBinding_For_SkillCenter()
    {
        var xaml = LoadChatWindow();
        // Window.InputBindings 必须存在
        Assert.Contains("<Window.InputBindings>", xaml);
        // Ctrl+Shift+K 快捷键 + 绑定 ShowSkillCenterCommand
        Assert.Matches(@"<KeyBinding\s+Key=\""K\""\s+Modifiers=\""Ctrl\+Shift\""\s+Command=\""\{Binding ShowSkillCenterCommand\}\""\s*/>", xaml);
    }

    [Fact]
    public void ChatWindow_SkillsMenu_Has_OpenSkillCenter_Item_With_InputGestureText()
    {
        var xaml = LoadChatWindow();
        // 「打开技能中心」子项必须含 InputGestureText="Ctrl+Shift+K"
        Assert.Matches(@"<MenuItem\s+Header=\""打开技能中心\(_C\)\.\.\.\""\s+Command=\""\{Binding ShowSkillCenterCommand\}\""\s+InputGestureText=\""Ctrl\+Shift\+K\""", xaml);
    }

    [Fact]
    public void SkillCenter_Has_Three_TabItems_Market_Installed_Updates()
    {
        var xaml = LoadSkillCenter();
        var tabs = Regex.Matches(xaml, @"<TabItem\s+Header=\""([^""]+)\""").Cast<Match>().ToList();
        Assert.Equal(3, tabs.Count);
        Assert.Contains(tabs, t => t.Groups[1].Value.Contains("技能市场"));
        Assert.Contains(tabs, t => t.Groups[1].Value.Contains("已安装"));
        Assert.Contains(tabs, t => t.Groups[1].Value.Contains("有更新"));
    }

    [Fact]
    public void SkillCenter_InstalledTab_ListView_Binds_InstalledSkills()
    {
        var xaml = LoadSkillCenter();
        // Installed Tab 内必须含 ListView + 绑定 InstalledSkills + 卸载按钮 CommandParameter={Binding Id}
        Assert.Contains("ItemsSource=\"{Binding InstalledSkills}\"", xaml);
        Assert.Contains("Command=\"{Binding DataContext.UninstallCommand", xaml);
        Assert.Contains("CommandParameter=\"{Binding Id}\"", xaml);
    }

    [Fact]
    public void SkillCenter_UpdatesTab_ListView_Binds_UpdateAvailableSkills()
    {
        var xaml = LoadSkillCenter();
        // Updates Tab 内必须含 ListView + 绑定 UpdateAvailableSkills + 一键更新按钮绑定 UpdateSkillCommand
        Assert.Contains("ItemsSource=\"{Binding UpdateAvailableSkills}\"", xaml);
        Assert.Contains("Command=\"{Binding DataContext.UpdateSkillCommand", xaml);
    }
}