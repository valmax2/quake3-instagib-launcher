namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>Configurazione server multiplayer salvata dall'utente, richiamabile in seguito.</summary>
public sealed class ServerPreset
{
    public string Name { get; set; } = "Nuovo preset";
    public string ServerName { get; set; } = "InstaGib di casa";
    public int Port { get; set; } = 27960;
    public int MaxClients { get; set; } = 12;
    public GameType GameType { get; set; } = GameType.FreeForAll;
    public int FragLimit { get; set; } = 30;
    public int TimeLimit { get; set; } = 15;
    public int Fov { get; set; } = 110;
    public bool BotsEnabled { get; set; }
    public int BotMinPlayers { get; set; }
    public bool IsLan { get; set; } = true;
    public string MapTechnicalName { get; set; } = "q3dm17";
}
