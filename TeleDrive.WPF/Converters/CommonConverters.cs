using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TeleDrive.Core.Models;

namespace TeleDrive.WPF.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is bool b && b;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility visibility && visibility == Visibility.Visible;
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class ByteSizeConverter : IValueConverter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long and not int)
        {
            return string.Empty;
        }

        var bytes = System.Convert.ToDouble(value);
        var unitIndex = 0;

        while (bytes >= 1024 && unitIndex < Units.Length - 1)
        {
            bytes /= 1024;
            unitIndex++;
        }

        return $"{bytes:0.#} {Units[unitIndex]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class TransferStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            TransferStatus.Completed => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),
            TransferStatus.Failed => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
            TransferStatus.Cancelled => new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)),
            TransferStatus.Paused => new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08)),
            TransferStatus.InProgress => new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
            _ => new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
