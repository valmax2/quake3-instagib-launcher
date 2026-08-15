using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Models;
using Quake3InstaGibLauncher.Services;

namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>
/// ViewModel radice: gestisce la navigazione tra schermate, lo stato condiviso
/// dell'installazione/mappe, il dialogo di conferma prima dell'avvio e orchestra i servizi.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly GameInstallationService _installationService;
    private readonly MapCacheService _mapCacheService;
    private readonly LaunchService _launchService;
    private readonly RotationCfgService _rotationCfgService;

    private readonly List<MapCardViewModel> _allMapCards = new();
    private TaskCompletionSource<bool>? _pendingConfirm;

    public AppSettings Settings { get; private set; }

    [ObservableProperty] private AppScreen _currentScreen = AppScreen.Home;
    [ObservableProperty] private AppLanguage _currentLanguage = AppLanguage.Italian;
    [ObservableProperty] private bool _uiSoundsEnabled = true;
    [ObservableProperty] private InstallationStatus? _installationStatus;
    [ObservableProperty] private string? _bannerMessage;
    [ObservableProperty] private bool _bannerIsError;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyMessage = string.Empty;

    // --- Dialogo di conferma pre-avvio ---
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

    private InstallationPaths CurrentPaths => _installationService.ResolvePaths(Settings.GameRootPath);

    public MainViewModel(
        SettingsService settingsService,
        GameInstallationService installationService,
        MapCacheService mapCacheService,
        LaunchService launchService,
        RotationCfgService rotationCfgService)
    {
        _settingsService = settingsService;
        _installationService = installationService;
        _mapCacheService = mapCacheService;
        _launchService = launchService;
        _rotationCfgService = rotationCfgService;

        Settings = _settingsService.Load();
        ThemeService.Apply(Settings.Theme);
        LocalizationService.Apply(Settings.Language);
        _currentLanguage = Settings.Language;

        // Primo avvio (o aggiornamento da una versione precedente senza questo campo): rileva la
        // risoluzione reale del monitor primario, cosi' il fix delle barre nere laterali funziona
        // gia' "di serie" senza che l'utente debba impostarla a mano.
        if (Settings.ScreenWidth <= 0 || Settings.ScreenHeight <= 0)
        {
            var (width, height) = DisplayInfo.GetPrimaryScreenResolution();
            if (width > 0 && height > 0)
            {
                Settings.ScreenWidth = width;
                Settings.ScreenHeight = height;
                _settingsService.Save(Settings); // salva subito: niente stato "rilevato ma non ancora salvato"
            }
        }

        CustomizeVm = new CustomizeViewModel(Settings.Theme, SaveSettingsQuiet, onUiSoundsChanged: value => UiSoundsEnabled = value);
        PlayersVm = new PlayersViewModel(Settings, SaveSettingsQuiet);
        KeyBindingsVm = new KeyBindingsViewModel(Settings, SaveSettingsQuiet);
        PlayerProfileVm = new PlayerProfileViewModel(Settings, SaveSettingsQuiet);
        _uiSoundsEnabled = Settings.Theme.UiSoundsEnabled;

        SettingsVm = new SettingsViewModel(
            Settings,
            applyNewGameRoot: ApplyNewGameRootAsync,
            refreshMaps: () => RefreshMapsAsync(),
            clearCache: () => _mapCacheService.ClearCache(),
            resetToDefaults: ResetSettingsToDefaults,
            openFolder: OpenFolder,
            saveSettings: SaveSettingsQuiet);

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
            saveSettings: SaveSettingsQuiet,
            playersVm: PlayersVm);
    }

    public async Task InitializeAsync()
    {
        RevalidateInstallation();

        if (InstallationStatus is { IsValid: false })
        {
            // Installazione non trovata al percorso configurato: invece di lasciare l'utente
            // davanti a una Home con un semplice pallino rosso, lo portiamo direttamente alle
            // Impostazioni per scegliere la cartella corretta di ioquake3.
            CurrentScreen = AppScreen.Settings;
            ShowBanner(
                $"Non trovo l'installazione di ioquake3 in \"{Settings.GameRootPath}\". " +
                "Seleziona qui sotto la cartella corretta (quella che contiene ioquake3.x86_64.exe) e premi \"Applica percorso e verifica\".",
                isError: true);
            return;
        }

        var cached = _mapCacheService.TryLoadFromDisk();
        if (cached is not null)
        {
            PopulateMapCards(cached);
        }
        else
        {
            await RefreshMapsAsync();
        }
    }

    private void RevalidateInstallation()
    {
        InstallationStatus = _installationService.Validate(Settings.GameRootPath);
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
        bool? requiresMissionPack = null;

        foreach (var name in mapNames)
        {
            var card = _allMapCards.FirstOrDefault(m =>
                string.Equals(m.TechnicalName, name, StringComparison.OrdinalIgnoreCase));
            if (card is null) continue;

            // Difesa in profondita': una rotazione salvata PRIMA di questo controllo (settings.json
            // di una sessione precedente) potrebbe gia' mischiare mappe missionpack e normali, il
            // che causa un crash a meta' partita (vedi MapGalleryPanelViewModel.AreCompatible).
            // Qui la "ripuliamo" tenendo solo le mappe compatibili con la prima della lista, invece
            // di ripropagare silenziosamente una rotazione che romperebbe la prossima partita.
            requiresMissionPack ??= card.Info.RequiresMissionPackBaseGame;
            if (card.Info.RequiresMissionPackBaseGame != requiresMissionPack)
                continue;

            gallery.RotationMaps.Add(card);
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

    private async Task ApplyNewGameRootAsync(string newPath)
    {
        Settings.GameRootPath = newPath;
        RevalidateInstallation();
        SaveSettingsQuiet();

        if (InstallationStatus is { IsValid: false })
        {
            ShowBanner(
                $"Percorso non valido: mancano {InstallationStatus.MissingItems.Count} elemento/i (vedi Diagnostica per i dettagli).",
                isError: true);
            return;
        }

        ShowBanner("Installazione trovata correttamente.", isError: false);
        await RefreshMapsAsync();
        CurrentScreen = AppScreen.Home;
    }

    private void ResetSettingsToDefaults()
    {
        _settingsService.ResetToDefaults();
        var fresh = new AppSettings();

        Settings.GameRootPath = fresh.GameRootPath;
        Settings.Fov = fresh.Fov;
        SettingsVm.GameRootPath = fresh.GameRootPath;
        SettingsVm.Fov = fresh.Fov;

        ShowBanner("Impostazioni ripristinate. I valori di partita locale/multiplayer torneranno predefiniti al prossimo avvio dell'app.", isError: false);
        SaveSettingsQuiet();
    }

    private void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            ShowBanner($"Cartella non trovata: {path}", isError: true);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", ArgumentList = { path }, UseShellExecute = false });
        }
        catch (Exception ex)
        {
            ShowBanner($"Impossibile aprire la cartella: {ex.Message}", isError: true);
        }
    }

    private void OnLaunched(LaunchOutcome outcome)
    {
        if (!outcome.Success)
            ShowBanner(outcome.ErrorMessage ?? "Avvio non riuscito.", isError: true);
        else
            ShowBanner("ioquake3 avviato correttamente.", isError: false);
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

    [RelayCommand]
    private void ConfirmYes()
    {
        IsConfirmOpen = false;
        _pendingConfirm?.TrySetResult(true);
    }

    [RelayCommand]
    private void ConfirmNo()
    {
        IsConfirmOpen = false;
        _pendingConfirm?.TrySetResult(false);
    }

    [RelayCommand]
    private void ToggleConfirmAdvanced() => ConfirmShowAdvanced = !ConfirmShowAdvanced;

    [RelayCommand]
    private void CopyAdvancedCommand()
    {
        try { Clipboard.SetText(ConfirmAdvancedCommand); }
        catch { /* clipboard occupato da un altro processo: non bloccante */ }
    }

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

    [RelayCommand]
    private void ToggleUiSounds()
    {
        // CustomizeVm resta l'unico punto che scrive davvero su UiThemeSettings/disco (CommitAndApply):
        // qui ci limitiamo a cambiare la sua proprieta', che a sua volta richiama il callback sopra
        // per tenere sincronizzata l'icona rapida nella barra di navigazione.
        CustomizeVm.UiSoundsEnabled = !UiSoundsEnabled;
    }

    [RelayCommand]
    private void SetLanguage(AppLanguage language)
    {
        if (CurrentLanguage == language) return;

        CurrentLanguage = language;
        Settings.Language = language;
        LocalizationService.Apply(language);
        SaveSettingsQuiet();
    }

    [RelayCommand]
    private void DismissBanner() => BannerMessage = null;

    public void SaveSettingsQuiet()
    {
        try { _settingsService.Save(Settings); }
        catch { /* salvataggio impostazioni non critico per la sessione corrente */ }
    }
}
