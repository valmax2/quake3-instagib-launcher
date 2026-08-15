using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace Quake3InstaGibLauncher.Mac.Converters;

/// <summary>Converte un percorso file (stringa) in un'immagine Avalonia per le anteprime della
/// galleria in Personalizza interfaccia. Nessuna cache per-percorso (a differenza di WPF): ogni
/// conversione rilegge il file da disco, quindi un'immagine sostituita con lo stesso nome si
/// aggiorna sempre correttamente. GIF: viene decodificato solo il primo fotogramma (Avalonia non
/// anima i GIF nativamente).</summary>
public sealed class PathToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
