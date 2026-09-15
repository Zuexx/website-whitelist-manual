// src/WebsiteWhitelistManual.App/Converters/BoolToProtectionTextConverter.cs
using System.Globalization;
using System.Windows.Data;

namespace WebsiteWhitelistManual.App.Converters;

public sealed class BoolToProtectionTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "已啟用白名單管制" : "尚未設定";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
