using System.IO;
using System.Text;

namespace Quake3InstaGibLauncher.Core.Services;

public sealed record RotationCfgResult(string FileNameForExec, string FullPathInGameDir, string? HistoryCopyPath);

/// <summary>
/// Genera un file .cfg di rotazione mappe in stile Quake III (set + vstr) e lo scrive come file
/// "sciolto" (non dentro un .pk3) nella cartella attiva del motore, cosi' che "+exec &lt;file&gt;"
/// lo trovi davvero tramite il search path di ioquake3.
///
/// NOME FISSO (non piu' univoco con suffisso casuale come nelle versioni precedenti): la
/// rotazione va comunque rigenerata ad ogni avvio (puo' cambiare tra una partita e l'altra), quindi
/// non serviva un file diverso per ogni lancio — quella scelta creava solo decine di
/// "q3ilauncher_rotation_XXXXXXXX.cfg" abbandonati per sempre nella cartella di gioco reale
/// dell'utente (stesso bug reale trovato in KeyBindingCfgService: file mai ripuliti, visibili a
/// occhio dopo poche sessioni di test). Sovrascrivere lo stesso file e' sicuro: e' posseduto
/// interamente da questo launcher. Nessun file di gioco originale viene toccato o cancellato. La
/// scelta di QUALE cartella sia quella "attiva" (fs_game vs fs_basegame) e' una decisione
/// specifica della piattaforma/installazione e viene presa dal chiamante, non da questo servizio.
/// </summary>
public sealed class RotationCfgService
{
    private const string FixedFileName = "q3ilauncher_rotation.cfg";

    public RotationCfgResult WriteRotationCfg(string targetModDirectory, IReadOnlyList<string> orderedMapTechnicalNames)
    {
        if (orderedMapTechnicalNames.Count == 0)
            throw new LaunchValidationException("La rotazione deve contenere almeno una mappa.");

        foreach (var map in orderedMapTechnicalNames)
            CommandBuilder.ValidateMapName(map);

        if (!Directory.Exists(targetModDirectory))
            throw new LaunchValidationException($"Cartella non trovata per scrivere il file di rotazione: {targetModDirectory}");

        CleanupLegacyFiles(targetModDirectory);

        var fullPath = Path.Combine(targetModDirectory, FixedFileName);
        var content = BuildCfgContent(orderedMapTechnicalNames);
        File.WriteAllText(fullPath, content, Encoding.ASCII);

        string? historyPath = null;
        try
        {
            AppPaths.EnsureDirectoriesExist();
            historyPath = Path.Combine(AppPaths.CfgHistoryDir, FixedFileName);
            File.Copy(fullPath, historyPath, overwrite: true);
        }
        catch
        {
            // La copia di cortesia per la diagnostica e' opzionale: se fallisce non blocca l'avvio.
            historyPath = null;
        }

        return new RotationCfgResult(FixedFileName, fullPath, historyPath);
    }

    /// <summary>Pulizia una tantum dei file della vecchia generazione (nome con suffisso casuale,
    /// mai ripulito prima d'ora): rimuove qualunque "q3ilauncher_rotation_XXXXXXXX.cfg" trovato,
    /// lasciando solo il nome fisso attuale. Best-effort: un file bloccato o permessi mancanti non
    /// blocca mai l'avvio della partita.</summary>
    private static void CleanupLegacyFiles(string directory)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "q3ilauncher_rotation_*.cfg"))
            {
                try { File.Delete(file); } catch { /* in uso o permessi: si ignora */ }
            }
        }
        catch { /* enumerazione fallita: si ignora, non e' critico */ }
    }

    private static string BuildCfgContent(IReadOnlyList<string> maps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// File generato automaticamente da Quake3InstaGibLauncher - rotazione mappe.");
        sb.AppendLine("// Sicuro da cancellare: viene rigenerato ad ogni avvio con una rotazione.");
        sb.AppendLine();

        for (var i = 0; i < maps.Count; i++)
        {
            var nextIndex = (i + 1) % maps.Count;
            sb.AppendLine($"set m{i + 1} \"map {maps[i]}; set nextmap vstr m{nextIndex + 1}\"");
        }

        sb.AppendLine("vstr m1");
        return sb.ToString();
    }
}
