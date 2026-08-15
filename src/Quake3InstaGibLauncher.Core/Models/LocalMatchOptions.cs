namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>
/// Opzioni scelte dall'utente per una partita locale contro bot.
/// Oggetto immutabile passato dal ViewModel ai servizi (CommandBuilder, RotationCfgService).
/// </summary>
public sealed class LocalMatchOptions
{
    public required string MapTechnicalName { get; init; }

    /// <summary>Mappe aggiuntive in rotazione dopo la prima (puo' essere vuota).</summary>
    public List<string> RotationMaps { get; init; } = new();

    public required MapSource MapSource { get; init; }

    /// <summary>True se la mappa scelta vive nella cartella missionpack (richiede fs_basegame missionpack).</summary>
    public required bool RequiresMissionPackBaseGame { get; init; }

    /// <summary>Numero totale di giocatori nella partita, bot inclusi (il giocatore umano conta 1).</summary>
    public required int TotalPlayers { get; init; }

    public int BotCount => Math.Max(0, TotalPlayers - 1);

    /// <summary>Difficolta' bot da 1 (facile) a 5 (incubo), mappata sul cvar g_spSkill.</summary>
    public required int BotSkill { get; init; }

    public required int FragLimit { get; init; }
    public required int TimeLimit { get; init; }
    public required int Fov { get; init; }
    public required GameType GameType { get; init; }

    /// <summary>Se true aggiunge "+set fs_game InstaGib129". Sempre true per impostazione predefinita.</summary>
    public required bool UseInstaGibMod { get; init; }

    public required bool SvPureOff { get; init; }

    /// <summary>Se true, la riga di comando forza risoluzione/aspect ratio corretti (evita le
    /// barre nere laterali "pillarbox" tipiche dei motori idTech3 quando il q3config.cfg salvato
    /// ha una modalita' video 4:3). Vedi CommandBuilder.AddVideoCvars.</summary>
    public bool FixAspectRatio { get; init; } = true;
    public int ScreenWidth { get; init; }
    public int ScreenHeight { get; init; }
    public bool Fullscreen { get; init; } = true;

    /// <summary>Nome/mirino/colori del giocatore (schermata "Personaggio"). Null = non forzati,
    /// il gioco usa i valori gia' salvati nel suo q3config.cfg.</summary>
    public PlayerProfile? Player { get; init; }
}
