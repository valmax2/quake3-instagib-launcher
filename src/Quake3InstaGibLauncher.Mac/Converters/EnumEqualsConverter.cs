using System.Globalization;
using Avalonia.Data.Converters;

namespace Quake3InstaGibLauncher.Mac.Converters;

/// <summary>Confronta un valore enum con ConverterParameter (stringa col nome del valore atteso).
/// Usato sia per IsVisible (schermate) sia per IsChecked di RadioButton legati a un enum.</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool b || !b || parameter is null || targetType is null) return null;
        return Enum.Parse(targetType, parameter.ToString()!);
    }
}
