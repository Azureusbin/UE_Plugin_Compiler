using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UEPluginCompiler.Converters;

/// <summary>
/// Converts true → Visible, false → Collapsed.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is true) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is Visibility v) && v == Visibility.Visible;
}

/// <summary>
/// Converts true → Collapsed, false → Visible. (Logical NOT + visibility)
/// </summary>
public class InvertBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is true) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is Visibility v) && v != Visibility.Visible;
}

/// <summary>
/// Converts true → false, false → true. Simple logical NOT for bool properties.
/// </summary>
public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}
