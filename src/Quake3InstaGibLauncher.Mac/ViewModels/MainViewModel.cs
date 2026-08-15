using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Mac.Models;
using Quake3InstaGibLauncher.Mac.Services;

namespace Quake3InstaGibLauncher.Mac.ViewModels;

public enum AppScreen { Home, LocalSetup, MultiplayerSetup, Settings, Diagnostics, Help, Customize, Players, KeyBindings, PlayerProfile }

/// <summary>
/// ViewModel radice della versione macOS: stessa architettura e stessa logica di orchestrazione
/// della versione Windows (vedi Quake3InstaGibLauncher.ViewModels.MainViewModel), adattata alla
/// risoluzione dei percorsi macOS (eseguibile e cartella dati separati, rilevamento automatico
/// con fallback a selezione manuale).
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly MacSettingsService _settingsService;
    private readonly MacInstallationLocator _installationLocator;
    private readonly MapCacheService _mapCacheService;
    private readonly LaunchService _launchService;
    private readonly RotationCfgService _rotationCfgService;
    private readonly Action<string> _openFolder;

    private readonly List<MapCardViewModel> _allMapCards = new();
    private TaskCompletionSource<bool>? _pendingConfirm;

    public MacAppSettings Settings { get; }

    [ObservableProperty] private AppScreen _currentScreen = AppScreen.Home;
    [ObservableProperty] private InstallationStatus? _installationStatus;
    [ObservableProperty] private string? _bannerMessage;
    [ObservableProperty] private bool _bannerIsError;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyMessage = string.Empty;

    [ObservableProperty] private bool _isConfirmOpen;
    [ObservableProperty] private string _confirmSummary = string.Empty;
    [ObservableProperty] private string _confirmAdvancedCommand = string.Empty;
    [ObservableProperty] private bool _confirmShowAdvanced;

    public HomeViewModel Home { get; } = new();
    public DiagnosticsViewModel Diagnostics { get; } = new();
    public HelpViewModel HelpVm { get; } = new();
    public CustomizeViewModel CustomizeVm { get; }
    public SettingsViewModel SettingsVm { get; }
    public LocalSetupViewModel LocalSetup { get; }
    public MultiplayerSetupViewModel MultiplayerSetup { get; }
    public PlayersViewModel PlayersVm { get; }
    public KeyBindingsViewModel KeyBindingsVm { get; }
    public PlayerProfileViewModel PlayerProfileVm { get; }

    [ObservableProperty] private bool _uiSoundsEnabled = true;

    private MacInstallationPaths CurrentPaths => _installationLocator.ResolvePaths(Settings.ExecutablePath, Settings.DataRootPath);

    public MainViewModel(
        MacSettingsService settingsService,
        MacInstallationLocator installationLocator,
        MapCacheService mapCacheService,
        LaunchService launchService,
        RotationCfgService rotationCfgService,
        Func<Task<string?>> pickExecutableFile,
        Func<Task<string?>> pickDataFolder,
        Func<Task<string?>> pickImageFile,
        Action<string> openFolder,
        Action<Models.UiThemeSettings, Avalonia.Controls.Window?> applyTheme,
        Func<(int Width, int Height)?> detectScreenResolution)
    {
        _settingsService = settingsService;
        _installationLocator = installationLocator;
        _mapCacheService = mapCacheService;
        _launchService = launchService;
        _rotationCfgService = rotationCfgService;
        _openFolder = openFolder;

        Settings = _settingsService.Load();
        applyTheme(Settings.Theme, null);
        LocalizationService.Apply(Settings.Language);

        // Rilevamento risoluzione al primo avvio (o se non mai riuscito prima): evita le barre
        // nere laterali forzando la risoluzione reale invece di lasciare che il motore usi una
        // modalita' 4:3 storica salvata in precedenza in q3config.cfg.
        if (Settings.ScreenWidth <= 0 || Settings.ScreenHeight <= 0)
        {
            var detected = detectScreenResolution();
            if (detected is { } d && d.Width > 0 && d.Height > 0)
            {
                Settings.ScreenWidth = d.Width;
                Settings.ScreenHeight = d.Height;
                _settingsService.Save(Settings);
            }
        }

        CustomizeVm = new CustomizeViewModel(Settings.Theme, SaveSettingsQuiet, applyTheme, pickImageFile,
            onUiSoundsChanged: value => UiSoundsEnabled = value);

        // Impostato DOPO la costruzione di CustomizeVm: il partial OnUiSoundsEnabledChanged
        // scrive anche in CustomizeVm.UiSoundsEnabled, che prima di questo punto sarebbe ancora
        // null (causava un NullReferenceException all'avvio, trovato con un lancio reale
        // dell'exe durante la verifica).
        UiSoundsEnabled = Settings.Theme.UiSoundsEnabled;

        SettingsVm = new SettingsViewModel(
            Settings,
            applyNewPaths: ApplyNewPathsAsync,
            refreshMaps: () => RefreshMapsAsync(),
            clearCache: () => _mapCacheService.ClearCache(),
            resetToDefaults: ResetSettingsToDefaults,
            openFolder: openFolder,
            pickExecutableFile: pickExecutableFile,
            pickDataFolder: pickDataFolder,
            detectScreenResolution: detectScreenResolution);

        LocalSetup = new LocalSetupViewModel(
            Settings,
            getPaths: () => CurrentPaths,
            confirmLaunch: ConfirmLaunchAsync,
            onLaunched: OnLaunched,
            onDiagnosticsReport: Diagnostics.ReportLaunch,
            onMapUsed: map => RegisterMapUsed(map, isLocal: true),
            _launchService,
            _rotationCfgService);

        MultiplayerSetup = new MultiplayerSetupViewModel(
            Settings,
            getPaths: () => CurrentPaths,
            confirmLaunch: ConfirmLaunchAsync,
            onLaunched: OnLaunched,
            onDiagnosticsReport: Diagnostics.ReportLaunch,
            onMapUsed: map => RegisterMapUsed(map, isLocal: false),
            _launchService,
            _rotationCfgService,
            saveSettings: SaveSettingsQuiet);

        PlayersVm = new PlayersViewModel(Settings, SaveSettingsQuiet);
        KeyBindingsVm = new KeyBindingsViewModel(Settings, SaveSettingsQuiet);
        PlayerProfileVm = new PlayerProfileViewModel(Settings, SaveSettingsQuiet);
    }

    partial void OnUiSoundsEnabledChanged(bool value)
    {
        CustomizeVm.UiSoundsEnabled = value;
        UiSoundService.Enabled = value;
    }

    [RelayCommand] private void ToggleUiSounds() => UiSoundsEnabled = !UiSoundsEnabled;

    [RelayCommand]
    private void SetLanguage(AppLanguage language)
    {
        Settings.Language = language;
        LocalizationService.Apply(language);
        SaveSettingsQuiet();
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(Settings.ExecutablePath) || string.IsNullOrEmpty(Settings.DataRootPath))
        {
            var (exe, data) = _installationLocator.AutoDetect();
            if (exe is not null) Settings.ExecutablePath = exe;
            if (data is not null) Settings.DataRootPath = data;
            SettingsVm.ExecutablePath = Settings.ExecutablePath;
            SettingsVm.DataRootPath = Settings.DataRootPath;
            SaveSettingsQuiet();
        }

        RevalidateInstallation();

        if (InstallationStatus is { IsValid: false })
        {
            CurrentScreen = AppScreen.Settings;
            ShowBanner(
                "Non trovo un'installazione completa di ioquake3 nei percorsi tipici di macOS. " +
                "Seleziona qui sotto l'eseguibile e la cartella dati corretti e premi \"Applica percorsi e verifica\".",
                isError: true);
            return;
        }

        var cached = _mapCacheService.TryLoadFromDisk();
        if (cached is not null) PopulateMapCards(cached);
        else await RefreshMapsAsync();
    }

    private void RevalidateInstallation()
    {
        InstallationStatus = _installationLocator.Validate(Settings.ExecutablePath, Settings.DataRootPath);
        Diagnostics.UpdateChecks(InstallationStatus);
        Home.UpdateFromStatus(InstallationStatus, _allMapCards.Count);
    }

    public async Task RefreshMapsAsync()
    {
        IsBusy = true;
        try
        {
            var progress = new Progress<string>(msg => BusyMessage = msg);
            var paths = CurrentPaths;
            var roots = new (string Directory, MapSource Source)[]
            {
                (paths.BaseQ3Path, MapSource.BaseQ3),
                (paths.InstaGibPath, MapSource.InstaGib129),
                (paths.MissionPackPath, MapSource.MissionPack),
            };
            var result = await _mapCacheService.RefreshAsync(roots, progress);
            PopulateMapCards(result.Maps);
            Diagnostics.UpdateScanErrors(result.Errors);

            if (result.Errors.Count > 0)
                ShowBanner($"Scansione completata con {result.Errors.Count} avviso/i (vedi Diagnostica).", isError: false);
        }
        catch (Exception ex)
        {
            ShowBanner($"Scansione mappe non riuscita: {ex.Message}", isError: true);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private void PopulateMapCards(IReadOnlyList<MapInfo> maps)
    {
        _allMapCards.Clear();
        foreach (var info in maps)
        {
            var card = new MapCardViewModel(info)
            {
                IsFavorite = Settings.FavoriteMaps.Contains(MapKeyOf(info)),
            };
            _allMapCards.Add(card);
        }

        LocalSetup.Gallery.SetMaps(_allMapCards, Settings.LocalLastMap);
        MultiplayerSetup.Gallery.SetMaps(_allMapCards, Settings.ServerLastMap);
        RestoreRotation(LocalSetup.Gallery, Settings.LocalRotationMaps);
        RestoreRotation(MultiplayerSetup.Gallery, Settings.ServerRotationMaps);

        Home.UpdateFromStatus(InstallationStatus ?? new InstallationStatus { IsValid = false }, _allMapCards.Count);
    }

    private void RestoreRotation(MapGalleryPanelViewModel gallery, List<string> mapNames)
    {
        gallery.RotationMaps.Clear();
        foreach (var name in mapNames)
        {
            var card = _allMapCards.FirstOrDefault(m => string.Equals(m.TechnicalName, name, StringComparison.OrdinalIgnoreCase));
            if (card is not null) gallery.RotationMaps.Add(card);
        }
    }

    private static string MapKeyOf(MapInfo info) => $"{info.Source}:{info.TechnicalName}";

    private void RegisterMapUsed(MapCardViewModel map, bool isLocal)
    {
        var recentKey = map.TechnicalName;
        if (isLocal) Settings.LocalLastMap = recentKey; else Settings.ServerLastMap = recentKey;

        Settings.RecentMaps.Remove(recentKey);
        Settings.RecentMaps.Insert(0, recentKey);
        if (Settings.RecentMaps.Count > 10) Settings.RecentMaps.RemoveRange(10, Settings.RecentMaps.Count - 10);

        Settings.FavoriteMaps = _allMapCards.Where(m => m.IsFavorite).Select(m => MapKeyOf(m.Info)).Distinct().ToList();
        SaveSettingsQuiet();
    }

    private async Task ApplyNewPathsAsync(string executablePath, string dataRootPath)
    {
        Settings.ExecutablePath = executablePath;
        Settings.DataRootPath = dataRootPath;
        RevalidateInstallation();
        SaveSettingsQuiet();

        if (InstallationStatus is { IsValid: false })
        {
            ShowBanner($"Percorsi non validi: mancano {InstallationStatus.MissingItems.Count} elemento/i (vedi Diagnostica).", isError: true);
            return;
        }

        ShowBanner("Installazione trovata correttamente.", isError: false);
        await RefreshMapsAsync();
        CurrentScreen = AppScreen.Home;
    }

    private void ResetSettingsToDefaults()
    {
        _settingsService.ResetToDefaults();
        var fresh = new MacAppSettings();
        Settings.ExecutablePath = fresh.ExecutablePath;
        Settings.DataRootPath = fresh.DataRootPath;
        Settings.Fov = fresh.Fov;
        Settings.FixAspectRatio = fresh.FixAspectRatio;
        Settings.Fullscreen = fresh.Fullscreen;
        SettingsVm.ExecutablePath = fresh.ExecutablePath;
        SettingsVm.DataRootPath = fresh.DataRootPath;
        SettingsVm.Fov = fresh.Fov;
        SettingsVm.FixAspectRatio = fresh.FixAspectRatio;
        SettingsVm.Fullscreen = fresh.Fullscreen;

        ShowBanner("Impostazioni ripristinate. I valori di partita locale/multiplayer torneranno predefiniti al prossimo avvio dell'app.", isError: false);
        SaveSettingsQuiet();
    }

    private void OnLaunched(LaunchOutcome outcome)
    {
        if (!outcome.Success) ShowBanner(outcome.ErrorMessage ?? "Avvio non riuscito.", isError: true);
        else ShowBanner("ioquake3 avviato correttamente.", isError: false);
    }

    private void ShowBanner(string message, bool isError)
    {
        BannerMessage = message;
        BannerIsError = isError;
    }

    private Task<bool> ConfirmLaunchAsync(string summary, string advancedCommand)
    {
        ConfirmSummary = summary;
        ConfirmAdvancedCommand = advancedCommand;
        ConfirmShowAdvanced = false;
        IsConfirmOpen = true;

        _pendingConfirm = new TaskCompletionSource<bool>();
        return _pendingConfirm.Task;
    }

    [RelayCommand] private void ConfirmYes() { IsConfirmOpen = false; _pendingConfirm?.TrySetResult(true); }
    [RelayCommand] private void ConfirmNo() { IsConfirmOpen = false; _pendingConfirm?.TrySetResult(false); }
    [RelayCommand] private void ToggleConfirmAdvanced() => ConfirmShowAdvanced = !ConfirmShowAdvanced;

    [RelayCommand]
    private void CopyAdvancedCommand()
    {
        // La clipboard su Avalonia richiede l'accesso al TopLevel: gestita nel code-behind
        // della finestra principale tramite un evento, per restare indipendenti dal framework qui.
        CopyRequested?.Invoke(ConfirmAdvancedCommand);
    }

    /// <summary>Sollevato quando l'utente chiede di copiare il comando avanzato: il code-behind
    /// della view si iscrive per usare l'API clipboard di Avalonia (che richiede un TopLevel).</summary>
    public event Action<string>? CopyRequested;

    [RelayCommand] private void GoHome() => CurrentScreen = AppScreen.Home;
    [RelayCommand] private void GoLocalSetup() => CurrentScreen = AppScreen.LocalSetup;
    [RelayCommand] private void GoMultiplayerSetup() => CurrentScreen = AppScreen.MultiplayerSetup;
    [RelayCommand] private void GoSettings() => CurrentScreen = AppScreen.Settings;
    [RelayCommand] private void GoDiagnostics() => CurrentScreen = AppScreen.Diagnostics;
    [RelayCommand] private void GoHelp() => CurrentScreen = AppScreen.Help;
    [RelayCommand] private void GoCustomize() => CurrentScreen = AppScreen.Customize;
    [RelayCommand] private void GoPlayers() => CurrentScreen = AppScreen.Players;
    [RelayCommand] private void GoKeyBindings() => CurrentScreen = AppScreen.KeyBindings;
    [RelayCommand] private void GoPlayerProfile() => CurrentScreen = AppScreen.PlayerProfile;
    [RelayCommand] private void DismissBanner() => BannerMessage = null;

    public void SaveSettingsQuiet()
    {
        try { _settingsService.Save(Settings); }
        catch { /* salvataggio impostazioni non critico per la sessione corrente */ }
    }
}
