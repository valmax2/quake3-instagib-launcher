using System.IO;
using System.Windows.Media.Imaging;
using Quake3InstaGibLauncher.Core.Services;

namespace Quake3InstaGibLauncher.Services;

/// <summary>
/// Importa immagini scelte dall'utente (logo, sfondo finestra, sfondo pulsanti) nella cartella
/// dati dell'app, ridimensionandole se necessario. Le immagini statiche (PNG/JPG/BMP) vengono
/// ridotte alla risoluzione massima configurata e ri-salvate come PNG (preserva la trasparenza).
/// I file GIF vengono copiati cosi' come sono, senza ridimensionamento: un GIF animato e'
/// essenzialmente una sequenza di fotogrammi, e ridimensionarli in modo affidabile con le sole
/// API di WPF (senza dipendenze esterne) rischierebbe di rompere l'animazione o la trasparenza;
/// dato che i logo/loghi tipici sono gia' immagini piccole, il costo in spazio e' trascurabile.
/// </summary>
public static class ThemeImageService
{
    /// <summary>Importa un'immagine scelta dall'utente. Restituisce il percorso del file
    /// copiato/ridimensionato nella cartella tema dell'app, e se e' un GIF (potenzialmente animato).
    ///
    /// Ogni import crea un file con un nome NUOVO E UNICO (categoria + timestamp), non sovrascrive
    /// mai un file precedente: questo tiene automaticamente una "cronologia" di tutte le immagini
    /// importate in passato (usata dalla galleria di anteprime in Personalizza interfaccia, per
    /// riselezionarne una gia' usata senza dover riaprire la finestra di scelta file), ed evita
    /// anche alla radice un fastidioso problema di cache di WPF: caricare un URI file gia' visto in
    /// precedenza restituirebbe l'immagine vecchia anche dopo aver sostituito il contenuto del file.</summary>
    public static (string Path, bool IsGif) Import(string sourcePath, string category, int maxResolution)
    {
        AppPaths.EnsureDirectoriesExist();
        Directory.CreateDirectory(ThemeDir);

        var uniqueName = $"{category}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}";
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();

        if (ext == ".gif")
        {
            var destPath = Path.Combine(ThemeDir, uniqueName + ".gif");
            File.Copy(sourcePath, destPath, overwrite: true);
            return (destPath, true);
        }

        // Immagine statica: decodifica vincolando la dimensione massima (evita di caricare in
        // memoria immagini enormi), poi ri-salva come PNG per preservare eventuale trasparenza.
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(sourcePath, UriKind.Absolute);

        // Vincola solo la dimensione maggiore per non deformare l'immagine: impostiamo entrambe
        // le DecodePixel* e lasciamo che WPF scelga quella che rispetta l'aspect ratio se una
        // delle due supera il massimo (WPF applica il vincolo piu' restrittivo automaticamente
        // solo se e' impostata UNA delle due; per sicurezza calcoliamo qui le dimensioni finali).
        var (targetW, targetH) = ComputeTargetSize(sourcePath, maxResolution);
        if (targetW > 0 && targetH > 0)
        {
            bitmap.DecodePixelWidth = targetW;
            bitmap.DecodePixelHeight = targetH;
        }

        bitmap.EndInit();
        bitmap.Freeze();

        var pngEncoder = new PngBitmapEncoder();
        pngEncoder.Frames.Add(BitmapFrame.Create(bitmap));

        var pngDestPath = Path.Combine(ThemeDir, uniqueName + ".png");
        using (var output = File.Create(pngDestPath))
            pngEncoder.Save(output);

        return (pngDestPath, false);
    }

    private static (int Width, int Height) ComputeTargetSize(string sourcePath, int maxResolution)
    {
        try
        {
            var probe = new BitmapImage();
            probe.BeginInit();
            probe.CacheOption = BitmapCacheOption.OnLoad;
            probe.UriSource = new Uri(sourcePath, UriKind.Absolute);
            probe.EndInit();

            var width = probe.PixelWidth;
            var height = probe.PixelHeight;

            if (width <= maxResolution && height <= maxResolution)
                return (0, 0); // nessun ridimensionamento necessario

            var scale = Math.Min((double)maxResolution / width, (double)maxResolution / height);
            return ((int)Math.Round(width * scale), (int)Math.Round(height * scale));
        }
        catch
        {
            return (0, 0); // se la lettura delle dimensioni fallisce, si importa senza ridimensionare
        }
    }

    public static string ThemeDir => Path.Combine(AppPaths.Root, "theme");
}
