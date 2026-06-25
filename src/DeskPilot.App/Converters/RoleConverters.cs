using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DeskPilot.App;

/// <summary>
/// 将 role 字符串转为气泡对齐方向：user 右对齐，assistant 左对齐。
/// </summary>
public sealed class RoleToAlignmentConverter : IValueConverter
{
    public static readonly RoleToAlignmentConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase))
            return HorizontalAlignment.Right;
        return HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 将 role 字符串转为气泡背景色。
/// </summary>
public sealed class RoleToBubbleBrushConverter : IValueConverter
{
    public static readonly RoleToBubbleBrushConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var app = Application.Current;
        if (value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase))
            return app?.TryFindResource("UserBubbleBrush") ?? Brushes.Orange;
        return app?.TryFindResource("AssistantBubbleBrush") ?? Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 将 role 字符串转为标签文案。
/// </summary>
public sealed class RoleToLabelConverter : IValueConverter
{
    public static readonly RoleToLabelConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string role)
        {
            if (role.Equals("user", StringComparison.OrdinalIgnoreCase)) return "你";
            if (role.Equals("assistant", StringComparison.OrdinalIgnoreCase)) return "DeskPilot";
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// bool 转 Visibility。
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b) return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}