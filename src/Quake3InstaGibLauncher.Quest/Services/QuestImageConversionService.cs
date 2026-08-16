using Pfim;
using SkiaSharp;

namespace Quake3InstaGibLauncher.Quest.Services;

/// <summary>
/// Decodifica le anteprime (levelshot) estratte dai .pk3 per la galleria mappe della scheda Bot.
/// Identica a MacImageConversionService (stessa piattaforma Avalonia/SkiaSharp): JPG/PNG via
/// SkiaSharp, TGA via Pfim + conversione manuale in SKBitmap. Duplicata invece di condivisa dal
/// Core per non introdurre una dipendenza SkiaSharp/Pfim nel Core (che oggi ne e' volutamente
/// privo per restare un puro class library platform-agnostic).
/// </summary>
public static class QuestImageConversionService
{
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
                    pixelData[d + 0] = src[s + 0];
                    pixelData[d + 1] = src[s + 1];
                    pixelData[d + 2] = src[s + 2];
                    pixelData[d + 3] = 255;
                }
            }
        }
        else
        {
            pixelData = image.Data;
        }

        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);

        unsafe
        {
            fixed (byte* ptr = pixelData)
            {
                bitmap.InstallPixels(info, (IntPtr)ptr, image.Width * 4);
            }
        }

        return bitmap.Copy();
    }

    private static byte[] EncodeAsPng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, quality: 100);
        return data.ToArray();
    }
}
