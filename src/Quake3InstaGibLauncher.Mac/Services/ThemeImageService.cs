using Avalonia;
using Avalonia.Media.Imaging;
using Quake3InstaGibLauncher.Core.Services;

namespace Quake3InstaGibLauncher.Mac.Services;

/// <summary>
/// Importa immagini scelte dall'utente (sfondo finestra, sfondo pulsanti, sfondo Home) nella
/// cartella dati dell'app, ridimensionandole se necessario. Equivalente macOS di
/// Quake3InstaGibLauncher.Services.ThemeImageService (Windows): stessa strategia "nome nuovo e
/// unico per ogni import" (categoria + timestamp), che tiene automaticamente una cronologia delle
/// immagini importate in passato e non richiede nessun trucco di invalidazione cache (a differenza
/// di WPF, Avalonia.Media.Imaging.Bitmap non tiene una cache per-percorso: caricarlo di nuovo da
/// disco restituisce sempre i pixel correnti del file).
/// </summary>
public static class ThemeImageService
{
    /// <summary>Importa un'immagine scelta dall'utente. Restituisce il percorso del file
    /// copiato/ridimensionato nella cartella tema dell'app, e se e' un GIF.
    ///
    /// NOTA: Avalonia non decodifica/anima i GIF nativamente (a differenza di WPF): un GIF scelto
    /// qui viene comunque copiato cosi' come e' (per non perdere silenziosamente la scelta
    /// dell'utente), ma verra' mostrato come immagine statica (primo fotogramma) dove usato.</summary>
    public static (string Path, bool IsGif) Import(string sourcePath, string category, int maxResolution)
    {
        AppPaths.EnsureDirectoriesExist();
        Directory.CreateDirectory(ThemeDir);

        var uniqueName = $"{category}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}";
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();

        if (ext == ".gif")
        {
            var gifDestPath = Path.Combine(ThemeDir, uniqueName + ".gif");
            File.Copy(sourcePath, gifDestPath, overwrite: true);
            return (gifDestPath, true);
        }

        PixelSize originalSize;
        using (var probeStream = File.OpenRead(sourcePath))
        using (var probe = new Bitmap(probeStream))
            originalSize = probe.PixelSize;

        var longSide = Math.Max(originalSize.Width, originalSize.Height);
        var pngDestPath = Path.Combine(ThemeDir, uniqueName + ".png");

        using (var decodeStream = File.OpenRead(sourcePath))
        {
            using var final = longSide <= maxResolution
                ? new Bitmap(decodeStream)
                : (originalSize.Width >= originalSize.Height
                    ? Bitmap.DecodeToWidth(decodeStream, maxResolution)
                    : Bitmap.DecodeToHeight(decodeStream, maxResolution));

            using var output = File.Create(pngDestPath);
            final.Save(output);
        }

        return (pngDestPath, false);
    }

    public static string ThemeDir => Path.Combine(AppPaths.Root, "theme");
}
