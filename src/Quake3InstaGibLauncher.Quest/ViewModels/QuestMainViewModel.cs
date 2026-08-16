using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Quest.Models;
using Quake3InstaGibLauncher.Quest.Services;

namespace Quake3InstaGibLauncher.Quest.ViewModels;

public enum QuestTab { Multiplayer, Lan, Bot, Profile }

public sealed record ModelColorOption(int Value, string Name);

/// <summary>
/// ViewModel unico dell'app: tre "schede" (Multiplayer/Bot/Personaggio) pensate per essere usate
/// col puntatore laser del controller Quest. Riusa il piu' possibile il Core gia' collaudato sul
/// launcher desktop (Quake3ServerBrowser, Pk3Scanner, CommandBuilder, PlayerProfile) invece di
/// reinventare la logica: stesso comportamento, stessi cvar, gia' testati.
/// </summary>
public partial class QuestMainViewModel : ObservableObject
{
    private readonly Quake3ServerBrowser _browser = new();
    private readonly QuestLaunchService _launchService = new();
    private readonly QuestSettingsService _settingsService = new();
    private readonly QuestAppSettings _appSettings;

    private List<ServerInfo> _allFoundServers = new();

    // ===================== Navigazione a schede =====================
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMultiplayerTab))]
    [NotifyPropertyChangedFor(nameof(IsLanTab))]
    [NotifyPropertyChangedFor(nameof(IsBotTab))]
    [NotifyPropertyChangedFor(nameof(IsProfileTab))]
    private QuestTab _activeTab = QuestTab.Multiplayer;

    public bool IsMultiplayerTab => ActiveTab == QuestTab.Multiplayer;
    public bool IsLanTab => ActiveTab == QuestTab.Lan;
    public bool IsBotTab => ActiveTab == QuestTab.Bot;
    public bool IsProfileTab => ActiveTab == QuestTab.Profile;

    [RelayCommand] private void ShowMultiplayerTab() => ActiveTab = QuestTab.Multiplayer;
    [RelayCommand] private void ShowLanTab() => ActiveTab = QuestTab.Lan;
    [RelayCommand] private void ShowBotTab() => ActiveTab = QuestTab.Bot;
    [RelayCommand] private void ShowProfileTab() => ActiveTab = QuestTab.Profile;

    // ===================== Scheda Multiplayer: ricerca e filtri =====================
    [ObservableProperty] private ObservableCollection<ServerInfo> _servers = new();
    [ObservableProperty] private ServerInfo? _selectedServer;
    [ObservableProperty] private string _statusMessage = "Premi \"Cerca partite\" per trovare server attivi.";
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _hasStorageAccess;

    [ObservableProperty] private bool _filterOnlyInstaGib = true;
    [ObservableProperty] private bool _filterHideFull = true;
    [ObservableProperty] private bool _filterHideEmpty;

    /// <summary>Null = "Tutte le modalita'". Usato solo quando FilterOnlyInstaGib e' spento, dato
    /// che i server InstaGib in genere non pubblicano un g_gametype affidabile per il filtro.</summary>
    [ObservableProperty] private GameType? _filterGameType;

    public IReadOnlyList<GameType> AvailableGameTypes { get; } = Enum.GetValues<GameType>();

    /// <summary>Come AvailableGameTypes ma con in testa un'opzione "Tutte le modalita'" (null):
    /// serve solo al ComboBox del filtro Internet, che deve poter tornare a "nessun filtro modalita'".</summary>
    public IReadOnlyList<GameType?> AvailableGameTypeFilters { get; } =
        new GameType?[] { null }.Concat(Enum.GetValues<GameType>().Cast<GameType?>()).ToList();

    partial void OnFilterOnlyInstaGibChanged(bool value) => ApplyFilters();
    partial void OnFilterHideFullChanged(bool value) => ApplyFilters();
    partial void OnFilterHideEmptyChanged(bool value) => ApplyFilters();
    partial void OnFilterGameTypeChanged(GameType? value) => ApplyFilters();

    // ===================== Scheda LAN (partita rapida PC<->Quest sulla stessa rete) =====================
    [ObservableProperty] private ObservableCollection<ServerInfo> _lanServers = new();
    [ObservableProperty] private bool _isSearchingLan;
    [ObservableProperty] private string _lanStatusMessage = "Assicurati che PC e Quest siano sullo stesso WiFi, poi premi \"Cerca in LAN\".";

    [RelayCommand(CanExecute = nameof(CanSearchLan))]
    private async Task SearchLanAsync()
    {
        IsSearchingLan = true;
        LanStatusMessage = "Ricerca sulla rete locale...";

        try
        {
            // Timeout breve apposta: sulla stessa rete locale i server rispondono quasi subito,
            // niente a che vedere con i tempi di un master server su internet.
            var found = await _browser.SearchLanAsync(timeout: TimeSpan.FromSeconds(2));
            LanServers = new ObservableCollection<ServerInfo>(found.OrderByDescending(s => s.Players));

            LanStatusMessage = LanServers.Count > 0
                ? $"{LanServers.Count} partita/e trovate in LAN."
                : "Nessuna partita trovata in LAN. Controlla che sul PC sia stata avviata una partita in modalita' \"Solo rete locale\".";
        }
        catch (Exception ex)
        {
            LanStatusMessage = $"Ricerca LAN fallita: {ex.Message}";
        }
        finally
        {
            IsSearchingLan = false;
        }
    }

    private bool CanSearchLan() => !IsSearchingLan;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void PlayLan(ServerInfo? server)
    {
        server ??= SelectedServer;
        if (server is null) return;

        RefreshStorageAccessStatus();
        var outcome = _launchService.PrepareAndJoin(server, CurrentProfile);
        LanStatusMessage = outcome.Message;
    }

    // ===================== Scheda Bot (allenamento in locale) =====================
    [ObservableProperty] private ObservableCollection<MapInfo> _availableMaps = new();

    // NotifyCanExecuteChangedFor e' necessario qui (a differenza di Play/PlayLan): il pulsante
    // "AVVIA PARTITA CONTRO BOT" non ha un IsEnabled bindato a parte, si affida al solo
    // Command.CanExecute di Avalonia per abilitarsi/disabilitarsi - senza questo attributo
    // restava disabilitato per sempre dopo la primissima scansione mappe (SelectedMap passava da
    // null a un valore, ma nessuno lo segnalava al bottone).
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBotMatchCommand))]
    private MapInfo? _selectedMap;
    [ObservableProperty] private int _botCount = 6;
    [ObservableProperty] private int _botSkill = 3;
    [ObservableProperty] private GameType _botGameType = GameType.FreeForAll;
    [ObservableProperty] private bool _botUseInstaGibMod = true;
    [ObservableProperty] private int _botFragLimit = 30;
    [ObservableProperty] private int _botTimeLimit = 15;
    [ObservableProperty] private string _botStatusMessage = "Premi \"Aggiorna elenco mappe\" la prima volta.";

    public IReadOnlyList<int> AvailableBotCounts { get; } = Enumerable.Range(1, 8).ToList();
    public IReadOnlyList<int> AvailableBotSkills { get; } = Enumerable.Range(1, 5).ToList();

    // ===================== Scheda Personaggio =====================
    [ObservableProperty] private string _profileColoredName;
    [ObservableProperty] private int _profileCrosshairStyle;
    [ObservableProperty] private int _profileCrosshairSize;
    [ObservableProperty] private string _profileCrosshairColorHex;
    [ObservableProperty] private bool _profileCrosshairHealthColor;
    [ObservableProperty] private bool _profileCrosshairPulseOnHit;
    [ObservableProperty] private int _profileModelColor1;
    [ObservableProperty] private int _profileModelColor2;

    // Record dedicato invece di una tupla: il binding XAML di Avalonia non risolve in modo
    // affidabile i nomi "Value"/"Name" di un ValueTuple (sono solo metadati del compilatore C#,
    // non veri membri riflettibili) - stesso identico pattern gia' usato dal launcher desktop
    // (ModelColorOption in PlayerProfileViewModel.cs).
    public IReadOnlyList<ModelColorOption> AvailableModelColors { get; } = new[]
    {
        new ModelColorOption(1, "Bianco"), new ModelColorOption(2, "Rosso"), new ModelColorOption(3, "Verde"), new ModelColorOption(4, "Blu"),
        new ModelColorOption(5, "Giallo"), new ModelColorOption(6, "Grigio"), new ModelColorOption(7, "Viola"), new ModelColorOption(8, "Fucsia"),
    };

    public IReadOnlyList<int> AvailableCrosshairStyles { get; } = Enumerable.Range(1, 15).ToList();

    partial void OnProfileColoredNameChanged(string value) => SaveProfile(p => p.ColoredName = value);
    partial void OnProfileCrosshairStyleChanged(int value) => SaveProfile(p => p.CrosshairStyle = value);
    partial void OnProfileCrosshairSizeChanged(int value) => SaveProfile(p => p.CrosshairSize = value);
    partial void OnProfileCrosshairColorHexChanged(string value) => SaveProfile(p => p.CrosshairColorHex = value);
    partial void OnProfileCrosshairHealthColorChanged(bool value) => SaveProfile(p => p.CrosshairHealthColor = value);
    partial void OnProfileCrosshairPulseOnHitChanged(bool value) => SaveProfile(p => p.CrosshairPulseOnHit = value);
    partial void OnProfileModelColor1Changed(int value) => SaveProfile(p => p.ModelColor1 = value);
    partial void OnProfileModelColor2Changed(int value) => SaveProfile(p => p.ModelColor2 = value);

    public QuestMainViewModel()
    {
        _appSettings = _settingsService.Load();
        var p = _appSettings.PlayerProfile;
        _profileColoredName = p.ColoredName;
        _profileCrosshairStyle = p.CrosshairStyle;
        _profileCrosshairSize = p.CrosshairSize;
        _profileCrosshairColorHex = p.CrosshairColorHex;
        _profileCrosshairHealthColor = p.CrosshairHealthColor;
        _profileCrosshairPulseOnHit = p.CrosshairPulseOnHit;
        _profileModelColor1 = p.ModelColor1;
        _profileModelColor2 = p.ModelColor2;

        RefreshStorageAccessStatus();
    }

    private PlayerProfile CurrentProfile => new()
    {
        ColoredName = ProfileColoredName,
        CrosshairStyle = ProfileCrosshairStyle,
        CrosshairSize = ProfileCrosshairSize,
        CrosshairColorHex = ProfileCrosshairColorHex,
        CrosshairHealthColor = ProfileCrosshairHealthColor,
        CrosshairPulseOnHit = ProfileCrosshairPulseOnHit,
        ModelColor1 = ProfileModelColor1,
        ModelColor2 = ProfileModelColor2,
    };

    private void SaveProfile(Action<PlayerProfile> apply)
    {
        apply(_appSettings.PlayerProfile);
        _settingsService.Save(_appSettings);
    }

    public void RefreshStorageAccessStatus() => HasStorageAccess = _launchService.HasStorageAccess();

    [RelayCommand]
    private void RequestStorageAccess() => _launchService.RequestStorageAccess();

    // ===================== Comandi Multiplayer =====================
    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        IsSearching = true;
        StatusMessage = "Ricerca server in corso...";

        try
        {
            _allFoundServers = (await _browser.SearchInternetAsync(detailsTimeout: TimeSpan.FromSeconds(6))).ToList();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ricerca fallita: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private bool CanSearch() => !IsSearching;

    private void ApplyFilters()
    {
        var query = _allFoundServers.AsEnumerable();

        if (FilterOnlyInstaGib)
            query = query.Where(s => s.IsLikelyInstaGib);
        else if (FilterGameType is { } gameType)
            query = query.Where(s => s.ParsedGameType == gameType);

        if (FilterHideFull)
            query = query.Where(s => !s.IsFull);
        if (FilterHideEmpty)
            query = query.Where(s => !s.IsEmpty);

        var result = query.OrderByDescending(s => s.Players).ToList();
        Servers = new ObservableCollection<ServerInfo>(result);

        StatusMessage = _allFoundServers.Count == 0
            ? "Nessuna ricerca ancora effettuata."
            : result.Count > 0
                ? $"{result.Count} server (su {_allFoundServers.Count} trovati)."
                : "Nessun server corrisponde ai filtri attuali.";
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play(ServerInfo? server)
    {
        server ??= SelectedServer;
        if (server is null) return;

        RefreshStorageAccessStatus();
        var outcome = _launchService.PrepareAndJoin(server, CurrentProfile);
        StatusMessage = outcome.Message;
    }

    private bool CanPlay(ServerInfo? server) => (server ?? SelectedServer) is not null;

    // ===================== Comandi Bot =====================
    [RelayCommand]
    private void RefreshMaps()
    {
        try
        {
            var result = _launchService.ScanMaps();
            AvailableMaps = new ObservableCollection<MapInfo>(result.Maps.OrderBy(m => m.TechnicalName));
            SelectedMap ??= AvailableMaps.FirstOrDefault();

            BotStatusMessage = AvailableMaps.Count > 0
                ? $"{AvailableMaps.Count} mappe trovate."
                : "Nessuna mappa trovata: apri prima Quake3Quest almeno una volta.";
        }
        catch (Exception ex)
        {
            BotStatusMessage = $"Scansione mappe fallita: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartBotMatch))]
    private void StartBotMatch()
    {
        if (SelectedMap is null)
        {
            BotStatusMessage = "Scegli prima una mappa.";
            return;
        }

        RefreshStorageAccessStatus();

        var options = new LocalMatchOptions
        {
            MapTechnicalName = SelectedMap.TechnicalName,
            MapSource = SelectedMap.Source,
            RequiresMissionPackBaseGame = SelectedMap.RequiresMissionPackBaseGame,
            TotalPlayers = BotCount + 1,
            BotSkill = BotSkill,
            FragLimit = BotFragLimit,
            TimeLimit = BotTimeLimit,
            Fov = 100,
            GameType = BotGameType,
            UseInstaGibMod = BotUseInstaGibMod,
            SvPureOff = true,
            FixAspectRatio = false, // vedi commento equivalente in QuestLaunchService.PrepareAndJoin
            Player = CurrentProfile,
        };

        var outcome = _launchService.PrepareAndLaunchBotMatch(options);
        BotStatusMessage = outcome.Message;
    }

    private bool CanStartBotMatch() => SelectedMap is not null;
}
