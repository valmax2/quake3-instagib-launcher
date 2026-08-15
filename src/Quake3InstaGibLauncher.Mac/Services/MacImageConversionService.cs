using Pfim;
using SkiaSharp;

namespace Quake3InstaGibLauncher.Mac.Services;

/// <summary>
/// Decodifica le anteprime (levelshot) estratte dai .pk3, equivalente macOS/Avalonia di
/// LevelshotImageService (Windows/WPF). Usa SkiaSharp (gia' dipendenza di Avalonia) per
/// JPG/PNG e per la ri-codifica in PNG; usa Pfim (decoder puro C#, stesso usato su Windows)
/// per i file TGA. I .pk3 non vengono mai modificati: si legge solo il singolo entry in memoria.
/// </summary>
public static class MacImageConversionService
{
    /// <summary>Firma compatibile con il delegate richiesto da Core.Services.MapCacheService.</summary>
    public static byte[] ConvertToPngBytes(byte[] rawEntryBytes, string virtualEntryPath)
    {
        var ext = Path.GetExtension(virtualEntryPath).ToLowerInvariant();

        if (ext is ".jpg" or ".jpeg" or ".png")
        {
            using var bitmap = SKBitmap.Decode(rawEntryBytes);
            if (bitmap is null)
                throw new InvalidOperationException($"Impossibile decodificare l'immagine: {virtualEntryPath}");

            return EncodeAsPng(bitmap);
        }

        if (ext == ".tga")
        {
            using var input = new MemoryStream(rawEntryBytes);
            using var image = Targa.Create(input, new PfimConfig());
            using var bitmap = ToSkBitmap(image);
            return EncodeAsPng(bitmap);
        }

        throw new NotSupportedException($"Formato immagine non supportato: {virtualEntryPath}");
    }

    private static SKBitmap ToSkBitmap(IImage image)
    {
        var colorType = image.Format switch
        {
            Pfim.ImageFormat.Rgba32 => SKColorType.Bgra8888,
            Pfim.ImageFormat.Rgb24 => SKColorType.Bgra8888, // convertito sotto: Skia non ha un 24bpp diretto comodo
            _ => SKColorType.Bgra8888,
        };

        // Se l'immagine sorgente non e' gia' a 32bpp, la normalizziamo copiando pixel per pixel:
        // i TGA dei levelshot Quake3 sono quasi sempre 24 o 32 bpp, quindi il costo e' trascurabile
        // per immagini di anteprima (poche centinaia di KB).
        byte[] pixelData;
        if (image.Format == Pfim.ImageFormat.Rgb24)
        {
            pixelData = new byte[image.Width * image.Height * 4];
            var src = image.Data;
            var srcStride = image.Stride;
            for (var y = 0; y < image.Height; y++)
            {
                var srcRow = y * srcStride;
                var dstRow = y * image.Width * 4;
                for (var x = 0; x < image.Width; x++)
                {
                    var s = srcRow + x * 3;
                    var d = dstRow + x * 4;
                    pixelData[d + 0] = src[s + 0]; // B
                    pixelData[d + 1] = src[s + 1]; // G
                    pixelData[d + 2] = src[s + 2]; // R
                    pixelData[d + 3] = 255;        // A
                }
            }
        }
        else
        {
            pixelData = image.Data;
        }

        var info = new SKImageInfo(image.Width, image.Height, colorType, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);

        unsafe
        {
            fixed (byte* ptr = pixelData)
            {
                bitmap.InstallPixels(info, (IntPtr)ptr, image.Width * 4);
            }
        }

        // InstallPixels non copia il buffer: ne facciamo una copia gestita indipendente prima
        // che pixelData esca di scope, cosi' il bitmap resta valido.
        return bitmap.Copy();
    }

    private static byte[] EncodeAsPng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, quality: 100);
        return data.ToArray();
    }
}
