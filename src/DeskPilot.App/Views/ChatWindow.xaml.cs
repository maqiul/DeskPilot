using DeskPilot.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskPilot.App.Views;

public partial class ChatWindow : Window
{
    private readonly ChatViewModel _viewModel;

    public ChatWindow(ChatViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Messages.CollectionChanged += (_, _) => ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            if (MessagesScrollViewer.Content is ItemsControl items)
            {
                if (VisualTreeHelper.GetChild(items, 0) is Decorator border)
                {
                    if (border.Child is ScrollViewer inner)
                    {
                        inner.ScrollToEnd();
                        return;
                    }
                }
            }
            MessagesScrollViewer.ScrollToEnd();
        }));
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            if (DataContext is ChatViewModel vm && vm.SendCommand.CanExecute(null))
            {
                vm.SendCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = App.Services.GetRequiredService<SettingsWindow>();
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }

    private void Suggestion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string text })
        {
            _viewModel.UserInput = text;
            if (_viewModel.SendCommand.CanExecute(null))
                _viewModel.SendCommand.Execute(null);
        }
    }
}