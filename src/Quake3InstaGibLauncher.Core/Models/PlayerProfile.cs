namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>
/// Identita' del giocatore applicata al lancio del gioco (cvar "name", mirino, colori modello).
/// ColoredName puo' contenere i codici colore Quake III "^0".."^7" (nero, rosso, verde, giallo,
/// blu, ciano, magenta, bianco) per colorare porzioni diverse del nome.
/// </summary>
public sealed class PlayerProfile
{
    public string ColoredName { get; set; } = "^1Player";

    /// <summary>Stile del mirino (cvar cg_drawCrosshair): indice 1-N tra le forme incluse nel motore.</summary>
    public int CrosshairStyle { get; set; } = 1;
    public int CrosshairSize { get; set; } = 24;

    /// <summary>Colore del mirino (cvar cg_crosshairColor, estensione specifica di ioquake3: non
    /// garantito su ogni mod/fork, ma innocuo se ignorato).</summary>
    public string CrosshairColorHex { get; set; } = "#FFFFFF";
    public bool CrosshairHealthColor { get; set; }
    public bool CrosshairPulseOnHit { get; set; } = true;

    /// <summary>Colori modello/effetti (cvar color1/color2): indice 1-8 della palette Quake III.</summary>
    public int ModelColor1 { get; set; } = 4;
    public int ModelColor2 { get; set; } = 4;
}
