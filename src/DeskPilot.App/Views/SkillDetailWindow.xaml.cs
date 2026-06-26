using System.Windows;
using DeskPilot.App.ViewModels;

namespace DeskPilot.App.Views;

public partial class SkillDetailWindow : Window
{
    public SkillDetailWindow(SkillDetailViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}