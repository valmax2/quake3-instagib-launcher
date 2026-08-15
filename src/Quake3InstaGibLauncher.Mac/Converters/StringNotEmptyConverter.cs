using System.Globalization;
using Avalonia.Data.Converters;

namespace Quake3InstaGibLauncher.Mac.Converters;

/// <summary>True se il valore e' una stringa non nulla/non vuota, o un oggetto non nullo.</summary>
public sealed class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            null => false,
            string s => !string.IsNullOrEmpty(s),
            _ => true,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
