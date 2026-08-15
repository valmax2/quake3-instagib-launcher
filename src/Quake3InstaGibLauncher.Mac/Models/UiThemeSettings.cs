namespace Quake3InstaGibLauncher.Mac.Models;

public enum ButtonStyleMode { Flat, Raised, Glow }
public enum LogoRotationAxis { Z, Y, X }
public enum LogoPosition { AboveTitle, BehindTitle, Hidden }
public enum ImageFitMode { Uniform, UniformToFill, Fill }

/// <summary>Preferenze di aspetto personalizzabili, equivalente macOS di quello Windows.</summary>
public sealed class UiThemeSettings
{
    public string AccentColorHex { get; set; } = "#FF5A1F";
    public string AccentBrightColorHex { get; set; } = "#FF8A3D";
    public string EnergyColorHex { get; set; } = "#E0202B";
    public string ButtonTextColorHex { get; set; } = "#FFFFFF";
    public string BackgroundColorHex { get; set; } = "#0A0B0D";
    public string PanelColorHex { get; set; } = "#1C2025";

    public ButtonStyleMode ButtonStyle { get; set; } = ButtonStyleMode.Flat;

    // Disattivata di default: il logo VStudio Apps incluso di default (vedi LogoImagePath sotto)
    // e' gia' una GIF che ruota per conto proprio. Tenere anche questa rotazione sommerebbe un
    // secondo movimento sopra quello gia' presente nella GIF (stessa scelta della versione
    // Windows). Resta comunque riattivabile in Personalizza per l'emblema vettoriale.
    public bool LogoRotationEnabled { get; set; } = false;
    public LogoRotationAxis LogoAxis { get; set; } = LogoRotationAxis.Z;
    public double LogoSize { get; set; } = 360;
    public LogoPosition LogoPos { get; set; } = LogoPosition.AboveTitle;
    public int LogoRotationSeconds { get; set; } = 90;

    /// <summary>Percorso di un'immagine importata da usare al posto dell'emblema vettoriale in
    /// Home. Null/vuoto = usa l'emblema vettoriale. Di default punta al logo animato VStudio Apps
    /// incluso nell'app (cartella Presets\Logos\, sempre presente accanto all'eseguibile
    /// pubblicato): decodificato fotogramma per fotogramma con SkiaSharp (vedi HomeView.axaml.cs),
    /// equivalente Avalonia del trucco BitmapDecoder.Frames usato su WPF (Avalonia non anima i GIF
    /// nativamente). Su un settings.json GIA' ESISTENTE questo default non ha effetto: la
    /// deserializzazione sovrascrive con cio' che era gia' salvato.</summary>
    public string? LogoImagePath { get; set; } = ResolveDefaultLogoPath();
    public bool LogoImageIsAnimatedGif { get; set; } = true;

    private static string? ResolveDefaultLogoPath()
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Presets", "Logos", "vstudio_apps_bronze_quake_logo_360.gif");
            return System.IO.File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Sfondo principale della finestra come immagine importata (facoltativo).</summary>
    public string? BackgroundImagePath { get; set; }
    public double BackgroundImageOpacity { get; set; } = 0.45;
    public ImageFitMode BackgroundImageFit { get; set; } = ImageFitMode.UniformToFill;

    /// <summary>Sfondo dedicato SOLO alla schermata Home (indipendente dallo sfondo generale):
    /// resta sempre lo stesso passando da una schermata all'altra.</summary>
    public string? HomeBackgroundImagePath { get; set; }
    public double HomeBackgroundImageOpacity { get; set; } = 1.0;
    public ImageFitMode HomeBackgroundImageFit { get; set; } = ImageFitMode.UniformToFill;

    /// <summary>Sfondo dei pulsanti (sia normali sia principali/CTA) come immagine importata.</summary>
    public string? ButtonBackgroundImagePath { get; set; }
    public double ButtonBackgroundImageOpacity { get; set; } = 0.55;
    public ImageFitMode ButtonBackgroundImageFit { get; set; } = ImageFitMode.UniformToFill;

    /// <summary>Cronologia delle immagini importate in passato (piu' recente per prima), per
    /// riselezionarle rapidamente dalla galleria di anteprime.</summary>
    public List<string> BackgroundImageHistory { get; set; } = new();
    public List<string> ButtonBackgroundImageHistory { get; set; } = new();
    public List<string> HomeBackgroundImageHistory { get; set; } = new();

    /// <summary>Risoluzione massima (lato piu' lungo, in pixel) a cui vengono ridotte le immagini
    /// importate: evita file enormi in memoria senza differenze visibili.</summary>
    public int ImportedImageMaxResolution { get; set; } = 1920;

    /// <summary>Suoni sci-fi sintetici (non originali di Quake III) al passaggio del mouse e al
    /// clic sui pulsanti. Vedi UiSoundService.</summary>
    public bool UiSoundsEnabled { get; set; } = true;
}
