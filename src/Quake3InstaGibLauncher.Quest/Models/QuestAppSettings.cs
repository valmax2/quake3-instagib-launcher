using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;

namespace Quake3InstaGibLauncher.Quest.Models;

/// <summary>Ultima configurazione usata per una partita contro bot: riproposta di default al
/// prossimo avvio dell'app, cosi' l'utente non deve reimpostare tutto ogni volta.</summary>
public sealed class BotMatchSettings
{
    public string? MapTechnicalName { get; set; }
    public int BotCount { get; set; } = 6;
    public int BotSkill { get; set; } = 3;
    public GameType GameType { get; set; } = GameType.FreeForAll;
    public bool UseInstaGibMod { get; set; } = true;
    public int FragLimit { get; set; } = 30;
    public int TimeLimit { get; set; } = 15;
}

/// <summary>Impostazioni video/motore applicate ad ogni avvio partita (join, bot, ospita).
/// Null = non forzato, resta il valore gia' salvato nel q3config.cfg del motore.</summary>
public sealed class VideoSettings
{
    public int Fov { get; set; } = 100;

    /// <summary>Campionamento anti-aliasing (cvar r_ext_multisample): 0/2/4/8. Null = non forzato.</summary>
    public int? AntiAliasingSamples { get; set; }

    /// <summary>Luminosita'/gamma (cvar r_gamma): tipicamente 0.5-3.0, 1.0 = valore di fabbrica.
    /// Null = non forzato.</summary>
    public double? Gamma { get; set; }

    /// <summary>Densita' di rendering VR (supersampling), se supportata dal motore. Best-effort:
    /// non verificato su hardware reale quale sia il cvar esatto usato da questo fork; se il
    /// motore lo ignora non succede nulla di male. Null = non forzato.</summary>
    public double? VrPixelDensity { get; set; }

    /// <summary>Offset verticale applicato a visore/mani/arma (cvar vr_heightAdjust nel motore,
    /// vedi vr_input.c/vr_renderer.c) - corregge la sensazione di "personaggio troppo basso/alto"
    /// senza dover ricalibrare il Guardian. Unita' di misura: metri (stessa del motore VR). Valori
    /// positivi alzano il punto di vista, negativi lo abbassano. Null = non forzato (0.0 di
    /// fabbrica, il motore lo salva comunque da solo una volta cambiato in-game).</summary>
    public double? HeightAdjust { get; set; }
}

/// <summary>Impostazioni persistite dell'app Quest.</summary>
public sealed class QuestAppSettings
{
    public PlayerProfile PlayerProfile { get; set; } = new();
    public BotMatchSettings LastBotMatch { get; set; } = new();
    public VideoSettings Video { get; set; } = new();

    /// <summary>Nomi tecnici (es. "q3dm17") delle mappe segnate come preferite.</summary>
    public List<string> FavoriteMapTechnicalNames { get; set; } = new();

    /// <summary>Giocatori incontrati in partita, salvati manualmente per riconoscerli in futuro
    /// (nome pulito + valutazione) - stesso modello Core gia' usato dal launcher Windows/Mac.</summary>
    public List<KnownPlayer> KnownPlayers { get; set; } = new();

    /// <summary>Bind tasti/comandi console e messaggi chat rapidi, applicati al lancio del gioco
    /// (vedi QuestLaunchService). Popolato con i preset di default al primo avvio: stesso elenco
    /// gia' usato dal launcher Windows/Mac (Core.Services.KeyBindingDefaults), cosi' i comandi
    /// preimpostati restano identici su tutte le piattaforme.</summary>
    public List<KeyBindingEntry> KeyBindings { get; set; } = KeyBindingDefaults.BuildDefaultPresets();
}
