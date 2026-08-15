using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Mac.Models;

/// <summary>
/// Impostazioni persistenti dell'app macOS, salvate in
/// ~/Library/Application Support/Quake3InstaGibLauncher/settings.json
/// Non contiene mai la CD key ne' altri dati sensibili.
/// </summary>
public sealed class MacAppSettings
{
    /// <summary>Percorso del binario eseguibile (dentro Contents/MacOS/ del bundle .app).
    /// Vuoto = non ancora individuato, l'app provera' il rilevamento automatico.</summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>Cartella radice dei dati di gioco (contiene baseq3, InstaGib129, missionpack).
    /// Vuoto = non ancora individuato, l'app provera' il rilevamento automatico.</summary>
    public string DataRootPath { get; set; } = string.Empty;

    public int Fov { get; set; } = 110;

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

    // --- Preferiti / cronologia / rotazioni salvate ---
    public List<string> FavoriteMaps { get; set; } = new();
    public List<string> RecentMaps { get; set; } = new();
    public List<RotationPreset> SavedRotations { get; set; } = new();

    /// <summary>Configurazioni server multiplayer salvate dall'utente (nome, porta, regole, ...).</summary>
    public List<ServerPreset> SavedServerPresets { get; set; } = new();

    /// <summary>Giocatori conosciuti (schermata Giocatori), confrontati per nome pulito con i
    /// giocatori trovati nel browser server multiplayer.</summary>
    public List<KnownPlayer> KnownPlayers { get; set; } = new();

    /// <summary>Tasti/comandi preimpostati e personalizzati (schermata Tasti). Null finche' non
    /// visitata la prima volta: KeyBindingsViewModel la popola con i preset di default.</summary>
    public List<KeyBindingEntry>? KeyBindings { get; set; }

    /// <summary>Identita' del giocatore (schermata Personaggio): nome colorato, mirino, colori modello.</summary>
    public PlayerProfile PlayerProfile { get; set; } = new();

    /// <summary>Lingua dell'interfaccia.</summary>
    public AppLanguage Language { get; set; } = AppLanguage.Italian;

    // --- Video/risoluzione: evita le barre nere laterali forzando r_customwidth/height reali ---
    public bool FixAspectRatio { get; set; } = true;
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public bool Fullscreen { get; set; } = true;

    public double WindowWidth { get; set; } = 1440;
    public double WindowHeight { get; set; } = 900;

    /// <summary>Preferenze di aspetto personalizzabili (colori, stile pulsanti, immagini tema).</summary>
    public UiThemeSettings Theme { get; set; } = new();

    // --- Chat vocale (Steam): l'app non si collega all'account, si limita ad aprire le
    // finestre "Cerca amico" e "Avvia chat" del client Steam gia' installato, vedi
    // Core.Services.SteamChatService. ---
    public string SteamLastFriendId { get; set; } = string.Empty;
}
