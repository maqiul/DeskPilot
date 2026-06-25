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
/// role 决定头像列顺序：user 头像在右（Column 1），assistant 头像在左（Column 0）。
/// </summary>
public sealed class RoleToAvatarColumnConverter : IValueConverter
{
    public static readonly RoleToAvatarColumnConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase))
            return 1; // 头像在气泡右侧
        return 0;
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
        bool visible = value is bool b && b;
        if (parameter is string p && p == "Invert") visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 非空字符串转 Visible，空字符串/Null 转 Collapsed。
/// ConverterParameter=Invert 时反转（用于空状态显示欢迎卡片）。
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public static readonly StringToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool visible = value is string s && !string.IsNullOrEmpty(s);
        if (parameter is string p && p == "Invert") visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 把 role 字符串转成头像 emoji：user → 👤，assistant → ✈。
/// </summary>
public sealed class RoleToAvatarConverter : IValueConverter
{
    public static readonly RoleToAvatarConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase))
            return "👤";
        return "✈";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// 把 role 字符串转成头像背景：user 浅橙，assistant 卡片色。
/// </summary>
public sealed class RoleToAvatarBrushConverter : IValueConverter
{
    public static readonly RoleToAvatarBrushConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var app = Application.Current;
        if (value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase))
            return app?.TryFindResource("UserBubbleBrush") ?? Brushes.Orange;
        return app?.TryFindResource("PrimaryBrush") ?? Brushes.Orange;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}