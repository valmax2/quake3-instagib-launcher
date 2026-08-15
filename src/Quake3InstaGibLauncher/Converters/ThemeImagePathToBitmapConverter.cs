using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Quake3InstaGibLauncher.Services;

namespace Quake3InstaGibLauncher.Converters;

/// <summary>Converte il percorso di un'immagine di tema (logo/sfondo/sfondo pulsanti) in un
/// BitmapImage per l'anteprima in Personalizza interfaccia. A differenza di
/// ImagePathToBitmapConverter (usato per i levelshot, che non cambiano mai durante l'esecuzione),
/// qui il file puo' essere sovrascritto ripetutamente con lo stesso nome (nuova immagine importata):
/// usa percio' ThemeService.LoadBitmapNoCache per bypassare la cache per-URI di WPF e mostrare
/// sempre l'anteprima aggiornata, non quella vecchia.</summary>
public sealed class ThemeImagePathToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try { return ThemeService.LoadBitmapNoCache(path); }
        catch { return null; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
