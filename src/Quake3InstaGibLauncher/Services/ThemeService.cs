using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Quake3InstaGibLauncher.Models;

namespace Quake3InstaGibLauncher.Services;

/// <summary>
/// Applica le preferenze di aspetto (Models.UiThemeSettings) all'interfaccia gia' in esecuzione,
/// senza bisogno di riavviare l'app.
///
/// IMPORTANTE: i brush usati come valore di un Setter dentro uno Style/ControlTemplate vengono
/// "congelati" (Freezable.Freeze) automaticamente da WPF non appena lo Style viene sigillato al
/// primo utilizzo — a quel punto l'oggetto brush diventa permanentemente di sola lettura e
/// tentare di cambiarne la proprieta' Color lancia InvalidOperationException. Per questo qui non
/// mutiamo l'oggetto esistente: lo SOSTITUIAMO con un nuovo brush nel dizionario risorse. Perche'
/// il cambiamento si veda dal vivo, Theme.xaml/le view usano {DynamicResource} (non
/// {StaticResource}) per questi brush specifici: DynamicResource ri-risolve la chiave ogni volta
/// che il dizionario cambia, mentre StaticResource la risolverebbe una sola volta.
/// </summary>
public static class ThemeService
{
    public static void Apply(UiThemeSettings theme)
    {
        var resources = Application.Current.Resources;

        var accent = ParseColorOrDefault(theme.AccentColorHex, (Color)ColorConverter.ConvertFromString("#FF5A1F")!);
        var accentBright = ParseColorOrDefault(theme.AccentBrightColorHex, (Color)ColorConverter.ConvertFromString("#FF8A3D")!);
        var energy = ParseColorOrDefault(theme.EnergyColorHex, (Color)ColorConverter.ConvertFromString("#E0202B")!);
        var buttonText = ParseColorOrDefault(theme.ButtonTextColorHex, Colors.White);
        var background = ParseColorOrDefault(theme.BackgroundColorHex, (Color)ColorConverter.ConvertFromString("#0A0B0D")!);
        var panel = ParseColorOrDefault(theme.PanelColorHex, (Color)ColorConverter.ConvertFromString("#1C2025")!);

        ReplaceBrush(resources, "Brush.Accent", accent);
        ReplaceBrush(resources, "Brush.AccentBright", accentBright);
        ReplaceBrush(resources, "Brush.Energy", energy);
        ReplaceBrush(resources, "Brush.Bg0", background);
        ReplaceBrush(resources, "Brush.Panel", panel);
        ReplaceBrush(resources, "Brush.ButtonPrimaryText", buttonText);

        var gradient = new LinearGradientBrush(accent, energy, new Point(0, 0), new Point(1, 1));
        resources["Brush.AccentGradient"] = gradient;

        // Sfondo pulsanti: immagine se caricata, altrimenti torna al colore pannello piatto.
        var buttonImageBrush = TryLoadImageBrush(
            theme.ButtonBackgroundImagePath, theme.ButtonBackgroundImageOpacity, theme.ButtonBackgroundImageFit);
        resources["Brush.ButtonBackground"] = buttonImageBrush ?? (Brush)new SolidColorBrush(panel);

        // Sfondo dei pulsanti PRINCIPALI (arancioni: AVVIA, hero della Home, ecc.): stessa immagine
        // se caricata (altrimenti restano con il gradiente arancione originale, look invariato).
        resources["Brush.PrimaryButtonBackground"] = buttonImageBrush ?? (Brush)gradient;

        // Sfondo finestra: immagine se caricata, altrimenti trasparente (si vede il colore Bg0 sotto).
        resources["Brush.WindowBackgroundImage"] = TryLoadImageBrush(
            theme.BackgroundImagePath, theme.BackgroundImageOpacity, theme.BackgroundImageFit)
            ?? (Brush)new SolidColorBrush(Colors.Transparent);

        // Sfondo dedicato alla Home: null (nessun Fill) quando non impostato, cosi' HomeView.xaml
        // mostra semplicemente la sua texture procedurale metallo/ruggine originale sotto.
        resources["Brush.HomeBackgroundImage"] = TryLoadImageBrush(
            theme.HomeBackgroundImagePath, theme.HomeBackgroundImageOpacity, theme.HomeBackgroundImageFit);

        ButtonStyleState.Instance.CurrentMode = theme.ButtonStyle;
        UiSoundService.Enabled = theme.UiSoundsEnabled;
    }

    private static void ReplaceBrush(ResourceDictionary resources, string key, Color color)
        => resources[key] = new SolidColorBrush(color);

    private static ImageBrush? TryLoadImageBrush(string? path, double opacity, ImageFitMode fit)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var bitmap = LoadBitmapNoCache(path);

            var brush = new ImageBrush(bitmap)
            {
                Opacity = Math.Clamp(opacity, 0, 1),
                Stretch = fit switch
                {
                    ImageFitMode.Fill => Stretch.Fill,
                    ImageFitMode.Uniform => Stretch.Uniform,
                    _ => Stretch.UniformToFill,
                },
            };
            brush.Freeze();
            return brush;
        }
        catch
        {
            return null; // immagine non valida/illeggibile: si ignora senza far crashare l'app
        }
    }

    /// <summary>
    /// Carica un'immagine da disco IGNORANDO la cache interna di WPF basata sull'URI. Le immagini
    /// di tema (logo/sfondo/sfondo pulsanti) vengono sempre re-importate con lo STESSO nome file
    /// (vedi ThemeImageService.Import): senza questo, dopo aver scelto una nuova immagine WPF
    /// continuerebbe a mostrare quella vecchia (stesso URI = bitmap gia' in cache), finche' l'app
    /// non viene chiusa e riaperta. BitmapCacheOption.OnLoad da solo NON basta: riguarda solo se i
    /// pixel restano in memoria dopo la chiusura dello stream, non la cache per-URI.
    /// </summary>
    public static BitmapImage LoadBitmapNoCache(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static Color ParseColorOrDefault(string hex, Color fallback)
    {
        try
        {
            var converted = ColorConverter.ConvertFromString(hex);
            return converted is Color color ? color : fallback;
        }
        catch
        {
            return fallback; // testo colore non valido inserito dall'utente: si ignora senza far crashare l'app
        }
    }
}
