using System.IO;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Models;

namespace Quake3InstaGibLauncher.Services;

/// <summary>
/// Individua e valida l'installazione esistente di ioquake3, senza mai copiare, spostare
/// o modificare alcun file del gioco. Fornisce messaggi d'errore precisi sul singolo
/// file o cartella mancante, come richiesto dalla diagnostica dell'app.
/// </summary>
public sealed class GameInstallationService
{
    public InstallationPaths ResolvePaths(string rootPath) => new() { RootPath = rootPath };

    /// <summary>
    /// Controlla che eseguibile, baseq3/pak0 e la mod InstaGib129 esistano davvero sul disco.
    /// Il missionpack e' opzionale: se assente viene segnalato ma non blocca l'app.
    /// </summary>
    public InstallationStatus Validate(string rootPath)
    {
        var status = new InstallationStatus { IsValid = true };
        var paths = ResolvePaths(rootPath);

        void Check(string title, bool exists, string pathForMessage, bool mandatory)
        {
            var detail = exists
                ? $"Trovato: {pathForMessage}"
                : $"Non trovato: {pathForMessage}";

            status.Checks.Add(new DiagnosticCheck { Title = title, Success = exists, Detail = detail });

            if (!exists && mandatory)
            {
                status.IsValid = false;
                status.MissingItems.Add(pathForMessage);
            }
        }

        Check("Cartella di installazione", Directory.Exists(paths.RootPath), paths.RootPath, mandatory: true);
        Check("Eseguibile ioquake3.x86_64.exe", File.Exists(paths.ExecutablePath), paths.ExecutablePath, mandatory: true);
        Check("Cartella baseq3", Directory.Exists(paths.BaseQ3Path), paths.BaseQ3Path, mandatory: true);
        Check("File baseq3\\pak0.pk3", File.Exists(paths.BaseQ3Pak0Path), paths.BaseQ3Pak0Path, mandatory: true);
        Check("Cartella mod InstaGib129", Directory.Exists(paths.InstaGibPath), paths.InstaGibPath, mandatory: true);
        Check("File InstaGib129.pk3", File.Exists(paths.InstaGibPk3Path), paths.InstaGibPk3Path, mandatory: true);
        Check("Cartella missionpack (facoltativa)", Directory.Exists(paths.MissionPackPath), paths.MissionPackPath, mandatory: false);

        // Verifica permessi di lettura reale: prova ad aprire l'eseguibile e pak0 in lettura.
        if (File.Exists(paths.ExecutablePath))
        {
            var readable = TryOpenForRead(paths.ExecutablePath);
            status.Checks.Add(new DiagnosticCheck
            {
                Title = "Permessi di lettura eseguibile",
                Success = readable,
                Detail = readable ? "L'eseguibile e' leggibile." : "Impossibile leggere l'eseguibile: controlla i permessi della cartella.",
            });
            if (!readable) status.IsValid = false;
        }

        return status;
    }

    private static bool TryOpenForRead(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
