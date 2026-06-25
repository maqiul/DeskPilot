using System;
using System.Globalization;
using System.Windows.Data;

namespace DeskPilot.App;

/// <summary>
/// 把枚举值转成 bool：值匹配时返回 true（用于 RadioButton IsChecked）。
/// ConverterParameter 指定要比较的枚举名（如 "Light" / "Dark" / "System"）。
/// </summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public static readonly EnumToBoolConverter LightInstance = new() { TargetValue = "Light" };
    public static readonly EnumToBoolConverter DarkInstance = new() { TargetValue = "Dark" };
    public static readonly EnumToBoolConverter SystemInstance = new() { TargetValue = "System" };

    public string TargetValue { get; set; } = string.Empty;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Enum e)
            return e.ToString() == TargetValue;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is string s)
            return Enum.Parse(targetType, s);
        return Binding.DoNothing;
    }
}
