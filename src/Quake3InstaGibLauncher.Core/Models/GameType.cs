namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>
/// Modalita' di gioco Quake III supportate dal launcher.
/// Il valore numerico corrisponde esattamente al cvar "g_gametype" del motore ioquake3.
/// </summary>
public enum GameType
{
    FreeForAll = 0,
    Tournament = 1,
    TeamDeathmatch = 3,
    CaptureTheFlag = 4,
}

public static class GameTypeExtensions
{
    /// <summary>Nome leggibile in italiano per l'interfaccia.</summary>
    public static string ToDisplayName(this GameType type) => type switch
    {
        GameType.FreeForAll => "Deathmatch (FFA)",
        GameType.Tournament => "Torneo (1 contro 1)",
        GameType.TeamDeathmatch => "Team Deathmatch",
        GameType.CaptureTheFlag => "Cattura la Bandiera (CTF)",
        _ => type.ToString(),
    };

    /// <summary>
    /// Converte le stringhe usate nei file .arena / arenas.txt ("ffa", "team", "tourney", "ctf", "single")
    /// nell'insieme di modalita' supportate da quella mappa.
    /// </summary>
    public static IReadOnlyList<GameType> ParseArenaTypeField(string? arenaTypeField)
    {
        if (string.IsNullOrWhiteSpace(arenaTypeField))
        {
            // Nessun dato: assumiamo compatibilita' generica FFA (vale per la stragrande
            // maggioranza delle mappe deathmatch prive di metadati, es. le mappe q3wpak).
            return new[] { GameType.FreeForAll };
        }

        var tokens = arenaTypeField
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new List<GameType>();
        foreach (var token in tokens)
        {
            switch (token.ToLowerInvariant())
            {
                case "ffa":
                case "single": // le mappe "single" (single player) sono comunque giocabili in FFA/bot match
                    if (!result.Contains(GameType.FreeForAll)) result.Add(GameType.FreeForAll);
                    break;
                case "tourney":
                case "duel":
                    if (!result.Contains(GameType.Tournament)) result.Add(GameType.Tournament);
                    break;
                case "team":
                    if (!result.Contains(GameType.TeamDeathmatch)) result.Add(GameType.TeamDeathmatch);
                    break;
                case "ctf":
                    if (!result.Contains(GameType.CaptureTheFlag)) result.Add(GameType.CaptureTheFlag);
                    break;
            }
        }

        return result.Count > 0 ? result : new[] { GameType.FreeForAll };
    }
}
