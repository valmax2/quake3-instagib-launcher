using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;

namespace Quake3InstaGibLauncher.Quest.Services;

/// <summary>
/// Genera/legge le anteprime PNG delle mappe (levelshot) nella cache privata dell'app
/// (Context.CacheDir: gestita dal sistema, non richiede permessi, ripulita automaticamente se
/// serve spazio). Stesso schema concettuale di MapCacheService (Core) ma senza passare da
/// AppPaths, il cui fallback generico per "altri Unix" non e' garantito adatto ad Android:
/// meglio usare direttamente la cartella cache ufficiale della piattaforma.
/// </summary>
public sealed class QuestMapPreviewService
{
    private string CacheDir => Path.Combine(
        global::Android.App.Application.Context.CacheDir!.AbsolutePath, "map_previews");

    /// <summary>Percorso del PNG di anteprima per questa mappa, generandolo al volo dal .pk3 se
    /// non e' gia' in cache. Null se la mappa non ha alcun levelshot nei suoi .pk3.</summary>
    public string? GetOrCreatePreviewPath(MapInfo map)
    {
        if (map.LevelshotEntryPath is null || map.LevelshotPk3FullPath is null)
            return null;

        Directory.CreateDirectory(CacheDir);

        var safeName = string.Concat(map.TechnicalName.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-'));
        var cacheFilePath = Path.Combine(CacheDir, $"{map.Source}_{safeName}.png".ToLowerInvariant());

        if (File.Exists(cacheFilePath))
            return cacheFilePath;

        try
        {
            var rawBytes = Pk3Scanner.ReadEntryBytes(map.LevelshotPk3FullPath, map.LevelshotEntryPath);
            var pngBytes = QuestImageConversionService.ConvertToPngBytes(rawBytes, map.LevelshotEntryPath);
            File.WriteAllBytes(cacheFilePath, pngBytes);
            return cacheFilePath;
        }
        catch
        {
            return null; // anteprima non generabile (pk3 corrotto, formato raro): la UI mostra un segnaposto
        }
    }
}
