using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pfim;

namespace Quake3InstaGibLauncher.Services;

/// <summary>
/// Decodifica le anteprime (levelshot) estratte dai .pk3. Supporta JPG e PNG in modo nativo
/// tramite WPF; per i file TGA (usati da molte mappe di baseq3 e dalla quasi totalita' del
/// missionpack) usa la libreria Pfim, un decoder puro C# senza dipendenze native.
/// I file .pk3 non vengono mai modificati: si legge solo il singolo entry in memoria.
/// </summary>
public static class LevelshotImageService
{
    public static bool IsSupportedImageEntry(string virtualEntryPath)
    {
        var ext = Path.GetExtension(virtualEntryPath).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".tga";
    }

    /// <summary>
    /// Converte i byte grezzi di un entry levelshot (letti da dentro il .pk3) in un PNG
    /// pronto per essere salvato su disco nella cache dell'app.
    /// </summary>
    public static byte[] ConvertToPngBytes(byte[] rawEntryBytes, string virtualEntryPath)
    {
        var ext = Path.GetExtension(virtualEntryPath).ToLowerInvariant();

        if (ext is ".jpg" or ".jpeg" or ".png")
        {
            // Gia' in un formato leggibile da WPF: ri-codifichiamo comunque in PNG per uniformare
            // la cache a un solo formato semplice da servire e da ripulire.
            using var input = new MemoryStream(rawEntryBytes);
            var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return EncodeFrameAsPng(decoder.Frames[0]);
        }

        if (ext == ".tga")
        {
            using var input = new MemoryStream(rawEntryBytes);
            using var image = Targa.Create(input, new PfimConfig());
            var bitmapSource = ToBitmapSource(image);
            return EncodeFrameAsPng(bitmapSource);
        }

        throw new NotSupportedException($"Formato immagine non supportato: {virtualEntryPath}");
    }

    private static BitmapSource ToBitmapSource(IImage image)
    {
        PixelFormat pixelFormat = image.Format switch
        {
            Pfim.ImageFormat.Rgba32 => PixelFormats.Bgra32,
            Pfim.ImageFormat.Rgb24 => PixelFormats.Bgr24,
            Pfim.ImageFormat.Rgb8 => PixelFormats.Gray8,
            Pfim.ImageFormat.R5g6b5 => PixelFormats.Bgr565,
            Pfim.ImageFormat.Rgba16 => PixelFormats.Bgra32, // convertito sotto se necessario
            _ => PixelFormats.Bgra32,
        };

        var data = image.Data;
        var stride = image.Stride;

        // Pfim decodifica i TGA con canali in ordine compatibile con le PixelFormats "Bgr*" di WPF
        // (vedi documentazione ufficiale di Pfim, esempio di consumo in WPF).
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, pixelFormat, null, data, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] EncodeFrameAsPng(BitmapSource frame)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    /// <summary>Carica un'immagine PNG dalla cache su disco come BitmapImage pronta per il binding XAML.</summary>
    public static BitmapImage LoadFromCacheFile(string pngFilePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(pngFilePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
