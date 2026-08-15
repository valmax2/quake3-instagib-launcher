using System.IO;

namespace Quake3InstaGibLauncher.Models;

/// <summary>Percorsi risolti dell'installazione ioquake3 su Windows, calcolati a partire dalla cartella radice.</summary>
public sealed class InstallationPaths
{
    public required string RootPath { get; init; }

    public string ExecutablePath => Path.Combine(RootPath, "ioquake3.x86_64.exe");
    public string DedicatedExecutablePath => Path.Combine(RootPath, "ioq3ded.x86_64.exe");
    public string BaseQ3Path => Path.Combine(RootPath, "baseq3");
    public string BaseQ3Pak0Path => Path.Combine(BaseQ3Path, "pak0.pk3");
    public string InstaGibPath => Path.Combine(RootPath, "InstaGib129");
    public string InstaGibPk3Path => Path.Combine(InstaGibPath, "InstaGib129.pk3");
    public string MissionPackPath => Path.Combine(RootPath, "missionpack");
}
