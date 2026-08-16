using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Quake3InstaGibLauncher.Quest.Converters;

/// <summary>True -> bordo spesso (evidenzia la card mappa selezionata), False -> nessun bordo.</summary>
public sealed class BoolToBorderThicknessConverter : IValueConverter
{
    public static readonly BoolToBorderThicknessConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? new Thickness(3) : new Thickness(0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
