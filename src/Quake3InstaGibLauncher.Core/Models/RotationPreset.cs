namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>Una rotazione di mappe salvata dall'utente (ordine incluso).</summary>
public sealed class RotationPreset
{
    public string Name { get; set; } = "Nuova rotazione";
    public List<string> MapTechnicalNames { get; set; } = new();
}
