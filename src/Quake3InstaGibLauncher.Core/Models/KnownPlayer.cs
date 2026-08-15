namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>Valutazione personale assegnata a un giocatore incontrato in partita.</summary>
public enum PlayerRating
{
    Neutrale,
    Forte,
    DaEvitare,
    Amico,
}

/// <summary>
/// Un giocatore avversario/alleato incontrato in partita, salvato dall'utente per riconoscerlo in
/// futuro. Il confronto con i nomi reali dei server (vedi Quake3ServerBrowser.GetPlayerNamesAsync)
/// avviene sul nome "pulito" (senza codici colore Quake III "^N"), case-insensitive: i nomi dei
/// giocatori spesso includono colori diversi da una partita all'altra, ma il nome pulito resta lo
/// stesso identificatore stabile che si vede a schermo.
/// </summary>
public sealed class KnownPlayer
{
    /// <summary>Nome pulito (senza codici colore) usato per il confronto e come identificatore univoco.</summary>
    public required string Name { get; set; }

    public PlayerRating Rating { get; set; } = PlayerRating.Neutrale;
    public string Notes { get; set; } = "";
    public int TimesSeen { get; set; } = 1;
    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
}
