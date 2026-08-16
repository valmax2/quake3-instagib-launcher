using System.Globalization;
using Avalonia.Data.Converters;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Quest.Converters;

/// <summary>Mostra il nome leggibile italiano di GameType nei ComboBox (riusa GameTypeExtensions
/// del Core, stessa etichetta gia' usata su Windows/Mac). Gestisce anche GameType? per il filtro
/// "Tutte le modalita'" (null).</summary>
public sealed class GameTypeDisplayConverter : IValueConverter
{
    public static readonly GameTypeDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            GameType gt => gt.ToDisplayName(),
            null => "Tutte le modalita'",
            _ => value.ToString(),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
