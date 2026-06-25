using System;
using System.Windows;

namespace DeskPilot.App.Services;

/// <summary>
/// 主题模式。
/// </summary>
public enum AppTheme
{
    Light,
    Dark,
    System
}

/// <summary>
/// 主题切换：合并/移除对应 ResourceDictionary 到 Application.Resources。
/// </summary>
public static class ThemeManager
{
    private const string LightSource = "Styles/Colors.xaml";
    private const string DarkSource = "Styles/DarkColors.xaml";

    private static ResourceDictionary? _currentThemeDict;

    public static void ApplyTheme(AppTheme theme)
    {
        var actual = theme == AppTheme.System
            ? (IsSystemDark() ? AppTheme.Dark : AppTheme.Light)
            : theme;

        var source = actual == AppTheme.Dark ? DarkSource : LightSource;

        // 移除旧主题字典
        if (_currentThemeDict != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(_currentThemeDict);
            _currentThemeDict = null;
        }

        // 加载新主题
        var newDict = new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Relative)
        };
        Application.Current.Resources.MergedDictionaries.Add(newDict);
        _currentThemeDict = newDict;
    }

    private static bool IsSystemDark()
    {
        try
        {
            // 读取 Windows 注册表判断系统主题
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }
}
