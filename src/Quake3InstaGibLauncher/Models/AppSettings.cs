using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Models;

/// <summary>
/// Impostazioni persistenti dell'applicazione, salvate in
/// %APPDATA%\Quake3InstaGibLauncher\settings.json
/// Non contiene mai la CD key ne' altri dati sensibili.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Cartella radice dell'installazione ioquake3 (default: C:\Giochi\ioquake3).</summary>
    public string GameRootPath { get; set; } = @"C:\Giochi\ioquake3";

    public int Fov { get; set; } = 110;

    /// <summary>Risoluzione/aspect ratio forzati al lancio del gioco (evita le barre nere laterali
    /// dovute a una vecchia modalita' video 4:3 salvata in q3config.cfg). ScreenWidth/Height=0 al
    /// primissimo avvio: MainViewModel li riempie allora con la risoluzione reale del desktop.</summary>
    public bool FixAspectRatio { get; set; } = true;
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public bool Fullscreen { get; set; } = true;

    // --- Ultimi valori usati nella partita locale ---
    public int LocalTotalPlayers { get; set; } = 6;
    public int LocalBotSkill { get; set; } = 4;
    public int LocalFragLimit { get; set; } = 30;
    public int LocalTimeLimit { get; set; } = 15;
    public GameType LocalGameType { get; set; } = GameType.FreeForAll;
    public bool LocalUseInstaGibMod { get; set; } = true;
    public bool LocalSvPureOff { get; set; } = true;
    public string LocalLastMap { get; set; } = "q3dm17";
    public List<string> LocalRotationMaps { get; set; } = new();

    // --- Ultimi valori usati in multiplayer ---
    public string ServerName { get; set; } = "InstaGib di casa";
    public int ServerPort { get; set; } = 27960;
    public int ServerMaxClients { get; set; } = 12;
    public bool ServerBotsEnabled { get; set; }
    public int ServerBotMinPlayers { get; set; }
    public int ServerFragLimit { get; set; } = 30;
    public int ServerTimeLimit { get; set; } = 15;
    public GameType ServerGameType { get; set; } = GameType.FreeForAll;
    public bool ServerIsLan { get; set; } = true;
    public string ServerLastMap { get; set; } = "q3dm17";
    public List<string> ServerRotationMaps { get; set; } = new();

    // --- Filtri del browser server multiplayer (ricordati tra una sessione e l'altra) ---
    public bool ServerFilterFfa { get; set; }
    public bool ServerFilterTeam { get; set; }
    public bool ServerFilterTournament { get; set; }
    public bool ServerFilterCtf { get; set; }
    public bool ServerFilterInstaGibOnly { get; set; }
    public bool ServerFilterHideFull { get; set; }
    public bool ServerFilterHideEmpty { get; set; }
    public bool ServerFilterLimitPing { get; set; }
    public int ServerMaxPingMs { get; set; } = 150;
    public bool ServerSortByPlayers { get; set; }
    public bool ServerFilterKnownPlayersOnly { get; set; }
    public bool ServerFilterHideBotOnly { get; set; }

    // --- Preferiti / cronologia / rotazioni salvate ---
    public List<string> FavoriteMaps { get; set; } = new();
    public List<string> RecentMaps { get; set; } = new();
    public List<RotationPreset> SavedRotations { get; set; } = new();

    /// <summary>Configurazioni server multiplayer salvate dall'utente (nome, porta, regole, ...).</summary>
    public List<ServerPreset> SavedServerPresets { get; set; } = new();

    /// <summary>Giocatori incontrati in partita e salvati dall'utente (valutazione, note), usati per
    /// riconoscerli ed eventualmente cercarli tra i server online (vedi MultiplayerSetupViewModel).</summary>
    public List<KnownPlayer> KnownPlayers { get; set; } = new();

    /// <summary>Tasti/comandi (schermata "Tasti"): null finche' non caricati la prima volta da
    /// KeyBindingsViewModel, che a quel punto li riempie con i preset predefiniti.</summary>
    public List<KeyBindingEntry>? KeyBindings { get; set; }

    /// <summary>Nome, colori e mirino del personaggio (schermata "Personaggio").</summary>
    public PlayerProfile PlayerProfile { get; set; } = new();

    // --- Finestra ---
    public double WindowWidth { get; set; } = 1440;
    public double WindowHeight { get; set; } = 900;
    public bool WindowMaximized { get; set; }

    /// <summary>Preferenze di aspetto personalizzabili (colori, stile pulsanti, logo).</summary>
    public UiThemeSettings Theme { get; set; } = new();

    /// <summary>Lingua dell'interfaccia (vedi LocalizationService).</summary>
    public AppLanguage Language { get; set; } = AppLanguage.Italian;

    // --- Chat vocale (Steam): l'app non si collega all'account, si limita ad aprire le
    // finestre "Cerca amico" e "Avvia chat" del client Steam gia' installato, vedi
    // Core.Services.SteamChatService. ---
    public string SteamLastFriendId { get; set; } = string.Empty;
}
