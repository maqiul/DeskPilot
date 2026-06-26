using DeskPilot.App.ViewModels;
using DeskPilot.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DeskPilot.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>v0.11: 点击市场卡片 → 弹详情窗（看完整 Prompt + Tools + 安装/卸载）。</summary>
    private void MarketSkillCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: MarketSkillRow row }) return;
        if (DataContext is not SettingsViewModel vm) return;

        var skillService = App.Services.GetService<ISkillService>();
        var skillMarket = vm.CurrentMarket;
        var detailVm = new SkillDetailViewModel(row, skillService, skillMarket);
        var detailWindow = new SkillDetailWindow(detailVm)
        {
            Owner = this
        };
        detailWindow.ShowDialog();

        // 关闭弹窗后刷新卡片状态（IsInstalled/HasUpdate 可能变了）
        row.IsInstalled = skillService?.FindById(row.Id) != null;
    }
}