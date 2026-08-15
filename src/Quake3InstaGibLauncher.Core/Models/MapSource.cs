namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>Da quale directory/pk3 dell'installazione proviene la mappa.</summary>
public enum MapSource
{
    BaseQ3,
    MissionPack,
    InstaGib129,
    Custom,
}

public static class MapSourceExtensions
{
    public static string ToDisplayName(this MapSource source) => source switch
    {
        MapSource.BaseQ3 => "baseq3 (originale)",
        MapSource.MissionPack => "missionpack (Team Arena)",
        MapSource.InstaGib129 => "InstaGib129",
        MapSource.Custom => "personalizzata",
        _ => source.ToString(),
    };
}
