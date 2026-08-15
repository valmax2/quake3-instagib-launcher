using System.Text;
using System.Text.RegularExpressions;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Core.Services;

/// <summary>
/// Costruisce dinamicamente l'elenco di argomenti da passare a ioquake3.x86_64.exe in base
/// alle opzioni scelte dall'utente, replicando i cvar usati dai comandi di riferimento:
///
/// Locale:
///   +set fs_game InstaGib129 +set sv_pure 0 +set dedicated 0 +set g_gametype 0
///   +set fraglimit 30 +set timelimit 15 +set bot_minplayers 6 +set cg_fov 110 +map q3dm17
///
/// Multiplayer:
///   +set fs_game InstaGib129 +set sv_pure 0 +set dedicated 0 +set net_port 27960
///   +set sv_hostname "InstaGib di casa" +set sv_maxclients 12 +set bot_minplayers 0
///   +set g_gametype 0 +set fraglimit 30 +set timelimit 15 +set cg_fov 110 +map q3dm17
///
/// Ogni argomento e' un elemento separato di una lista (usata poi con ProcessStartInfo.ArgumentList),
/// mai una stringa concatenata: questo evita problemi di quoting con percorsi/nomi contenenti spazi
/// e impedisce l'injection di comandi extra nella console di Quake III.
/// </summary>
public static class CommandBuilder
{
    private static readonly Regex SafeMapNameRegex = new(@"^[A-Za-z0-9_\-]{1,64}$", RegexOptions.Compiled);

    public static List<string> BuildLocalArguments(LocalMatchOptions options, string? rotationCfgFileName)
    {
        ValidateMapName(options.MapTechnicalName);
        foreach (var m in options.RotationMaps) ValidateMapName(m);

        var args = new List<string>();

        if (options.UseInstaGibMod)
            AddSet(args, "fs_game", "InstaGib129");

        if (options.RequiresMissionPackBaseGame)
            AddSet(args, "fs_basegame", "missionpack");

        AddSet(args, "sv_pure", options.SvPureOff ? "0" : "1");
        AddSet(args, "dedicated", "0");
        AddSet(args, "g_gametype", ((int)options.GameType).ToString());
        AddSet(args, "fraglimit", ClampNonNegative(options.FragLimit).ToString());
        AddSet(args, "timelimit", ClampNonNegative(options.TimeLimit).ToString());

        if (options.GameType == GameType.CaptureTheFlag)
            AddSet(args, "capturelimit", Math.Max(1, options.FragLimit / 3).ToString());

        AddSet(args, "bot_minplayers", Math.Clamp(options.TotalPlayers, 1, 32).ToString());
        AddSet(args, "g_spSkill", Math.Clamp(options.BotSkill, 1, 5).ToString());
        AddSet(args, "cg_fov", Math.Clamp(options.Fov, 90, 160).ToString());
        AddVideoCvars(args, options.FixAspectRatio, options.ScreenWidth, options.ScreenHeight, options.Fullscreen);
        AddPlayerProfileCvars(args, options.Player);
        AddStabilityCvars(args);

        AppendMapOrRotation(args, options.MapTechnicalName, rotationCfgFileName);
        return args;
    }

    public static List<string> BuildMultiplayerArguments(MultiplayerMatchOptions options, string? rotationCfgFileName)
    {
        ValidateMapName(options.MapTechnicalName);
        foreach (var m in options.RotationMaps) ValidateMapName(m);
        ValidatePort(options.Port);
        var serverName = ValidateFreeText(options.ServerName, nameof(options.ServerName));
        var password = string.IsNullOrEmpty(options.Password) ? null : ValidateFreeText(options.Password, nameof(options.Password));

        var args = new List<string>();

        if (options.UseInstaGibMod)
            AddSet(args, "fs_game", "InstaGib129");

        if (options.RequiresMissionPackBaseGame)
            AddSet(args, "fs_basegame", "missionpack");

        AddSet(args, "sv_pure", options.SvPureOff ? "0" : "1");
        AddSet(args, "dedicated", "0");
        AddSet(args, "net_port", options.Port.ToString());
        AddSet(args, "sv_hostname", serverName);
        AddSet(args, "sv_maxclients", Math.Clamp(options.MaxClients, 2, 32).ToString());

        // Nessun limite di banda imposto ai client che si connettono a NOI: se sv_maxRate resta
        // al valore storico salvato in q3config.cfg, chiunque si unisce alla nostra partita
        // avrebbe gli stessi scatti che AddNetworkCvars risolve lato client (vedi sotto), anche
        // se noi stessi giochiamo perfettamente.
        AddSet(args, "sv_maxRate", "0");

        if (password is not null)
            AddSet(args, "g_password", password);

        AddSet(args, "bot_minplayers", options.BotsEnabled ? Math.Clamp(options.BotMinPlayers, 0, 32).ToString() : "0");
        AddSet(args, "g_gametype", ((int)options.GameType).ToString());
        AddSet(args, "fraglimit", ClampNonNegative(options.FragLimit).ToString());
        AddSet(args, "timelimit", ClampNonNegative(options.TimeLimit).ToString());

        if (options.GameType == GameType.CaptureTheFlag)
            AddSet(args, "capturelimit", Math.Max(1, options.FragLimit / 3).ToString());

        AddSet(args, "cg_fov", Math.Clamp(options.Fov, 90, 160).ToString());
        AddVideoCvars(args, options.FixAspectRatio, options.ScreenWidth, options.ScreenHeight, options.Fullscreen);
        AddPlayerProfileCvars(args, options.Player);
        AddNetworkCvars(args);
        AddStabilityCvars(args);

        // sv_lanForceRate: cvar reale ioquake3/Q3 che forza il rate massimo per i client sulla
        // stessa LAN. Lo impostiamo esplicitamente in base alla scelta LAN/Internet dell'utente
        // solo a scopo di ottimizzazione locale: non modifica ne' firewall ne' router.
        AddSet(args, "sv_lanForceRate", options.IsLan ? "1" : "0");

        AppendMapOrRotation(args, options.MapTechnicalName, rotationCfgFileName);
        return args;
    }

    // Nome di una cartella mod dichiarata da un server remoto (campo "game" della risposta
    // getinfo): dato di rete non fidato, va validato prima di usarlo in un cvar.
    private static readonly Regex SafeModFolderRegex = new(@"^[A-Za-z0-9_\-]{1,32}$", RegexOptions.Compiled);

    /// <summary>
    /// Costruisce gli argomenti per UNIRSI a un server esistente trovato dal browser server
    /// (LAN o Internet), invece di ospitarne uno. Usa il comando "+connect indirizzo:porta".
    ///
    /// IMPORTANTE: a differenza di Locale/Ospita (dove SIAMO NOI a decidere quale mod far
    /// girare), qui NON forziamo piu' "fs_game InstaGib129" in base alla preferenza generica
    /// dell'utente: il motore Quake III negozia da solo con il server la mod corretta durante la
    /// connessione (comportamento standard del client, identico a quello del menu Multiplayer
    /// integrato nel gioco). Prima invece impostavamo sempre fs_game=InstaGib129 se la casella
    /// "Usa mod InstaGib129" era spuntata, il che rompeva silenziosamente la connessione (o dava
    /// un'esperienza incoerente) verso QUALSIASI server che non fosse InstaGib129 — nella pratica
    /// la maggioranza dei server reali trovati dal browser (altre mod come osp/cpma/defrag/ecc.,
    /// o server FFA/CTF "vanilla" senza mod). Il parametro "modFolderHint" (facoltativo, tipicamente
    /// ServerInfo.ModName) resta solo come indicazione per capire quale sottocartella locale
    /// usare per il file dei tasti (vedi sotto), NON per forzare fs_game.
    /// </summary>
    public static List<string> BuildJoinArguments(
        string address, int port, string? modFolderHint, string? password, int fov,
        bool fixAspectRatio = true, int screenWidth = 0, int screenHeight = 0, bool fullscreen = true,
        PlayerProfile? player = null)
    {
        var validAddress = ValidateAddress(address);
        ValidatePort(port);

        var args = new List<string>();

        AddSet(args, "cg_fov", Math.Clamp(fov, 90, 160).ToString());
        AddVideoCvars(args, fixAspectRatio, screenWidth, screenHeight, fullscreen);
        AddPlayerProfileCvars(args, player);
        AddNetworkCvars(args);
        AddStabilityCvars(args);

        if (!string.IsNullOrEmpty(password))
            AddSet(args, "password", ValidateFreeText(password, "password"));

        args.Add("+connect");
        args.Add($"{validAddress}:{port}");

        return args;
    }

    /// <summary>
    /// Causa REALE piu' comune degli "scatti"/rubber-banding online in Quake III che NON dipende
    /// dal ping: il client chiede al server pochissimi dati al secondo perche' "rate" e' rimasto
    /// al valore storico salvato in q3config.cfg (l'era del modem 56k: rate 3000-5000, snaps 20,
    /// cl_maxpackets 30). Con quei valori il movimento degli altri giocatori appare a scatti anche
    /// con connessione e ping perfetti, perche' il server invia meno aggiornamenti al secondo di
    /// quanti servirebbero. Qui il launcher non aveva MAI impostato questi cvar: li forziamo a
    /// valori moderni sicuri ad ogni avvio (funzionano bene su qualunque connessione a banda larga
    /// odierna). Se il server ha un limite piu' basso (sv_maxRate/sv_fps), lo applica comunque lui
    /// stesso: chiedere un valore alto qui non puo' mai "rompere" nulla, nel peggiore dei casi il
    /// server clampa la richiesta.
    /// </summary>
    private static void AddNetworkCvars(List<string> args)
    {
        AddSet(args, "rate", "25000");
        AddSet(args, "snaps", "40");
        AddSet(args, "cl_maxpackets", "100");
        AddSet(args, "cl_packetdup", "1");

        // cl_allowDownload: se il server ospita una mappa/pk3 che non abbiamo in locale, il motore
        // prova a scaricarla automaticamente da solo (esattamente il meccanismo dietro le schermate
        // "Downloading: .../*.pk3" che si vedono connettendosi a server con contenuti custom). Il
        // launcher non l'aveva mai impostato esplicitamente: qui forziamo "1" (abilitato) cosi' il
        // download parte sempre, invece di dipendere dal valore storico salvato in q3config.cfg.
        // Non e' pero' una garanzia di successo: se il server stesso ha una configurazione di
        // download rotta (redirect HTTP non funzionante, file mancante sul suo host), il download
        // puo' comunque restare bloccato o fallire — quel caso e' un problema del server, non
        // risolvibile lato client.
        AddSet(args, "cl_allowDownload", "1");
    }

    /// <summary>
    /// Causa REALE del crash "HUNK_ALLOC FAILED ON &lt;numero&gt;" osservato durante partite vere
    /// (sia locali con rotazioni di mappe pesanti, sia online su server con mappe custom dai
    /// texture pack grandi): la memoria "hunk" che il motore riserva all'avvio per caricare mappe,
    /// modelli e texture e' un blocco FISSO deciso da com_hunkmegs, mai impostato esplicitamente da
    /// questo launcher finora (quindi lasciato al default storico del motore, insufficiente per
    /// molte mappe custom moderne dai contenuti piu' pesanti delle mappe originali del 1999). Con
    /// hardware odierno riservarne di piu' non costa nulla in pratica: qui lo alziamo a un valore
    /// generoso ad ogni avvio, eliminando la causa piu' comune di questo crash specifico. Se il
    /// crash si ripresenta con questo valore, la mappa in questione richiede davvero piu' memoria di
    /// quanta un normale PC da gioco ne abbia (caso raro).
    /// </summary>
    private static void AddStabilityCvars(List<string> args)
    {
        AddSet(args, "com_hunkmegs", "1024");
    }

    /// <summary>True se modFolderHint sembra proprio un server InstaGib129 (usato solo per
    /// decidere in quale cartella locale scrivere il file dei tasti/comandi, vedi
    /// KeyBindingCfgService: se il server gira una mod che non abbiamo installato in locale, il
    /// file va scritto comunque in baseq3, che il motore consulta sempre come fallback finale
    /// indipendentemente dalla mod attiva in quella sessione).</summary>
    public static bool LooksLikeInstaGibModFolder(string? modFolderHint) =>
        !string.IsNullOrWhiteSpace(modFolderHint) &&
        SafeModFolderRegex.IsMatch(modFolderHint) &&
        modFolderHint.Contains("instagib", StringComparison.OrdinalIgnoreCase);

    private static string ValidateAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Length > 255)
            throw new LaunchValidationException($"Indirizzo server non valido: \"{address}\".");

        // Ammette IPv4, IPv6 (tra parentesi quadre) e nomi host: lettere, numeri, punti, due
        // punti, trattini e parentesi quadre. Niente spazi, virgolette o punto e virgola.
        if (address.IndexOfAny(new[] { ' ', '"', ';', '\n', '\r', '`' }) >= 0)
            throw new LaunchValidationException($"Indirizzo server non valido: \"{address}\".");

        return address;
    }

    private static void AppendMapOrRotation(List<string> args, string firstMap, string? rotationCfgFileName)
    {
        if (!string.IsNullOrEmpty(rotationCfgFileName))
        {
            args.Add("+exec");
            args.Add(rotationCfgFileName);
        }
        else
        {
            args.Add("+map");
            args.Add(firstMap);
        }
    }

    /// <summary>
    /// Forza una risoluzione/aspect ratio corretti invece di lasciare che il motore usi la
    /// modalita' video salvata in precedenza in q3config.cfg (spesso una voce 4:3 storica, che
    /// causa le classiche barre nere laterali "pillarbox" su schermi widescreen).
    ///
    /// "r_mode -2" e' un'estensione specifica di ioquake3 (non presente nel Quake III originale
    /// del 2000): dice al motore di interrogare DIRETTAMENTE SDL2 per la risoluzione nativa
    /// reale del desktop corrente e usare sempre quella, invece di un valore che il launcher
    /// avrebbe dovuto indovinare in anticipo (con "r_mode -1" + r_customwidth/r_customheight
    /// fissi, usato in precedenza qui). E' la scelta piu' robusta: si adatta da solo anche se
    /// l'utente cambia monitor, risoluzione del desktop o scaling DPI tra un lancio e l'altro, e
    /// non sovrascrive piu' silenziosamente un cambio di risoluzione fatto manualmente dall'utente
    /// nel menu video di Quake III con un valore rilevato male dal launcher a un lancio precedente.
    /// r_customwidth/r_customheight vengono comunque impostati come dato di riserva (innocuo: con
    /// r_mode -2 il motore li ignora ai fini della risoluzione, ma alcuni calcoli di scala HUD
    /// storicamente li leggono comunque).
    /// </summary>
    private static void AddVideoCvars(List<string> args, bool fixAspectRatio, int width, int height, bool fullscreen)
    {
        if (!fixAspectRatio)
            return;

        AddSet(args, "r_mode", "-2");
        if (width > 0 && height > 0)
        {
            AddSet(args, "r_customwidth", width.ToString());
            AddSet(args, "r_customheight", height.ToString());
        }
        AddSet(args, "r_fullscreen", fullscreen ? "1" : "0");
    }

    /// <summary>
    /// Imposta nome (con eventuali codici colore "^N"), mirino e colori modello del giocatore.
    /// "name" e cg_drawCrosshair/cg_crosshairSize sono cvar standard di Quake III/ioquake3.
    /// cg_crosshairColor e' un'estensione specifica di ioquake3 (non garantita su ogni mod/fork):
    /// se il motore/mod la ignora, semplicemente non ha effetto, nessun errore.
    /// </summary>
    private static void AddPlayerProfileCvars(List<string> args, PlayerProfile? player)
    {
        if (player is null) return;

        if (!string.IsNullOrWhiteSpace(player.ColoredName))
            AddSet(args, "name", ValidateFreeText(player.ColoredName, "nome giocatore"));

        AddSet(args, "cg_drawCrosshair", Math.Clamp(player.CrosshairStyle, 1, 30).ToString());
        AddSet(args, "cg_crosshairSize", Math.Clamp(player.CrosshairSize, 4, 96).ToString());
        AddSet(args, "cg_crosshairHealth", player.CrosshairHealthColor ? "1" : "0");
        AddSet(args, "cg_crosshairPulse", player.CrosshairPulseOnHit ? "1" : "0");

        var (r, g, b) = ParseRgbOrWhite(player.CrosshairColorHex);
        AddSet(args, "cg_crosshairColor", $"{r} {g} {b}");

        AddSet(args, "color1", Math.Clamp(player.ModelColor1, 1, 8).ToString());
        AddSet(args, "color2", Math.Clamp(player.ModelColor2, 1, 8).ToString());
    }

    private static (int R, int G, int B) ParseRgbOrWhite(string hex)
    {
        try
        {
            if (hex.StartsWith('#') && hex.Length == 7)
            {
                var r = Convert.ToInt32(hex.Substring(1, 2), 16);
                var g = Convert.ToInt32(hex.Substring(3, 2), 16);
                var b = Convert.ToInt32(hex.Substring(5, 2), 16);
                return (r, g, b);
            }
        }
        catch { /* formato non valido: si ignora, si usa il bianco */ }
        return (255, 255, 255);
    }

    private static void AddSet(List<string> args, string cvar, string value)
    {
        args.Add("+set");
        args.Add(cvar);
        args.Add(value);
    }

    private static int ClampNonNegative(int value) => Math.Max(0, value);

    /// <summary>Rappresentazione testuale del comando SOLO per la visualizzazione nella sezione avanzata
    /// (copiabile dall'utente). Non viene mai usata per avviare realmente il processo.</summary>
    public static string ToDisplayCommand(string exePath, IReadOnlyList<string> args)
    {
        var sb = new StringBuilder();
        sb.Append('"').Append(exePath).Append('"');

        foreach (var arg in args)
        {
            sb.Append(' ');
            if (arg.Contains(' ') || arg.Length == 0)
                sb.Append('"').Append(arg).Append('"');
            else
                sb.Append(arg);
        }

        return sb.ToString();
    }

    public static void ValidateMapName(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName) || !SafeMapNameRegex.IsMatch(mapName))
            throw new LaunchValidationException(
                $"Nome mappa non valido: \"{mapName}\". Sono ammessi solo lettere, numeri, trattini e underscore.");
    }

    public static void ValidatePort(int port)
    {
        if (port is < 1 or > 65535)
            throw new LaunchValidationException($"Porta UDP non valida: {port}. Deve essere compresa tra 1 e 65535.");
    }

    /// <summary>
    /// Valida un campo testuale libero (nome server, password) impedendo caratteri che potrebbero
    /// interrompere il parser di comandi della console Quake III (virgolette, punto e virgola,
    /// newline) e quindi iniettare cvar/comandi non voluti.
    /// </summary>
    private static string ValidateFreeText(string value, string fieldName)
    {
        if (value.Length > 64)
            throw new LaunchValidationException($"Il campo {fieldName} e' troppo lungo (massimo 64 caratteri).");

        if (value.IndexOfAny(new[] { '"', ';', '\n', '\r', '`' }) >= 0)
            throw new LaunchValidationException(
                $"Il campo {fieldName} contiene caratteri non ammessi (virgolette, punto e virgola o a capo).");

        return value;
    }
}
