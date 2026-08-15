using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Converters;

/// <summary>PlayerRating -> etichetta in italiano, per ComboBox/badge.</summary>
public sealed class PlayerRatingLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        PlayerRating.Forte => "Forte — voglio sfidarlo",
        PlayerRating.DaEvitare => "Da evitare",
        PlayerRating.Amico => "Amico",
        _ => "Neutrale",
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>PlayerRating -> colore badge (usa le stesse risorse del tema, cosi' segue i colori
/// personalizzati dall'utente in Personalizza interfaccia invece di colori fissi).</summary>
public sealed class PlayerRatingBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resources = Application.Current.Resources;
        var key = value switch
        {
            PlayerRating.Forte => "Brush.AccentBright",
            PlayerRating.DaEvitare => "Brush.Danger",
            PlayerRating.Amico => "Brush.Success",
            _ => "Brush.TextDim",
        };
        return resources[key] as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
