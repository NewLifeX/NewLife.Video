using Avalonia.Data.Converters;
using System.Globalization;

namespace NewLife.Video.Studio.Helpers;

/// <summary>布尔值取反转换器</summary>
public class BoolNegationConverter : IValueConverter
{
    public Object? Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
        => value is Boolean b ? !b : value;

    public Object? ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture)
        => value is Boolean b ? !b : value;
}

/// <summary>布尔值转换为字符串（可自定义 True/False 显示文本）</summary>
public class BoolToStringConverter : IValueConverter
{
    public String? TrueText { get; set; }
    public String? FalseText { get; set; }

    public Object? Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
        => value is Boolean b && b ? TrueText : FalseText;

    public Object? ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
