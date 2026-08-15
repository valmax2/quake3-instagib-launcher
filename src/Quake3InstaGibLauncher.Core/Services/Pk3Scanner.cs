using System.IO;
using System.IO.Compression;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Core.Services;

/// <summary>Esito completo di una scansione: mappe trovate ed eventuali errori (pk3 illeggibili, ecc.).</summary>
public sealed class Pk3ScanResult
{
    public List<MapInfo> Maps { get; } = new();
    public List<string> Errors { get; } = new();
}

/// <summary>
/// Analizza i file .pk3 (archivi ZIP) delle cartelle baseq3, InstaGib129 e missionpack per
/// costruire l'elenco reale delle mappe disponibili, SENZA estrarre o modificare alcun file:
/// ogni .pk3 viene aperto in sola lettura e solo i singoli entry necessari (script arena,
/// levelshot) vengono letti in memoria.
///
/// Il motore ioquake3 carica i .pk3 di una cartella in ordine alfabetico e, a parita' di
/// percorso interno, l'ultimo pk3 caricato "vince" (sovrascrive) sui precedenti: questo scanner
/// replica esattamente lo stesso comportamento per restare fedele a cio' che il gioco vedrebbe
/// davvero (es. missionpack/mp-pak0.pk3 viene sovrascritto da missionpack/pak0.pk3).
/// </summary>
public sealed class Pk3Scanner
{
    /// <summary>Estensioni di anteprima (levelshot) riconosciute: JPG/PNG leggibili nativamente
    /// dai framework grafici, TGA da decodificare esplicitamente (vedi convertitore per piattaforma).</summary>
    public static bool IsSupportedImageEntry(string virtualEntryPath)
    {
        var ext = Path.GetExtension(virtualEntryPath).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".tga";
    }

    /// <summary>
    /// Analizza le cartelle indicate (ciascuna con la propria sorgente logica: baseq3, InstaGib129,
    /// missionpack, ...). Ogni piattaforma risolve i percorsi reali dell'installazione locale e li
    /// passa qui: questo scanner non conosce e non assume nulla sulla struttura di cartelle del
    /// sistema operativo ospite.
    /// </summary>
    public Pk3ScanResult ScanInstallation(IEnumerable<(string Directory, MapSource Source)> rootsToScan)
    {
        var result = new Pk3ScanResult();

        foreach (var (directory, source) in rootsToScan)
            ScanDirectory(directory, source, result);

        return result;
    }

    private static void ScanDirectory(string directoryPath, MapSource source, Pk3ScanResult result)
    {
        if (!Directory.Exists(directoryPath))
            return;

        var pk3Files = Directory.GetFiles(directoryPath, "*.pk3")
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (pk3Files.Length == 0)
            return;

        var arenaByMap = new Dictionary<string, ArenaEntry?>(StringComparer.OrdinalIgnoreCase);
        var bspByMap = new Dictionary<string, (string Pk3Name, string Pk3Path)>(StringComparer.OrdinalIgnoreCase);
        var levelshotByMap = new Dictionary<string, (string EntryPath, string Pk3Path)>(StringComparer.OrdinalIgnoreCase);

        foreach (var pk3Path in pk3Files)
        {
            try
            {
                using var archive = ZipFile.OpenRead(pk3Path);

                foreach (var entry in archive.Entries)
                {
                    var name = entry.FullName.Replace('\\', '/');
                    if (name.Length == 0 || name.EndsWith('/'))
                        continue; // entry "cartella", niente da leggere

                    if (name.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".arena", StringComparison.OrdinalIgnoreCase))
                    {
                        ParseArenaEntryInto(entry, arenaByMap, result);
                    }
                    else if (string.Equals(name, "scripts/arenas.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        ParseArenaEntryInto(entry, arenaByMap, result);
                    }
                    else if (name.StartsWith("maps/", StringComparison.OrdinalIgnoreCase) &&
                             name.EndsWith(".bsp", StringComparison.OrdinalIgnoreCase))
                    {
                        var mapName = Path.GetFileNameWithoutExtension(name);
                        bspByMap[mapName] = (Path.GetFileName(pk3Path)!, pk3Path); // ultimo pk3 alfabetico vince
                    }
                    else if (name.StartsWith("levelshots/", StringComparison.OrdinalIgnoreCase) &&
                             IsSupportedImageEntry(name))
                    {
                        var mapName = Path.GetFileNameWithoutExtension(name);
                        levelshotByMap[mapName] = (name, pk3Path);
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                result.Errors.Add($"{pk3Path}: archivio zip non valido o corrotto ({ex.Message})");
            }
            catch (IOException ex)
            {
                result.Errors.Add($"{pk3Path}: errore di lettura ({ex.Message})");
            }
            catch (UnauthorizedAccessException ex)
            {
                result.Errors.Add($"{pk3Path}: permessi insufficienti ({ex.Message})");
            }
        }

        foreach (var (mapName, bsp) in bspByMap)
        {
            arenaByMap.TryGetValue(mapName, out var arena);
            levelshotByMap.TryGetValue(mapName, out var shot);

            var longName = arena?.LongName;

            result.Maps.Add(new MapInfo
            {
                TechnicalName = mapName,
                LongName = string.IsNullOrWhiteSpace(longName) ? mapName : longName!,
                SupportedGameTypes = GameTypeExtensions.ParseArenaTypeField(arena?.TypeField).ToList(),
                Source = ClassifySource(source, bsp.Pk3Name),
                RequiresMissionPackBaseGame = source == MapSource.MissionPack,
                SourcePk3FileName = bsp.Pk3Name,
                SourcePk3FullPath = bsp.Pk3Path,
                LevelshotEntryPath = shot.EntryPath,
                LevelshotPk3FullPath = shot.EntryPath is null ? null : shot.Pk3Path,
                HasArenaMetadata = arena is not null,
                SuggestedBots = arena?.Bots,
            });
        }
    }

    /// <summary>
    /// I .pk3 originali dell'installazione (id Software / Team Arena / patch community note).
    /// Qualunque altro .pk3 trovato dentro baseq3 o missionpack e' considerato "personalizzato":
    /// e' cosi' che, in pratica, vengono distribuite le mappe custom per Quake III (si aggiunge
    /// semplicemente un nuovo .pk3 nella cartella baseq3). Questo permette all'app di distinguere
    /// le mappe originali da quelle aggiunte dall'utente senza dover indovinare nulla.
    /// </summary>
    private static readonly HashSet<string> KnownBaseQ3Paks = new(StringComparer.OrdinalIgnoreCase)
    {
        "pak0.pk3", "pak1.pk3", "pak2.pk3", "pak3.pk3", "pak4.pk3", "pak5.pk3",
        "pak6.pk3", "pak7.pk3", "pak8.pk3", "pak8a.pk3",
        "q3wpak0.pk3", "q3wpak1.pk3", "q3wpak2.pk3", "q3wpak3.pk3", "q3wpak4.pk3",
    };

    private static readonly HashSet<string> KnownMissionPackPaks = new(StringComparer.OrdinalIgnoreCase)
    {
        "pak0.pk3", "pak1.pk3", "pak2.pk3", "pak3.pk3", "mp-pak0.pk3",
    };

    private static MapSource ClassifySource(MapSource directorySource, string pk3FileName) => directorySource switch
    {
        MapSource.BaseQ3 when !KnownBaseQ3Paks.Contains(pk3FileName) => MapSource.Custom,
        MapSource.MissionPack when !KnownMissionPackPaks.Contains(pk3FileName) => MapSource.Custom,
        _ => directorySource,
    };

    private static void ParseArenaEntryInto(
        ZipArchiveEntry entry,
        Dictionary<string, ArenaEntry?> arenaByMap,
        Pk3ScanResult result)
    {
        try
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();

            foreach (var parsed in ArenaFileParser.Parse(text))
                arenaByMap[parsed.Map] = parsed; // ultimo pk3 alfabetico vince, come il motore
        }
        catch (Exception ex)
        {
            result.Errors.Add($"{entry.FullName}: impossibile leggere lo script arena ({ex.Message})");
        }
    }

    /// <summary>Legge in memoria i byte grezzi di un entry di un .pk3 (levelshot), senza estrarlo su disco.</summary>
    public static byte[] ReadEntryBytes(string pk3FullPath, string entryPath)
    {
        using var archive = ZipFile.OpenRead(pk3FullPath);
        var entry = archive.GetEntry(entryPath)
            ?? archive.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\', '/'), entryPath, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Entry '{entryPath}' non trovato in {pk3FullPath}");

        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
