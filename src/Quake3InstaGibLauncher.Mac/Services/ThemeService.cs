using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Quake3InstaGibLauncher.Mac.Models;

namespace Quake3InstaGibLauncher.Mac.Services;

/// <summary>
/// Applica le preferenze di aspetto all'interfaccia gia' in esecuzione, equivalente macOS del
/// ThemeService Windows: i brush di colore dichiarati in App.axaml sono oggetti condivisi
/// mutabili, quindi modificarne la proprieta' Color aggiorna subito ogni punto dell'interfaccia
/// che li usa. I brush a immagine (sfondo finestra/pulsanti/Home) invece cambiano OGGETTO ad ogni
/// applicazione (un'immagine diversa e' un ImageBrush diverso): per questo vengono sostituiti nel
/// dizionario risorse invece di essere mutati, esattamente come fa la versione Windows.
/// </summary>
public static class ThemeService
{
    public static void Apply(UiThemeSettings theme, Window? window = null)
    {
        var resources = Application.Current!.Resources;

        SetBrush(resources, "AccentBrush", theme.AccentColorHex);
        SetBrush(resources, "AccentBrightBrush", theme.AccentBrightColorHex);
        SetBrush(resources, "EnergyBrush", theme.EnergyColorHex);
        SetBrush(resources, "Bg0Brush", theme.BackgroundColorHex);
        SetBrush(resources, "PanelBrush", theme.PanelColorHex);
        SetBrush(resources, "ButtonPrimaryTextBrush", theme.ButtonTextColorHex);

        LinearGradientBrush? gradient = null;
        if (resources.TryGetResource("AccentGradientBrush", null, out var gradientObj) &&
            gradientObj is LinearGradientBrush g && g.GradientStops.Count >= 2)
        {
            g.GradientStops[0].Color = ParseColorOrDefault(theme.AccentColorHex, g.GradientStops[0].Color);
            g.GradientStops[1].Color = ParseColorOrDefault(theme.EnergyColorHex, g.GradientStops[1].Color);
            gradient = g;
        }

        // Sfondo dei pulsanti (normali e principali/CTA): immagine se caricata, altrimenti si
        // torna al colore pannello piatto / al gradiente arancione originale (nessun cambiamento
        // visivo rispetto a prima se l'utente non ha mai importato nulla).
        var buttonImageBrush = TryLoadImageBrush(
            theme.ButtonBackgroundImagePath, theme.ButtonBackgroundImageOpacity, theme.ButtonBackgroundImageFit);
        IBrush panelFallback = resources.TryGetResource("PanelBrush", null, out var panelObj) && panelObj is IBrush pb
            ? pb
            : Brushes.Transparent;
        resources["ButtonBackgroundBrush"] = buttonImageBrush ?? panelFallback;
        resources["PrimaryButtonBackgroundBrush"] = buttonImageBrush ?? (gradient is not null ? (IBrush)gradient : Brushes.OrangeRed);

        // Sfondo finestra: immagine se caricata, altrimenti trasparente (si vede il colore Bg0
        // sotto, nessun cambiamento rispetto a prima).
        resources["WindowBackgroundImageBrush"] = TryLoadImageBrush(
            theme.BackgroundImagePath, theme.BackgroundImageOpacity, theme.BackgroundImageFit)
            ?? (IBrush)Brushes.Transparent;

        // Sfondo dedicato alla Home: nullo quando non impostato, cosi' HomeView.axaml mostra
        // semplicemente il suo emblema/texture originale sotto (nessun Fill sull'elemento).
        resources["HomeBackgroundImageBrush"] = TryLoadImageBrush(
            theme.HomeBackgroundImagePath, theme.HomeBackgroundImageOpacity, theme.HomeBackgroundImageFit);

        UiSoundService.Enabled = theme.UiSoundsEnabled;

        if (window is not null)
        {
            window.Classes.Remove("raised");
            window.Classes.Remove("glow");
            if (theme.ButtonStyle == ButtonStyleMode.Raised) window.Classes.Add("raised");
            else if (theme.ButtonStyle == ButtonStyleMode.Glow) window.Classes.Add("glow");
        }
    }

    private static void SetBrush(IResourceDictionary resources, string key, string hex)
    {
        if (resources.TryGetResource(key, null, out var obj) && obj is SolidColorBrush brush)
            brush.Color = ParseColorOrDefault(hex, brush.Color);
    }

    private static ImageBrush? TryLoadImageBrush(string? path, double opacity, ImageFitMode fit)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            // GIF: Avalonia non lo decodifica/anima nativamente; Bitmap ne legge comunque il primo
            // fotogramma come immagine statica, meglio che ignorarlo del tutto.
            using var stream = File.OpenRead(path);
            var bitmap = new Bitmap(stream);

            return new ImageBrush(bitmap)
            {
                Opacity = Math.Clamp(opacity, 0, 1),
                Stretch = fit switch
                {
                    ImageFitMode.Fill => Stretch.Fill,
                    ImageFitMode.Uniform => Stretch.Uniform,
                    _ => Stretch.UniformToFill,
                },
            };
        }
        catch
        {
            return null; // immagine non valida/illeggibile: si ignora senza far crashare l'app
        }
    }

    private static Color ParseColorOrDefault(string hex, Color fallback)
    {
        try { return Color.Parse(hex); }
        catch { return fallback; } // testo colore non valido: si ignora senza far crashare l'app
    }
}
