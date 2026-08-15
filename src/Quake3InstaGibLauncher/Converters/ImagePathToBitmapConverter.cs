using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Quake3InstaGibLauncher.Services;

namespace Quake3InstaGibLauncher.Converters;

/// <summary>Converte il percorso di un'anteprima in cache (PNG) in un BitmapImage per il binding XAML.
/// Se il percorso e' nullo o il file non esiste, restituisce null: la UI mostra allora il segnaposto.</summary>
public sealed class ImagePathToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            return LevelshotImageService.LoadFromCacheFile(path);
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
