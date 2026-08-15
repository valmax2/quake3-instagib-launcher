namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>Informazioni su un server Quake III individuato via LAN o Internet (master server).</summary>
public sealed class ServerInfo
{
    public required string Address { get; init; }
    public required int Port { get; init; }
    public string HostName { get; set; } = "(sconosciuto)";
    public string MapName { get; set; } = "";
    public int Players { get; set; }
    public int MaxPlayers { get; set; }
    public string GameTypeRaw { get; set; } = "";
    public string ModName { get; set; } = "";
    public bool NeedsPassword { get; set; }
    public long? PingMs { get; set; }
    public bool IsLan { get; set; }

    public string EndPointText => $"{Address}:{Port}";

    /// <summary>Modalita' di gioco dedotta dal campo "gametype" della risposta del server
    /// (stesso valore numerico del cvar g_gametype: 0=FFA,1=Torneo,3=Team,4=CTF). Null se il
    /// server risponde con un valore non riconosciuto (es. mod totalmente diversa).</summary>
    public GameType? ParsedGameType => int.TryParse(GameTypeRaw, out var value) && Enum.IsDefined(typeof(GameType), value)
        ? (GameType)value
        : null;

    /// <summary>True se il nome della mod suggerisce InstaGib (qualunque variante: InstaGib129,
    /// instagib, ecc.). Usato dal filtro "solo InstaGib" nel browser server.</summary>
    public bool IsLikelyInstaGib => ModName.Contains("instagib", StringComparison.OrdinalIgnoreCase);

    public bool IsFull => MaxPlayers > 0 && Players >= MaxPlayers;
    public bool IsEmpty => Players == 0;

    /// <summary>Nomi (con eventuali codici colore Quake III) dei giocatori attualmente in partita su
    /// questo server, ottenuti con una richiesta "getstatus" separata (piu' pesante di "getinfo": va
    /// fatta solo su richiesta, non per ogni server trovato). Vuota finche' non richiesta.</summary>
    public List<string> PlayerNames { get; set; } = new();

    /// <summary>Sottoinsieme di PlayerNames (nome pulito) che corrisponde a un giocatore salvato
    /// nella lista "Giocatori conosciuti" dell'utente. Popolato dalla ViewModel dopo aver incrociato
    /// PlayerNames con le preferenze salvate.</summary>
    public List<string> MatchedKnownPlayers { get; set; } = new();

    public bool HasKnownPlayers => MatchedKnownPlayers.Count > 0;
    public bool PlayerListLoaded { get; set; }

    /// <summary>Conteggio giocatori umani/bot, dedotto dal ping riportato da "getstatus": i bot
    /// rispondono sempre con ping 0 (nessuna vera connessione di rete), i giocatori umani hanno
    /// quasi sempre un ping maggiore di 0 (e' l'euristica standard usata da ogni browser server
    /// Quake III, incluso quello integrato nel gioco). Valorizzati solo dopo "Carica giocatori"
    /// (vedi PlayerListLoaded): finche' non e' true, entrambi restano a 0.</summary>
    public int HumanPlayerCount { get; set; }
    public int BotPlayerCount { get; set; }

    /// <summary>True se il server ha giocatori ma nessuno di loro e' umano: il caso segnalato
    /// spesso come "server pieni ma vuoti", che il filtro "Nascondi server con soli bot" usa per
    /// escluderli dall'elenco.</summary>
    public bool IsBotOnly => PlayerListLoaded && BotPlayerCount > 0 && HumanPlayerCount == 0;
}
