using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Quake3InstaGibLauncher.Core.Services;

/// <summary>
/// Avvia la ricerca amici e la chat di Steam usando gli schemi di URL "steam://" che il client
/// Steam stesso registra durante l'installazione (nessun trucco, sono comandi ufficiali del
/// client). L'app non si collega in alcun modo all'account Steam dell'utente: si limita ad
/// aprire finestre del client gia' installato, esattamente come cliccare i pulsanti equivalenti
/// dentro Steam.
/// </summary>
public static class SteamChatService
{
    /// <summary>SteamID64: sempre 17 cifre numeriche (inizia tipicamente con 7656119...).</summary>
    private static readonly Regex SteamId64Regex = new(@"^\d{17}$", RegexOptions.Compiled);

    /// <summary>Riconosce un SteamID64 dentro un link di profilo tipo
    /// steamcommunity.com/profiles/7656119xxxxxxxxxx (non funziona con i link "vanity"
    /// tipo /id/nomeutente: quelli richiedono una chiave Web API per essere risolti).</summary>
    private static readonly Regex ProfileUrlRegex = new(@"/profiles/(\d{17})", RegexOptions.Compiled);

    /// <summary>Apre la finestra "Aggiungi amico" di Steam, dove l'utente puo' cercare per nome
    /// direttamente dentro il client. E' il modo piu' semplice per trovare qualcuno senza dover
    /// conoscere gia' il suo SteamID.</summary>
    public static bool OpenAddFriendSearch()
    {
        return TryStart("steam://friends/add/");
    }

    /// <summary>Prova a estrarre un SteamID64 valido da testo libero: puo' essere il numero gia'
    /// incollato, oppure un link completo al profilo (es. copiato dalla pagina "Copia URL
    /// pagina" di Steam).</summary>
    public static bool TryExtractSteamId64(string? input, out string steamId64)
    {
        steamId64 = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var trimmed = input.Trim();
        if (SteamId64Regex.IsMatch(trimmed))
        {
            steamId64 = trimmed;
            return true;
        }

        var match = ProfileUrlRegex.Match(trimmed);
        if (match.Success)
        {
            steamId64 = match.Groups[1].Value;
            return true;
        }

        return false;
    }

    /// <summary>Apre direttamente una chat con l'amico indicato (SteamID64 o link profilo).
    /// Restituisce false (senza eccezioni) se il testo non e' un ID/link riconoscibile: i link
    /// "vanity" (steamcommunity.com/id/nome) non sono risolvibili senza una Web API key, quindi
    /// vanno convertiti a mano copiando l'URL numerico del profilo da Steam.</summary>
    public static bool TryOpenChat(string? friendInput)
    {
        if (!TryExtractSteamId64(friendInput, out var steamId64)) return false;
        return TryStart($"steam://friends/message/{steamId64}");
    }

    private static bool TryStart(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
            return true;
        }
        catch
        {
            // Steam non installato o schema "steam://" non registrato: non deve mai bloccare l'app.
            return false;
        }
    }
}
