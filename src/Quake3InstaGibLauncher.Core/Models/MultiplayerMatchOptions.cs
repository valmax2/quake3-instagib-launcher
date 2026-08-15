namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>
/// Opzioni scelte dall'utente per una partita multiplayer ospitata dal proprio PC
/// (server non dedicato: l'host gioca dalla stessa istanza).
/// </summary>
public sealed class MultiplayerMatchOptions
{
    public required string MapTechnicalName { get; init; }

    public List<string> RotationMaps { get; init; } = new();

    public required MapSource MapSource { get; init; }

    /// <summary>True se la mappa scelta vive nella cartella missionpack (richiede fs_basegame missionpack).</summary>
    public required bool RequiresMissionPackBaseGame { get; init; }

    public required string ServerName { get; init; }
    public required int Port { get; init; }
    public required int MaxClients { get; init; }

    /// <summary>Password facoltativa per l'accesso al server; null/vuota = nessuna password.</summary>
    public string? Password { get; init; }

    public required int FragLimit { get; init; }
    public required int TimeLimit { get; init; }
    public required int Fov { get; init; }
    public required GameType GameType { get; init; }

    public required bool BotsEnabled { get; init; }
    public required int BotMinPlayers { get; init; }

    /// <summary>True = server pensato per la LAN, false = accessibile anche da Internet.</summary>
    public required bool IsLan { get; init; }

    public required bool UseInstaGibMod { get; init; }
    public required bool SvPureOff { get; init; }

    /// <summary>Vedi LocalMatchOptions.FixAspectRatio.</summary>
    public bool FixAspectRatio { get; init; } = true;
    public int ScreenWidth { get; init; }
    public int ScreenHeight { get; init; }
    public bool Fullscreen { get; init; } = true;

    /// <summary>Vedi LocalMatchOptions.Player.</summary>
    public PlayerProfile? Player { get; init; }
}
