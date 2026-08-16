using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Core.Services;

/// <summary>
/// Genera un file .cfg con i comandi "bind" scelti dall'utente (schermata Tasti/Comandi) e lo
/// scrive come file "sciolto" nella cartella attiva del motore, cosi' che "+exec &lt;file&gt;" lo
/// trovi tramite il search path di ioquake3. Nessun file di gioco originale viene toccato.
///
/// NOME FISSO (non piu' univoco con suffisso casuale come nelle versioni precedenti): il file va
/// rigenerato ad ogni avvio comunque (i tasti possono essere cambiati tra una partita e l'altra),
/// quindi non c'e' alcun motivo di tenerne una copia diversa per ogni lancio — farlo creava solo
/// decine di file "q3ilauncher_binds_XXXXXXXX.cfg" abbandonati per sempre nella cartella di gioco
/// reale dell'utente (bug reale, trovato dall'utente stesso: si accumulavano visibilmente in
/// baseq3/InstaGib129 dopo poche sessioni di test). Sovrascrivere lo stesso file e' sicuro: e'
/// posseduto interamente da questo launcher, mai letto/modificato da altro.
/// </summary>
public sealed class KeyBindingCfgService
{
    private const string FixedFileName = "q3ilauncher_binds.cfg";

    // Un tasto Quake III e' o un singolo carattere (lettera/cifra/simbolo comune) o un nome
    // speciale come SPACE/SHIFT/TAB/MOUSE1/F1: qui ammettiamo lettere/cifre, 1-12 caratteri,
    // senza spazi/virgolette/punto e virgola (nessuna injection di comandi extra nella console).
    private static readonly Regex SafeKeyRegex = new(@"^[A-Za-z0-9]{1,12}$", RegexOptions.Compiled);

    public string? WriteBindingsCfg(string targetModDirectory, IReadOnlyList<KeyBindingEntry> bindings)
    {
        var enabled = bindings.Where(b => b.Enabled).ToList();
        if (enabled.Count == 0) return null;

        foreach (var binding in enabled)
        {
            // Trim difensivo: un TextBox puo' finire con spazi o caratteri invisibili copiati/
            // digitati per sbaglio (es. tastiera touch, autocorrezione) che l'utente non vede a
            // schermo ma fanno fallire il confronto esatto col regex sottostante. Bug reale
            // riscontrato: un utente vedeva "i" nel campo ma il valore salvato non era il singolo
            // carattere ASCII pulito, e il messaggio d'errore (solo il valore, non la riga) rendeva
            // impossibile capire quale delle ~20 voci correggere.
            var trimmedKey = binding.Key?.Trim() ?? string.Empty;
            if (!SafeKeyRegex.IsMatch(trimmedKey))
                throw new LaunchValidationException(
                    $"Tasto non valido per \"{binding.Description}\": \"{binding.Key}\". Vai su Tasti/Comandi e correggi quella riga (cancella il campo Tasto e riscrivilo).");
            ValidateCommand(binding.Command);
        }

        if (!Directory.Exists(targetModDirectory))
            throw new LaunchValidationException($"Cartella non trovata per scrivere il file dei tasti: {targetModDirectory}");

        CleanupLegacyFiles(targetModDirectory);

        var fullPath = Path.Combine(targetModDirectory, FixedFileName);
        var content = BuildCfgContent(enabled);
        File.WriteAllText(fullPath, content, Encoding.ASCII);

        return FixedFileName;
    }

    /// <summary>Pulizia una tantum dei file della vecchia generazione (nome con suffisso casuale,
    /// mai ripulito prima d'ora): rimuove qualunque "q3ilauncher_binds_XXXXXXXX.cfg" trovato,
    /// lasciando solo il nome fisso attuale. Best-effort: un file bloccato o permessi mancanti non
    /// blocca mai l'avvio della partita.</summary>
    private static void CleanupLegacyFiles(string directory)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "q3ilauncher_binds_*.cfg"))
            {
                try { File.Delete(file); } catch { /* in uso o permessi: si ignora */ }
            }
        }
        catch { /* enumerazione fallita: si ignora, non e' critico */ }
    }

    private static void ValidateCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || command.Length > 64)
            throw new LaunchValidationException($"Comando non valido: \"{command}\".");
        if (command.IndexOfAny(new[] { '"', ';', '\n', '\r', '`' }) >= 0)
            throw new LaunchValidationException($"Il comando \"{command}\" contiene caratteri non ammessi.");
    }

    private static string BuildCfgContent(IReadOnlyList<KeyBindingEntry> bindings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// File generato automaticamente da Quake3InstaGibLauncher - tasti/comandi.");
        sb.AppendLine("// Sicuro da cancellare: viene rigenerato ad ogni avvio con i tasti scelti.");
        sb.AppendLine();

        foreach (var binding in bindings)
            sb.AppendLine($"bind \"{binding.Key.Trim().ToUpperInvariant()}\" \"{binding.Command}\"");

        return sb.ToString();
    }
}
