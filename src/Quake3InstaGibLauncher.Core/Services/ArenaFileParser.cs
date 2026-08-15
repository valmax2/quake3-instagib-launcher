using System.IO;
using System.Text.RegularExpressions;

namespace Quake3InstaGibLauncher.Core.Services;

/// <summary>Una singola voce estratta da scripts/arenas.txt o da uno scripts/*.arena.</summary>
public sealed record ArenaEntry(
    string Map,
    string? LongName,
    string? TypeField,
    string? Bots,
    int? FragLimit,
    int? TimeLimit);

/// <summary>
/// Parser del formato testuale usato da Quake III per descrivere le arene, es.:
/// <code>
/// {
/// map          "q3dm17"
/// bots         "major sorlag doom"
/// longname     "The Longest Yard"
/// fraglimit    20
/// type         "single ffa"
/// }
/// </code>
/// Un singolo file puo' contenere piu' blocchi (es. scripts/arenas.txt, scripts/missionpack.arena,
/// scripts/promaps.arena), ognuno racchiuso tra parentesi graffe non annidate.
/// </summary>
public static class ArenaFileParser
{
    private static readonly Regex BlockRegex = new(@"\{(?<body>[^{}]*)\}", RegexOptions.Singleline | RegexOptions.Compiled);

    public static IReadOnlyList<ArenaEntry> Parse(string content)
    {
        var results = new List<ArenaEntry>();

        foreach (Match blockMatch in BlockRegex.Matches(content))
        {
            var fields = ParseFields(blockMatch.Groups["body"].Value);

            if (!fields.TryGetValue("map", out var map) || string.IsNullOrWhiteSpace(map))
                continue; // blocco senza campo "map" non e' un'arena valida, viene ignorato

            fields.TryGetValue("longname", out var longName);
            fields.TryGetValue("type", out var type);
            fields.TryGetValue("bots", out var bots);

            int? fragLimit = fields.TryGetValue("fraglimit", out var fl) && int.TryParse(fl, out var flVal) ? flVal : null;
            int? timeLimit = fields.TryGetValue("timelimit", out var tl) && int.TryParse(tl, out var tlVal) ? tlVal : null;

            results.Add(new ArenaEntry(map.Trim(), longName, type, bots, fragLimit, timeLimit));
        }

        return results;
    }

    private static Dictionary<string, string> ParseFields(string blockBody)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StringReader(blockBody);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//"))
                continue;

            // Chiave = prima sequenza di caratteri non-spazio; valore = resto della riga,
            // con eventuali virgolette esterne rimosse.
            var separatorIndex = IndexOfWhitespace(trimmed);
            if (separatorIndex < 0)
                continue;

            var key = trimmed[..separatorIndex];
            var rawValue = trimmed[separatorIndex..].Trim();

            if (rawValue.StartsWith('"'))
            {
                var closingQuote = rawValue.IndexOf('"', 1);
                rawValue = closingQuote > 0 ? rawValue[1..closingQuote] : rawValue.Trim('"');
            }

            fields[key] = rawValue;
        }

        return fields;
    }

    private static int IndexOfWhitespace(string s)
    {
        for (var i = 0; i < s.Length; i++)
            if (char.IsWhiteSpace(s[i]))
                return i;
        return -1;
    }
}
