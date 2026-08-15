using System.Diagnostics;
using System.IO;

namespace Quake3InstaGibLauncher.Core.Services;

public sealed record LaunchOutcome(bool Success, string? ErrorMessage, int? ProcessId, IReadOnlyList<string> Arguments);

/// <summary>
/// Avvia realmente l'eseguibile di ioquake3 come processo figlio, con gli argomenti gia' costruiti
/// da <see cref="CommandBuilder"/>. Usa ProcessStartInfo.ArgumentList (non concatenazione di
/// stringhe) cosi' ogni argomento viene passato al processo esattamente com'e', anche se contiene
/// spazi o caratteri speciali, senza bisogno di quoting manuale ne' rischio di injection.
/// Non dipende dalla struttura di cartelle di una piattaforma specifica: riceve gia' il percorso
/// risolto dell'eseguibile e la working directory da usare.
/// </summary>
public sealed class LaunchService
{
    public LaunchOutcome Launch(string executablePath, string workingDirectory, IReadOnlyList<string> arguments)
    {
        if (!File.Exists(executablePath))
        {
            return new LaunchOutcome(
                Success: false,
                ErrorMessage: $"Eseguibile non trovato: {executablePath}. Controlla il percorso nelle Impostazioni.",
                ProcessId: null,
                Arguments: arguments);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new LaunchOutcome(false, "Il sistema operativo non ha avviato il processo.", null, arguments);
            }

            return new LaunchOutcome(true, null, process.Id, arguments);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            return new LaunchOutcome(false, $"Impossibile avviare ioquake3: {ex.Message}", null, arguments);
        }
    }
}
