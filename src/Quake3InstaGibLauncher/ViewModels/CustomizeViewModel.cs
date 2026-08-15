using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Quake3InstaGibLauncher.Models;
using Quake3InstaGibLauncher.Services;

namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>Una voce della palette rapida nella schermata Personalizza interfaccia.
/// Classe con proprieta' vere (non una ValueTuple) perche' il binding XAML/WPF risolve i nomi
/// dei campi via reflection a runtime, e gli elementi nominati di una ValueTuple esistono solo
/// a livello di metadati del compilatore (a runtime sono Item1/Item2/...): non sarebbero risolvibili.</summary>
public sealed record ColorPreset(string Name, string Accent, string Bright, string Energy);

/// <summary>
/// Schermata "Personalizza interfaccia": colori, stile dei pulsanti (piatto / rialzato /
/// bagliore), immagini personalizzate (logo, sfondo finestra, sfondo pulsanti) e comportamento
/// dell'emblema animato in Home (rotazione, asse, dimensione, posizione).
///
/// IMPORTANTE: ogni singola modifica (colore digitato, slider spostato, immagine importata) viene
/// scritta SUBITO nell'oggetto UiThemeSettings condiviso con MainViewModel/HomeView e salvata SUBITO
/// su disco (CommitAndApply). In una versione precedente le modifiche restavano solo in un'anteprima
/// "live" separata finche' l'utente non premeva esplicitamente "Applica e salva": chiudendo l'app
/// prima di quel click, tutto andava perso (compreso un logo GIF appena importato). Il pulsante
/// "Applica e salva" resta per dare conferma esplicita all'utente, ma non e' piu' necessario:
/// non c'e' nessuno stato "non salvato" possibile.
/// </summary>
public partial class CustomizeViewModel : ObservableObject
{
    private const string ImageFileFilter =
        "Immagini (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif";

    /// <summary>Quante immagini importate in passato restano visibili nella galleria di anteprime
    /// per categoria (logo/sfondo/sfondo pulsanti): oltre questo numero, la piu' vecchia viene
    /// eliminata sia dalla lista sia dal disco, per non accumulare file all'infinito.</summary>
    private const int MaxHistoryEntries = 8;

    private readonly UiThemeSettings _theme;
    private readonly Action _saveSettings;
    private readonly Action<bool>? _onUiSoundsChanged;

    /// <summary>Galleria di anteprime: tutte le immagini importate in passato per categoria (la piu'
    /// recente per prima), cliccabili per riselezionarle senza riaprire la finestra di scelta file.</summary>
    public ObservableCollection<string> LogoImageHistory { get; } = new();
    public ObservableCollection<string> BackgroundImageHistory { get; } = new();
    public ObservableCollection<string> ButtonBackgroundImageHistory { get; } = new();
    public ObservableCollection<string> HomeBackgroundImageHistory { get; } = new();

    /// <summary>Sfondi/pulsanti predefiniti inclusi nell'app (arte originale, cartella "Presets"
    /// accanto all'eseguibile): sempre disponibili, non modificabili/eliminabili dall'utente,
    /// diversi dalla cronologia (che invece contiene le immagini importate manualmente).</summary>
    public IReadOnlyList<string> PresetBackgroundImages { get; } = ListPresetImages("Backgrounds");
    public IReadOnlyList<string> PresetButtonBackgroundImages { get; } = ListPresetImages("Buttons");

    private static IReadOnlyList<string> ListPresetImages(string subFolder)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Presets", subFolder);
            if (!Directory.Exists(dir)) return Array.Empty<string>();

            return Directory.GetFiles(dir)
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>(); // cartella non leggibile: la galleria predefiniti resta vuota, non blocca l'app
        }
    }

    [ObservableProperty] private string _accentColorHex;
    [ObservableProperty] private string _accentBrightColorHex;
    [ObservableProperty] private string _energyColorHex;
    [ObservableProperty] private string _buttonTextColorHex;
    [ObservableProperty] private string _backgroundColorHex;
    [ObservableProperty] private string _panelColorHex;

    [ObservableProperty] private ButtonStyleMode _buttonStyle;

    [ObservableProperty] private bool _logoRotationEnabled;
    [ObservableProperty] private LogoRotationAxis _logoAxis;
    [ObservableProperty] private LogoPosition _logoPos;
    [ObservableProperty] private double _logoSize;
    [ObservableProperty] private int _logoRotationSeconds;
    [ObservableProperty] private string? _logoImagePath;
    [ObservableProperty] private bool _logoImageIsAnimatedGif;

    [ObservableProperty] private string? _backgroundImagePath;
    [ObservableProperty] private double _backgroundImageOpacity;
    [ObservableProperty] private ImageFitMode _backgroundImageFit;

    [ObservableProperty] private string? _buttonBackgroundImagePath;
    [ObservableProperty] private double _buttonBackgroundImageOpacity;
    [ObservableProperty] private ImageFitMode _buttonBackgroundImageFit;

    [ObservableProperty] private string? _homeBackgroundImagePath;
    [ObservableProperty] private double _homeBackgroundImageOpacity;
    [ObservableProperty] private ImageFitMode _homeBackgroundImageFit;

    [ObservableProperty] private int _importedImageMaxResolution;
    [ObservableProperty] private bool _uiSoundsEnabled;

    [ObservableProperty] private string _statusMessage = string.Empty;

    public bool HasLogoImage => !string.IsNullOrWhiteSpace(LogoImagePath);
    public bool HasBackgroundImage => !string.IsNullOrWhiteSpace(BackgroundImagePath);
    public bool HasButtonBackgroundImage => !string.IsNullOrWhiteSpace(ButtonBackgroundImagePath);
    public bool HasHomeBackgroundImage => !string.IsNullOrWhiteSpace(HomeBackgroundImagePath);

    public IReadOnlyList<ColorPreset> Presets { get; } = new[]
    {
        new ColorPreset("Arancio/Rosso (predefinito)", "#FF5A1F", "#FF8A3D", "#E0202B"),
        new ColorPreset("Blu plasma", "#1F6FFF", "#4FA0FF", "#0033A0"),
        new ColorPreset("Verde tossico", "#2FBF4A", "#7CE68C", "#0B6E24"),
        new ColorPreset("Viola energia", "#8A2FE0", "#B570FF", "#4A0F8C"),
        new ColorPreset("Oro imperiale", "#E0A72F", "#FFD873", "#8C5A0F"),
        new ColorPreset("Ciano gelido", "#2FD8E0", "#7DF3F7", "#0F7C8C"),
    };

    public IReadOnlyList<int> AvailableMaxResolutions { get; } = new[] { 1280, 1920, 2560, 3840 };

    public CustomizeViewModel(UiThemeSettings theme, Action saveSettings, Action<bool>? onUiSoundsChanged = null)
    {
        _theme = theme;
        _saveSettings = saveSettings;
        _onUiSoundsChanged = onUiSoundsChanged;

        _accentColorHex = theme.AccentColorHex;
        _accentBrightColorHex = theme.AccentBrightColorHex;
        _energyColorHex = theme.EnergyColorHex;
        _buttonTextColorHex = theme.ButtonTextColorHex;
        _backgroundColorHex = theme.BackgroundColorHex;
        _panelColorHex = theme.PanelColorHex;
        _buttonStyle = theme.ButtonStyle;
        _logoRotationEnabled = theme.LogoRotationEnabled;
        _logoAxis = theme.LogoAxis;
        _logoPos = theme.LogoPos;
        _logoSize = theme.LogoSize;
        _logoRotationSeconds = theme.LogoRotationSeconds;
        _logoImagePath = theme.LogoImagePath;
        _logoImageIsAnimatedGif = theme.LogoImageIsAnimatedGif;
        _backgroundImagePath = theme.BackgroundImagePath;
        _backgroundImageOpacity = theme.BackgroundImageOpacity;
        _backgroundImageFit = theme.BackgroundImageFit;
        _buttonBackgroundImagePath = theme.ButtonBackgroundImagePath;
        _buttonBackgroundImageOpacity = theme.ButtonBackgroundImageOpacity;
        _buttonBackgroundImageFit = theme.ButtonBackgroundImageFit;
        _homeBackgroundImagePath = theme.HomeBackgroundImagePath;
        _homeBackgroundImageOpacity = theme.HomeBackgroundImageOpacity;
        _homeBackgroundImageFit = theme.HomeBackgroundImageFit;
        _importedImageMaxResolution = theme.ImportedImageMaxResolution;
        _uiSoundsEnabled = theme.UiSoundsEnabled;

        // Solo i file che esistono ancora davvero su disco (l'utente potrebbe aver svuotato a mano
        // la cartella dati, o un file potrebbe non essersi importato correttamente in passato).
        foreach (var path in theme.LogoImageHistory.Where(File.Exists)) LogoImageHistory.Add(path);
        foreach (var path in theme.BackgroundImageHistory.Where(File.Exists)) BackgroundImageHistory.Add(path);
        foreach (var path in theme.ButtonBackgroundImageHistory.Where(File.Exists)) ButtonBackgroundImageHistory.Add(path);
        foreach (var path in theme.HomeBackgroundImageHistory.Where(File.Exists)) HomeBackgroundImageHistory.Add(path);
    }

    partial void OnAccentColorHexChanged(string value) => CommitAndApply();
    partial void OnAccentBrightColorHexChanged(string value) => CommitAndApply();
    partial void OnEnergyColorHexChanged(string value) => CommitAndApply();
    partial void OnButtonTextColorHexChanged(string value) => CommitAndApply();
    partial void OnBackgroundColorHexChanged(string value) => CommitAndApply();
    partial void OnPanelColorHexChanged(string value) => CommitAndApply();
    partial void OnButtonStyleChanged(ButtonStyleMode value) => CommitAndApply();
    partial void OnLogoRotationEnabledChanged(bool value) => CommitAndApply();
    partial void OnLogoAxisChanged(LogoRotationAxis value) => CommitAndApply();
    partial void OnLogoPosChanged(LogoPosition value) => CommitAndApply();
    partial void OnLogoSizeChanged(double value) => CommitAndApply();
    partial void OnLogoRotationSecondsChanged(int value) => CommitAndApply();
    partial void OnBackgroundImageOpacityChanged(double value) => CommitAndApply();
    partial void OnBackgroundImageFitChanged(ImageFitMode value) => CommitAndApply();
    partial void OnButtonBackgroundImageOpacityChanged(double value) => CommitAndApply();
    partial void OnButtonBackgroundImageFitChanged(ImageFitMode value) => CommitAndApply();
    partial void OnHomeBackgroundImageOpacityChanged(double value) => CommitAndApply();
    partial void OnHomeBackgroundImageFitChanged(ImageFitMode value) => CommitAndApply();
    partial void OnImportedImageMaxResolutionChanged(int value) => CommitAndApply();
    partial void OnUiSoundsEnabledChanged(bool value)
    {
        CommitAndApply();
        _onUiSoundsChanged?.Invoke(value); // tiene sincronizzato il pulsante rapido on/off nella barra di navigazione
    }

    [RelayCommand]
    private void ApplyPreset(ColorPreset preset)
    {
        AccentColorHex = preset.Accent;
        AccentBrightColorHex = preset.Bright;
        EnergyColorHex = preset.Energy;
    }

    [RelayCommand]
    private void BrowseLogoImage()
    {
        var dialog = new OpenFileDialog { Title = "Scegli l'immagine o GIF del logo", Filter = ImageFileFilter };
        if (dialog.ShowDialog(System.Windows.Application.Current.MainWindow) != true) return;

        try
        {
            var (path, isGif) = ThemeImageService.Import(dialog.FileName, "logo", ImportedImageMaxResolution);
            LogoImagePath = path;
            LogoImageIsAnimatedGif = isGif;
            OnPropertyChanged(nameof(HasLogoImage));
            AddToHistory(LogoImageHistory, path);
            CommitAndApply();
            StatusMessage = "Logo importato e salvato. Vai in Home per vederlo.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile importare l'immagine: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearLogoImage()
    {
        LogoImagePath = null;
        LogoImageIsAnimatedGif = false;
        OnPropertyChanged(nameof(HasLogoImage));
        CommitAndApply();
        StatusMessage = "Logo personalizzato rimosso: torna l'emblema originale.";
    }

    [RelayCommand]
    private void BrowseBackgroundImage()
    {
        var dialog = new OpenFileDialog { Title = "Scegli l'immagine di sfondo", Filter = ImageFileFilter };
        if (dialog.ShowDialog(System.Windows.Application.Current.MainWindow) != true) return;

        try
        {
            var (path, _) = ThemeImageService.Import(dialog.FileName, "background", ImportedImageMaxResolution);
            BackgroundImagePath = path;
            OnPropertyChanged(nameof(HasBackgroundImage));
            AddToHistory(BackgroundImageHistory, path);
            CommitAndApply();
            StatusMessage = "Sfondo importato e salvato.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile importare l'immagine: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearBackgroundImage()
    {
        BackgroundImagePath = null;
        OnPropertyChanged(nameof(HasBackgroundImage));
        CommitAndApply();
        StatusMessage = "Sfondo personalizzato rimosso.";
    }

    [RelayCommand]
    private void BrowseButtonBackgroundImage()
    {
        var dialog = new OpenFileDialog { Title = "Scegli l'immagine di sfondo per i pulsanti", Filter = ImageFileFilter };
        if (dialog.ShowDialog(System.Windows.Application.Current.MainWindow) != true) return;

        try
        {
            var (path, _) = ThemeImageService.Import(dialog.FileName, "buttons", ImportedImageMaxResolution);
            ButtonBackgroundImagePath = path;
            OnPropertyChanged(nameof(HasButtonBackgroundImage));
            AddToHistory(ButtonBackgroundImageHistory, path);
            CommitAndApply();
            StatusMessage = "Sfondo pulsanti importato e salvato.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile importare l'immagine: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearButtonBackgroundImage()
    {
        ButtonBackgroundImagePath = null;
        OnPropertyChanged(nameof(HasButtonBackgroundImage));
        CommitAndApply();
        StatusMessage = "Sfondo pulsanti personalizzato rimosso.";
    }

    [RelayCommand]
    private void BrowseHomeBackgroundImage()
    {
        var dialog = new OpenFileDialog { Title = "Scegli l'immagine di sfondo per la Home", Filter = ImageFileFilter };
        if (dialog.ShowDialog(System.Windows.Application.Current.MainWindow) != true) return;

        try
        {
            var (path, _) = ThemeImageService.Import(dialog.FileName, "homebg", ImportedImageMaxResolution);
            HomeBackgroundImagePath = path;
            OnPropertyChanged(nameof(HasHomeBackgroundImage));
            AddToHistory(HomeBackgroundImageHistory, path);
            CommitAndApply();
            StatusMessage = "Sfondo Home importato e salvato.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile importare l'immagine: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearHomeBackgroundImage()
    {
        HomeBackgroundImagePath = null;
        OnPropertyChanged(nameof(HasHomeBackgroundImage));
        CommitAndApply();
        StatusMessage = "Sfondo Home rimosso: torna la texture originale.";
    }

    [RelayCommand]
    private void SelectHomeBackgroundFromHistory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        HomeBackgroundImagePath = path;
        OnPropertyChanged(nameof(HasHomeBackgroundImage));
        AddToHistory(HomeBackgroundImageHistory, path);
        CommitAndApply();
        StatusMessage = "Sfondo Home selezionato dalla galleria.";
    }

    [RelayCommand]
    private void RemoveHomeBackgroundFromHistory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        HomeBackgroundImageHistory.Remove(path);
        if (string.Equals(HomeBackgroundImagePath, path, StringComparison.OrdinalIgnoreCase))
        {
            HomeBackgroundImagePath = null;
            OnPropertyChanged(nameof(HasHomeBackgroundImage));
        }
        TryDeleteFile(path);
        CommitAndApply();
    }

    [RelayCommand]
    private void SelectPresetHomeBackground(string? presetPath)
    {
        if (string.IsNullOrWhiteSpace(presetPath) || !File.Exists(presetPath)) return;
        try
        {
            var (path, _) = ThemeImageService.Import(presetPath, "homebg", ImportedImageMaxResolution);
            HomeBackgroundImagePath = path;
            OnPropertyChanged(nameof(HasHomeBackgroundImage));
            AddToHistory(HomeBackgroundImageHistory, path);
            CommitAndApply();
            StatusMessage = "Sfondo Home predefinito applicato.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile applicare il predefinito: {ex.Message}";
        }
    }

    // ===================== Predefiniti inclusi nell'app =====================

    [RelayCommand]
    private void SelectPresetBackground(string? presetPath)
    {
        if (string.IsNullOrWhiteSpace(presetPath) || !File.Exists(presetPath)) return;
        try
        {
            var (path, _) = ThemeImageService.Import(presetPath, "background", ImportedImageMaxResolution);
            BackgroundImagePath = path;
            OnPropertyChanged(nameof(HasBackgroundImage));
            AddToHistory(BackgroundImageHistory, path);
            CommitAndApply();
            StatusMessage = "Sfondo predefinito applicato.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile applicare il predefinito: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SelectPresetButtonBackground(string? presetPath)
    {
        if (string.IsNullOrWhiteSpace(presetPath) || !File.Exists(presetPath)) return;
        try
        {
            var (path, _) = ThemeImageService.Import(presetPath, "buttons", ImportedImageMaxResolution);
            ButtonBackgroundImagePath = path;
            OnPropertyChanged(nameof(HasButtonBackgroundImage));
            AddToHistory(ButtonBackgroundImageHistory, path);
            CommitAndApply();
            StatusMessage = "Sfondo pulsanti predefinito applicato.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile applicare il predefinito: {ex.Message}";
        }
    }

    // ===================== Galleria: riseleziona/rimuovi da una gia' importata =====================

    [RelayCommand]
    private void SelectLogoFromHistory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        LogoImagePath = path;
        LogoImageIsAnimatedGif = Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(HasLogoImage));
        AddToHistory(LogoImageHistory, path); // la riporta in cima come "usata di recente"
        CommitAndApply();
        StatusMessage = "Logo selezionato dalla galleria.";
    }

    [RelayCommand]
    private void RemoveLogoFromHistory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        LogoImageHistory.Remove(path);
        if (string.Equals(LogoImagePath, path, StringComparison.OrdinalIgnoreCase))
        {
            LogoImagePath = null;
            LogoImageIsAnimatedGif = false;
            OnPropertyChanged(nameof(HasLogoImage));
        }
        TryDeleteFile(path);
        CommitAndApply();
    }

    [RelayCommand]
    private void SelectBackgroundFromHistory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        BackgroundImagePath = path;
        OnPropertyChanged(nameof(HasBackgroundImage));
        AddToHistory(BackgroundImageHistory, path);
        CommitAndApply();
        StatusMessage = "Sfondo selezionato dalla galleria.";
    }

    [RelayCommand]
    private void RemoveBackgroundFromHistory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        BackgroundImageHistory.Remove(path);
        if (string.Equals(BackgroundImagePath, path, StringComparison.OrdinalIgnoreCase))
        {
            BackgroundImagePath = null;
            OnPropertyChanged(nameof(HasBackgroundImage));
        }
        TryDeleteFile(path);
        CommitAndApply();
    }

    [RelayCommand]
    private void SelectButtonBackgroundFromHistory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        ButtonBackgroundImagePath = path;
        OnPropertyChanged(nameof(HasButtonBackgroundImage));
        AddToHistory(ButtonBackgroundImageHistory, path);
        CommitAndApply();
        StatusMessage = "Sfondo pulsanti selezionato dalla galleria.";
    }

    [RelayCommand]
    private void RemoveButtonBackgroundFromHistory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        ButtonBackgroundImageHistory.Remove(path);
        if (string.Equals(ButtonBackgroundImagePath, path, StringComparison.OrdinalIgnoreCase))
        {
            ButtonBackgroundImagePath = null;
            OnPropertyChanged(nameof(HasButtonBackgroundImage));
        }
        TryDeleteFile(path);
        CommitAndApply();
    }

    /// <summary>Inserisce (o riporta) un percorso in cima alla cronologia; oltre MaxHistoryEntries
    /// elimina la voce piu' vecchia sia dalla lista sia dal file su disco.</summary>
    private static void AddToHistory(ObservableCollection<string> history, string path)
    {
        history.Remove(path);
        history.Insert(0, path);
        while (history.Count > MaxHistoryEntries)
        {
            var oldest = history[^1];
            history.RemoveAt(history.Count - 1);
            TryDeleteFile(oldest);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* file in uso o non cancellabile: non blocca l'interfaccia */ }
    }

    /// <summary>Pulsante "Applica e salva": non e' piu' l'unico modo di salvare (ogni modifica lo fa
    /// gia' da sola), resta come conferma esplicita rassicurante per l'utente.</summary>
    [RelayCommand]
    private void ApplyAndSave()
    {
        CommitAndApply();
        StatusMessage = "Aspetto applicato e salvato. Torna in Home per vedere l'emblema aggiornato.";
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var fresh = new UiThemeSettings();
        AccentColorHex = fresh.AccentColorHex;
        AccentBrightColorHex = fresh.AccentBrightColorHex;
        EnergyColorHex = fresh.EnergyColorHex;
        ButtonTextColorHex = fresh.ButtonTextColorHex;
        BackgroundColorHex = fresh.BackgroundColorHex;
        PanelColorHex = fresh.PanelColorHex;
        ButtonStyle = fresh.ButtonStyle;
        LogoRotationEnabled = fresh.LogoRotationEnabled;
        LogoAxis = fresh.LogoAxis;
        LogoPos = fresh.LogoPos;
        LogoSize = fresh.LogoSize;
        LogoRotationSeconds = fresh.LogoRotationSeconds;
        LogoImagePath = fresh.LogoImagePath;
        LogoImageIsAnimatedGif = fresh.LogoImageIsAnimatedGif;
        BackgroundImagePath = fresh.BackgroundImagePath;
        BackgroundImageOpacity = fresh.BackgroundImageOpacity;
        BackgroundImageFit = fresh.BackgroundImageFit;
        ButtonBackgroundImagePath = fresh.ButtonBackgroundImagePath;
        ButtonBackgroundImageOpacity = fresh.ButtonBackgroundImageOpacity;
        ButtonBackgroundImageFit = fresh.ButtonBackgroundImageFit;
        HomeBackgroundImagePath = fresh.HomeBackgroundImagePath;
        HomeBackgroundImageOpacity = fresh.HomeBackgroundImageOpacity;
        HomeBackgroundImageFit = fresh.HomeBackgroundImageFit;
        ImportedImageMaxResolution = fresh.ImportedImageMaxResolution;
        UiSoundsEnabled = fresh.UiSoundsEnabled;
        OnPropertyChanged(nameof(HasLogoImage));
        OnPropertyChanged(nameof(HasBackgroundImage));
        OnPropertyChanged(nameof(HasButtonBackgroundImage));
        OnPropertyChanged(nameof(HasHomeBackgroundImage));

        CommitAndApply();
        StatusMessage = "Aspetto predefinito ripristinato.";
    }

    /// <summary>Scrive tutte le proprieta' correnti nell'oggetto UiThemeSettings CONDIVISO (lo
    /// stesso riferimento usato da MainViewModel/HomeView e serializzato su disco), aggiorna
    /// subito l'interfaccia e salva subito le impostazioni. Chiamato da ogni singola modifica:
    /// nessuna preferenza di aspetto puo' piu' andare persa chiudendo l'app.</summary>
    private void CommitAndApply()
    {
        _theme.AccentColorHex = AccentColorHex;
        _theme.AccentBrightColorHex = AccentBrightColorHex;
        _theme.EnergyColorHex = EnergyColorHex;
        _theme.ButtonTextColorHex = ButtonTextColorHex;
        _theme.BackgroundColorHex = BackgroundColorHex;
        _theme.PanelColorHex = PanelColorHex;
        _theme.ButtonStyle = ButtonStyle;
        _theme.LogoRotationEnabled = LogoRotationEnabled;
        _theme.LogoAxis = LogoAxis;
        _theme.LogoPos = LogoPos;
        _theme.LogoSize = LogoSize;
        _theme.LogoRotationSeconds = LogoRotationSeconds;
        _theme.LogoImagePath = LogoImagePath;
        _theme.LogoImageIsAnimatedGif = LogoImageIsAnimatedGif;
        _theme.BackgroundImagePath = BackgroundImagePath;
        _theme.BackgroundImageOpacity = BackgroundImageOpacity;
        _theme.BackgroundImageFit = BackgroundImageFit;
        _theme.ButtonBackgroundImagePath = ButtonBackgroundImagePath;
        _theme.ButtonBackgroundImageOpacity = ButtonBackgroundImageOpacity;
        _theme.ButtonBackgroundImageFit = ButtonBackgroundImageFit;
        _theme.HomeBackgroundImagePath = HomeBackgroundImagePath;
        _theme.HomeBackgroundImageOpacity = HomeBackgroundImageOpacity;
        _theme.HomeBackgroundImageFit = HomeBackgroundImageFit;
        _theme.ImportedImageMaxResolution = ImportedImageMaxResolution;
        _theme.UiSoundsEnabled = UiSoundsEnabled;
        _theme.LogoImageHistory = LogoImageHistory.ToList();
        _theme.BackgroundImageHistory = BackgroundImageHistory.ToList();
        _theme.ButtonBackgroundImageHistory = ButtonBackgroundImageHistory.ToList();
        _theme.HomeBackgroundImageHistory = HomeBackgroundImageHistory.ToList();

        ThemeService.Apply(_theme);
        _saveSettings();
    }
}
