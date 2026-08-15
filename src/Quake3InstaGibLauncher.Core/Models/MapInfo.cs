namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>
/// Metadati di una mappa Quake III individuata analizzando i file .pk3 dell'installazione
/// (scripts/arenas.txt, scripts/*.arena, maps/*.bsp, levelshots/*).
/// Questo oggetto viene serializzato nella cache locale dell'app (metadati, non asset di gioco).
/// </summary>
public sealed class MapInfo
{
    /// <summary>Nome tecnico usato dal motore per "+map", es. "q3dm17".</summary>
    public required string TechnicalName { get; init; }

    /// <summary>Nome completo leggibile, es. "The Longest Yard". Se assente, uguale al nome tecnico.</summary>
    public required string LongName { get; set; }

    /// <summary>Modalita' di gioco dichiarate compatibili dai metadati arena.</summary>
    public List<GameType> SupportedGameTypes { get; set; } = new();

    /// <summary>Sorgente della mappa (baseq3, missionpack, InstaGib129, personalizzata) per l'etichetta in UI.</summary>
    public required MapSource Source { get; set; }

    /// <summary>
    /// True se il .bsp si trova fisicamente nella cartella missionpack: in tal caso il comando di
    /// avvio deve includere "+set fs_basegame missionpack" affinche' il motore trovi la mappa,
    /// indipendentemente dal fatto che la mappa sia originale o aggiunta dall'utente in quella cartella.
    /// </summary>
    public bool RequiresMissionPackBaseGame { get; set; }

    /// <summary>Nome del file .pk3 (senza percorso) da cui proviene il file .bsp della mappa.</summary>
    public required string SourcePk3FileName { get; set; }

    /// <summary>Percorso assoluto del .pk3 che contiene il .bsp, usato per riaprire l'archivio se serve.</summary>
    public required string SourcePk3FullPath { get; set; }

    /// <summary>Percorso virtuale interno del levelshot (es. "levelshots/q3dm17.jpg"), se trovato.</summary>
    public string? LevelshotEntryPath { get; set; }

    /// <summary>Nome del .pk3 in cui e' stato trovato il levelshot (puo' differire da quello del .bsp).</summary>
    public string? LevelshotPk3FullPath { get; set; }

    /// <summary>Percorso su disco dell'anteprima gia' convertita/cache (PNG), se disponibile.</summary>
    public string? CachedPreviewPath { get; set; }

    /// <summary>True se i metadati provengono da un blocco arena reale; false se dedotti dal solo .bsp.</summary>
    public bool HasArenaMetadata { get; set; }

    /// <summary>Elenco (informativo) dei bot consigliati dal file arena originale, se presente.</summary>
    public string? SuggestedBots { get; set; }

    public bool SupportsGameType(GameType type) => SupportedGameTypes.Contains(type);

    public string SourceLabel => Source.ToDisplayName();
}
