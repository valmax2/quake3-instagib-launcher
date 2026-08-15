using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Mac.Models;

namespace Quake3InstaGibLauncher.Mac.Services;

/// <summary>
/// Individua l'installazione di ioquake3 su macOS provando i percorsi documentati piu' comuni,
/// senza mai copiare, spostare o modificare alcun file. Se non trova nulla in automatico,
/// l'app chiede all'utente di indicare manualmente eseguibile e cartella dati (vedi Impostazioni).
///
/// Percorsi verificati, in ordine, secondo la documentazione ufficiale e le guide della community:
///   - Eseguibile: /Applications/ioquake3.app/Contents/MacOS/ioquake3
///                 /Applications/ioquake3/ioquake3.app/Contents/MacOS/ioquake3
///   - Dati (baseq3): ~/Library/Application Support/Quake3/baseq3
///                     /Applications/ioquake3/ioquake3.app/Contents/MacOS/baseq3
///                     /Applications/ioquake3/baseq3
/// Fonti: https://ioquake3.org/help/players-guide/ ,
///        https://www.macsourceports.com/game/quake3arena/ ,
///        https://github.com/diegoulloao/ioquake3-mac-install
/// </summary>
public sealed class MacInstallationLocator
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static readonly string[] ExecutableCandidates =
    {
        "/Applications/ioquake3.app/Contents/MacOS/ioquake3",
        "/Applications/ioquake3/ioquake3.app/Contents/MacOS/ioquake3",
    };

    private static string[] DataRootCandidates => new[]
    {
        Path.Combine(Home, "Library", "Application Support", "Quake3"),
        "/Applications/ioquake3/ioquake3.app/Contents/MacOS",
        "/Applications/ioquake3",
    };

    /// <summary>Prova a individuare automaticamente eseguibile e cartella dati. Restituisce null
    /// per il campo non trovato: il chiamante propone poi la selezione manuale in Impostazioni.</summary>
    public (string? ExecutablePath, string? DataRootPath) AutoDetect()
    {
        var exe = ExecutableCandidates.FirstOrDefault(File.Exists);

        var dataRoot = DataRootCandidates.FirstOrDefault(dir =>
            File.Exists(Path.Combine(dir, "baseq3", "pak0.pk3")));

        return (exe, dataRoot);
    }

    public MacInstallationPaths ResolvePaths(string executablePath, string dataRootPath) => new()
    {
        ExecutablePath = executablePath,
        DataRootPath = dataRootPath,
    };

    /// <summary>Controlla che eseguibile, baseq3/pak0 e la mod InstaGib129 esistano davvero.
    /// Il missionpack e' opzionale: se assente viene segnalato ma non blocca l'app.</summary>
    public InstallationStatus Validate(string executablePath, string dataRootPath)
    {
        var status = new InstallationStatus { IsValid = true };

        void Check(string title, bool exists, string pathForMessage, bool mandatory)
        {
            var detail = exists ? $"Trovato: {pathForMessage}" : $"Non trovato: {pathForMessage}";
            status.Checks.Add(new DiagnosticCheck { Title = title, Success = exists, Detail = detail });

            if (!exists && mandatory)
            {
                status.IsValid = false;
                status.MissingItems.Add(pathForMessage);
            }
        }

        Check("Eseguibile ioquake3", !string.IsNullOrEmpty(executablePath) && File.Exists(executablePath),
            string.IsNullOrEmpty(executablePath) ? "(nessun percorso impostato)" : executablePath, mandatory: true);

        var paths = ResolvePaths(executablePath ?? string.Empty, dataRootPath ?? string.Empty);

        Check("Cartella dati (baseq3, InstaGib129, ...)", !string.IsNullOrEmpty(dataRootPath) && Directory.Exists(dataRootPath),
            string.IsNullOrEmpty(dataRootPath) ? "(nessun percorso impostato)" : dataRootPath, mandatory: true);
        Check("Cartella baseq3", Directory.Exists(paths.BaseQ3Path), paths.BaseQ3Path, mandatory: true);
        Check("File baseq3/pak0.pk3", File.Exists(paths.BaseQ3Pak0Path), paths.BaseQ3Pak0Path, mandatory: true);
        Check("Cartella mod InstaGib129", Directory.Exists(paths.InstaGibPath), paths.InstaGibPath, mandatory: true);
        Check("File InstaGib129.pk3", File.Exists(paths.InstaGibPk3Path), paths.InstaGibPk3Path, mandatory: true);
        Check("Cartella missionpack (facoltativa)", Directory.Exists(paths.MissionPackPath), paths.MissionPackPath, mandatory: false);

        if (File.Exists(executablePath))
        {
            var executable = TryCheckExecutableBit(executablePath);
            status.Checks.Add(new DiagnosticCheck
            {
                Title = "Permesso di esecuzione",
                Success = executable,
                Detail = executable
                    ? "Il file ha il permesso di esecuzione."
                    : "Il file non risulta eseguibile: da Terminale esegui \"chmod +x\" sul percorso indicato.",
            });
            if (!executable) status.IsValid = false;
        }

        return status;
    }

    private static bool TryCheckExecutableBit(string path)
    {
        try
        {
            // GetUnixFileMode non e' supportato su Windows (CA1416): questa app macOS gira solo
            // su macOS/Linux a runtime, e il catch sotto copre comunque l'eventualita' remota che
            // l'API non sia disponibile per qualche motivo (es. compilazione di verifica incrociata).
#pragma warning disable CA1416
            var mode = File.GetUnixFileMode(path);
#pragma warning restore CA1416
            return mode.HasFlag(UnixFileMode.UserExecute);
        }
        catch
        {
            return true;
        }
    }
}
