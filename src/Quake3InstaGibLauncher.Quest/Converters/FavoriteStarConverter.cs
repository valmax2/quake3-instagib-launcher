using System.Globalization;
using Avalonia.Data.Converters;

namespace Quake3InstaGibLauncher.Quest.Converters;

/// <summary>True -> stella piena, False -> stella vuota, per il pulsante preferiti sulle card mappa.</summary>
public sealed class FavoriteStarConverter : IValueConverter
{
    public static readonly FavoriteStarConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "★" : "☆";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
