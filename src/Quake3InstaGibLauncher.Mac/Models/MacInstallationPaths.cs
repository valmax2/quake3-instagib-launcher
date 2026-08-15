namespace Quake3InstaGibLauncher.Mac.Models;

/// <summary>
/// Percorsi risolti dell'installazione ioquake3 su macOS. A differenza di Windows, dove tutto
/// vive sotto un'unica cartella, su macOS l'eseguibile (dentro un .app bundle) e i dati di
/// gioco (baseq3) sono tipicamente in DUE posti diversi:
///   - l'eseguibile: es. /Applications/ioquake3.app/Contents/MacOS/ioquake3
///   - i dati (baseq3, InstaGib129, missionpack): es. ~/Library/Application Support/Quake3/
/// Fonti: https://ioquake3.org/help/players-guide/ , https://www.macsourceports.com/game/quake3arena/
/// </summary>
public sealed class MacInstallationPaths
{
    /// <summary>Percorso del binario eseguibile reale dentro il bundle .app (Contents/MacOS/ioquake3).</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>Cartella radice dei dati di gioco (contiene baseq3, InstaGib129, missionpack).</summary>
    public required string DataRootPath { get; init; }

    public string BaseQ3Path => Path.Combine(DataRootPath, "baseq3");
    public string BaseQ3Pak0Path => Path.Combine(BaseQ3Path, "pak0.pk3");
    public string InstaGibPath => Path.Combine(DataRootPath, "InstaGib129");
    public string InstaGibPk3Path => Path.Combine(InstaGibPath, "InstaGib129.pk3");
    public string MissionPackPath => Path.Combine(DataRootPath, "missionpack");

    /// <summary>Working directory da usare per avviare il processo: la cartella che contiene
    /// il binario stesso (Contents/MacOS/), come farebbe Finder aprendo il bundle .app.</summary>
    public string WorkingDirectory => Path.GetDirectoryName(ExecutablePath) ?? DataRootPath;
}
