// src/WebsiteWhitelistManual.App/Converters/NullToVisibilityConverter.cs
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WebsiteWhitelistManual.App.Converters;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = !string.IsNullOrEmpty(value as string);
        return Equals(parameter, "Bool") ? hasValue : (hasValue ? Visibility.Visible : Visibility.Collapsed);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
