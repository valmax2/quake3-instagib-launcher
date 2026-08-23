using System.IO;
using System.Text;

namespace Quake3InstaGibLauncher.Core.Services;

public sealed record StartupCfgResult(string FileNameForExec, string FullPathInGameDir);

/// <summary>
/// Scrive in un file .cfg "sciolto" tutte le impostazioni (cvar server/video/giocatore) che il
/// launcher passerebbe altrimenti come lunga lista di argomenti "+set" sulla riga di comando.
///
/// Vedi CommandBuilder.ConsolidateForStartupCfg per il perche': il motore ioquake3 ha un limite
/// interno al numero di argomenti "+" accettati insieme all'avvio, superato facilmente da una
/// configurazione completa (rotazione mappe + profilo giocatore + tasti personalizzati), che causa
/// un ritorno silenzioso al menu principale senza alcun errore visibile.
///
/// NOME FISSO (stesso schema di RotationCfgService/KeyBindingCfgService, mai suffissi casuali):
/// il file va comunque rigenerato ad ogni avvio, non serve un nome diverso per ogni lancio.
/// </summary>
public sealed class StartupCfgService
{
    private const string FixedFileName = "q3ilauncher_startup.cfg";

    public StartupCfgResult WriteStartupCfg(string targetModDirectory, string cfgContent)
    {
        if (!Directory.Exists(targetModDirectory))
            throw new LaunchValidationException($"Cartella non trovata per scrivere il file di avvio: {targetModDirectory}");

        var fullPath = Path.Combine(targetModDirectory, FixedFileName);
        var content =
            "// File generato automaticamente da Quake3InstaGibLauncher - impostazioni di avvio.\n" +
            "// Sicuro da cancellare: viene rigenerato ad ogni avvio.\n\n" +
            cfgContent;

        File.WriteAllText(fullPath, content, Encoding.ASCII);

        return new StartupCfgResult(FixedFileName, fullPath);
    }
}
