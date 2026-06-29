using System.Windows;
using System.Windows.Input;
using DeskPilot.App.ViewModels;
using DeskPilot.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeskPilot.App.Views;

/// <summary>v0.15: 技能中心窗口（独立 Window，Melon 风格）。承载市场 / 已安装 / 更新 3 个 Tab。
/// v0.16 C: Market Tab 卡片点击 → 弹出 SkillDetailWindow 查看详情。</summary>
public partial class SkillCenterWindow : Window
{
    public SkillCenterWindow()
    {
        InitializeComponent();
    }

    /// <summary>v0.16 C: Market Tab 卡片点击 → 弹出 SkillDetailWindow 查看详情。</summary>
    private void MarketSkillCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string skillId)
            return;
        if (DataContext is not SkillCenterViewModel vm)
            return;

        var row = vm.MarketSkillRows.FirstOrDefault(r => r.Id == skillId);
        if (row == null)
            return;

        // 通过 App.Services 获取 ISkillService + ISkillMarket（已在 App.xaml.cs DI 容器中注册）
        var skillService = App.Services.GetService<ISkillService>();
        var skillMarket = App.Services.GetService<ISkillMarket>();

        var detailVm = new SkillDetailViewModel(row, skillService, skillMarket);
        var detailWindow = new SkillDetailWindow(detailVm) { Owner = this };
        detailWindow.ShowDialog();
    }
}