using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Quest.Models;

/// <summary>Impostazioni persistite dell'app Quest: per ora solo il profilo giocatore (nome,
/// mirino, colori), stesso oggetto condiviso Core usato da Windows/Mac.</summary>
public sealed class QuestAppSettings
{
    public PlayerProfile PlayerProfile { get; set; } = new();
}
