using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Mac.Models;
using Quake3InstaGibLauncher.Mac.Services;

namespace Quake3InstaGibLauncher.Mac.ViewModels;

public sealed record ColorPreset(string Name, string Accent, string Bright, string Energy);

/// <summary>
/// Schermata "Personalizza interfaccia", equivalente macOS di quella Windows: colori, stile
/// pulsanti, immagini personalizzate (sfondo finestra/pulsanti/Home, con galleria di predefiniti
/// inclusi nell'app e cronologia delle immagini importate), suoni dell'interfaccia.
///
/// Come su Windows: ogni singola modifica viene scritta SUBITO nell'oggetto UiThemeSettings
/// condiviso e salvata SUBITO su disco (CommitAndApply) — nessuno stato "non salvato" possibile.
/// </summary>
public partial class CustomizeViewModel : ObservableObject
{
    /// <summary>Quante immagini importate in passato restano visibili nella galleria di anteprime
    /// per categoria: oltre questo numero, la piu' vecchia viene eliminata sia dalla lista sia dal
    /// disco, per non accumulare file all'infinito.</summary>
    private const int MaxHistoryEntries = 8;

    private readonly UiThemeSettings _theme;
    private readonly Action _saveSettings;
    private readonly Action<UiThemeSettings, Avalonia.Controls.Window?> _applyTheme;
    private readonly Func<Task<string?>> _pickImageFile;
    private readonly Action<bool>? _onUiSoundsChanged;

    public ObservableCollection<string> BackgroundImageHistory { get; } = new();
    public ObservableCollection<string> ButtonBackgroundImageHistory { get; } = new();
    public ObservableCollection<string> HomeBackgroundImageHistory { get; } = new();

    /// <summary>Sfondi/pulsanti predefiniti inclusi nell'app (arte originale, cartella "Presets"
    /// accanto all'eseguibile): sempre disponibili, non modificabili/eliminabili dall'utente.</summary>
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
            return Array.Empty<string>();
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

    public CustomizeViewModel(UiThemeSettings theme, Action saveSettings,
        Action<UiThemeSettings, Avalonia.Controls.Window?> applyTheme, Func<Task<string?>> pickImageFile,
        Action<bool>? onUiSoundsChanged = null)
    {
        _theme = theme;
        _saveSettings = saveSettings;
        _applyTheme = applyTheme;
        _pickImageFile = pickImageFile;
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
        _onUiSoundsChanged?.Invoke(value);
    }

    [RelayCommand]
    private void ApplyPreset(ColorPreset preset)
    {
        AccentColorHex = preset.Accent;
        AccentBrightColorHex = preset.Bright;
        EnergyColorHex = preset.Energy;
    }

    [RelayCommand]
    private async Task BrowseBackgroundImageAsync()
    {
        var picked = await _pickImageFile();
        ImportBackground(picked, "Sfondo importato e salvato.");
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
    private async Task BrowseButtonBackgroundImageAsync()
    {
        var picked = await _pickImageFile();
        ImportButtonBackground(picked, "Sfondo pulsanti importato e salvato.");
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
    private async Task BrowseHomeBackgroundImageAsync()
    {
        var picked = await _pickImageFile();
        ImportHomeBackground(picked, "Sfondo Home importato e salvato.");
    }

    [RelayCommand]
    private void ClearHomeBackgroundImage()
    {
        HomeBackgroundImagePath = null;
        OnPropertyChanged(nameof(HasHomeBackgroundImage));
        CommitAndApply();
        StatusMessage = "Sfondo Home rimosso: torna l'emblema originale.";
    }

    [RelayCommand]
    private void SelectHomeBackgroundFromHistory(string? path) => ImportHomeBackground(path, "Sfondo Home selezionato dalla galleria.", isReselect: true);

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
    private void SelectPresetHomeBackground(string? presetPath) => ImportHomeBackground(presetPath, "Sfondo Home predefinito applicato.");

    [RelayCommand]
    private void SelectPresetBackground(string? presetPath) => ImportBackground(presetPath, "Sfondo predefinito applicato.");

    [RelayCommand]
    private void SelectPresetButtonBackground(string? presetPath) => ImportButtonBackground(presetPath, "Sfondo pulsanti predefinito applicato.");

    [RelayCommand]
    private void SelectBackgroundFromHistory(string? path) => ImportBackground(path, "Sfondo selezionato dalla galleria.", isReselect: true);

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
    private void SelectButtonBackgroundFromHistory(string? path) => ImportButtonBackground(path, "Sfondo pulsanti selezionato dalla galleria.", isReselect: true);

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

    private void ImportBackground(string? sourcePath, string successMessage, bool isReselect = false)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return;
        try
        {
            var path = isReselect ? sourcePath : ThemeImageService.Import(sourcePath, "background", ImportedImageMaxResolution).Path;
            BackgroundImagePath = path;
            OnPropertyChanged(nameof(HasBackgroundImage));
            AddToHistory(BackgroundImageHistory, path);
            CommitAndApply();
            StatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile importare l'immagine: {ex.Message}";
        }
    }

    private void ImportButtonBackground(string? sourcePath, string successMessage, bool isReselect = false)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return;
        try
        {
            var path = isReselect ? sourcePath : ThemeImageService.Import(sourcePath, "buttons", ImportedImageMaxResolution).Path;
            ButtonBackgroundImagePath = path;
            OnPropertyChanged(nameof(HasButtonBackgroundImage));
            AddToHistory(ButtonBackgroundImageHistory, path);
            CommitAndApply();
            StatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile importare l'immagine: {ex.Message}";
        }
    }

    private void ImportHomeBackground(string? sourcePath, string successMessage, bool isReselect = false)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return;
        try
        {
            var path = isReselect ? sourcePath : ThemeImageService.Import(sourcePath, "homebg", ImportedImageMaxResolution).Path;
            HomeBackgroundImagePath = path;
            OnPropertyChanged(nameof(HasHomeBackgroundImage));
            AddToHistory(HomeBackgroundImageHistory, path);
            CommitAndApply();
            StatusMessage = successMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile importare l'immagine: {ex.Message}";
        }
    }

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
        OnPropertyChanged(nameof(HasBackgroundImage));
        OnPropertyChanged(nameof(HasButtonBackgroundImage));
        OnPropertyChanged(nameof(HasHomeBackgroundImage));

        CommitAndApply();
        StatusMessage = "Aspetto predefinito ripristinato.";
    }

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
        _theme.BackgroundImageHistory = BackgroundImageHistory.ToList();
        _theme.ButtonBackgroundImageHistory = ButtonBackgroundImageHistory.ToList();
        _theme.HomeBackgroundImageHistory = HomeBackgroundImageHistory.ToList();

        _applyTheme(_theme, null);
        _saveSettings();
    }
}
