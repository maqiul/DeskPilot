using DeskPilot.App.ViewModels;
using System.Windows;

namespace DeskPilot.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}