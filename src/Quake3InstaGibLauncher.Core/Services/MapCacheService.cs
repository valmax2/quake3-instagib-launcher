using System.IO;
using System.Text.Json;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Core.Services;

public sealed class MapCacheRefreshResult
{
    public required IReadOnlyList<MapInfo> Maps { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
}

/// <summary>
/// Coordina la scansione dei .pk3 (Pk3Scanner), la conversione delle anteprime e la cache
/// persistente su disco in AppPaths.CacheDir, cosi' che l'app non debba riaprire tutti i .pk3
/// (alcuni da centinaia di MB) a ogni avvio. La cache contiene SOLO metadati e piccole anteprime
/// PNG: nessun contenuto di gioco viene mai estratto in modo permanente.
///
/// La decodifica delle immagini (JPG/PNG/TGA -> PNG) e' specifica del framework grafico di ogni
/// piattaforma (WPF su Windows, SkiaSharp/Avalonia su macOS): viene fornita dal chiamante tramite
/// <paramref name="convertToPng"/> cosi' che questo servizio resti privo di dipendenze da UI toolkit.
/// </summary>
public sealed class MapCacheService(Func<byte[], string, byte[]> convertToPng)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Carica la cache esistente da disco, se presente, senza rileggere i .pk3.</summary>
    public IReadOnlyList<MapInfo>? TryLoadFromDisk()
    {
        if (!File.Exists(AppPaths.MapsIndexFile))
            return null;

        try
        {
            var json = File.ReadAllText(AppPaths.MapsIndexFile);
            return JsonSerializer.Deserialize<List<MapInfo>>(json, JsonOptions);
        }
        catch
        {
            return null; // cache corrotta: la scansione completa la rigenerera'
        }
    }

    /// <summary>
    /// Riscansiona per intero l'installazione, rigenera le anteprime mancanti e riscrive la cache.
    /// </summary>
    public async Task<MapCacheRefreshResult> RefreshAsync(
        IEnumerable<(string Directory, MapSource Source)> rootsToScan,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectoriesExist();

        progress?.Report("Analisi dei file .pk3 in corso...");
        var scanResult = await Task.Run(() => new Pk3Scanner().ScanInstallation(rootsToScan), cancellationToken);

        var errors = new List<string>(scanResult.Errors);

        var mapsList = scanResult.Maps
            .OrderBy(m => m.TechnicalName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = mapsList.Count;
        var done = 0;

        foreach (var map in mapsList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            done++;
            progress?.Report($"Anteprima {done}/{total}: {map.TechnicalName}");

            if (map.LevelshotEntryPath is null || map.LevelshotPk3FullPath is null)
                continue; // nessun levelshot nel pk3: la UI mostrera' il segnaposto

            var cacheFileName = BuildPreviewCacheFileName(map);
            var cacheFilePath = Path.Combine(AppPaths.PreviewsDir, cacheFileName);

            try
            {
                if (!File.Exists(cacheFilePath))
                {
                    var rawBytes = await Task.Run(
                        () => Pk3Scanner.ReadEntryBytes(map.LevelshotPk3FullPath, map.LevelshotEntryPath),
                        cancellationToken);

                    var pngBytes = await Task.Run(
                        () => convertToPng(rawBytes, map.LevelshotEntryPath),
                        cancellationToken);

                    await File.WriteAllBytesAsync(cacheFilePath, pngBytes, cancellationToken);
                }

                map.CachedPreviewPath = cacheFilePath;
            }
            catch (Exception ex)
            {
                errors.Add($"Anteprima non generata per {map.TechnicalName} ({map.SourceLabel}): {ex.Message}");
            }
        }

        progress?.Report("Salvataggio indice mappe...");
        await WriteIndexAtomicAsync(mapsList, cancellationToken);

        return new MapCacheRefreshResult { Maps = mapsList, Errors = errors };
    }

    private static async Task WriteIndexAtomicAsync(List<MapInfo> maps, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(maps, JsonOptions);
        var tempFile = AppPaths.MapsIndexFile + ".tmp";
        await File.WriteAllTextAsync(tempFile, json, cancellationToken);
        File.Move(tempFile, AppPaths.MapsIndexFile, overwrite: true);
    }

    private static string BuildPreviewCacheFileName(MapInfo map)
    {
        var safeName = string.Concat(map.TechnicalName.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-'));
        return $"{map.Source}_{safeName}.png".ToLowerInvariant();
    }

    /// <summary>Cancella solo la cache delle anteprime e l'indice mappe (non tocca le impostazioni).</summary>
    public void ClearCache()
    {
        if (Directory.Exists(AppPaths.PreviewsDir))
        {
            foreach (var file in Directory.GetFiles(AppPaths.PreviewsDir))
            {
                try { File.Delete(file); } catch { /* file in uso: verra' rigenerato comunque */ }
            }
        }

        if (File.Exists(AppPaths.MapsIndexFile))
            File.Delete(AppPaths.MapsIndexFile);
    }
}
