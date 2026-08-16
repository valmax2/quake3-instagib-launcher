using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Quest.Models;
using Quake3InstaGibLauncher.Quest.Services;

namespace Quake3InstaGibLauncher.Quest.ViewModels;

public enum QuestTab { Multiplayer, Lan, Bot, Host, Profile }

public sealed record ModelColorOption(int Value, string Name);

/// <summary>Uno degli 8 codici colore standard Quake III (^0-^7) mostrato come pulsante-palette
/// cliccabile nella scheda Personaggio, per comporre il nome colorato senza dover ricordare a
/// memoria i codici - stessa idea della palette del launcher desktop.</summary>
public sealed record NameColorSwatch(string Code, string HexColor);

/// <summary>Una mappa nella galleria della scheda Bot, con l'anteprima (levelshot) gia' decodificata
/// se disponibile. IsSelected e' locale alla UI (evidenzia la card scelta), non persiste.
/// IsFavorite invece riflette QuestAppSettings.FavoriteMapTechnicalNames e viene salvato.</summary>
public sealed partial class MapPickerItem : ObservableObject
{
    public MapInfo Map { get; }
    public Bitmap? Preview { get; }
    public bool HasPreview => Preview is not null;

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isFavorite;

    public MapPickerItem(MapInfo map, Bitmap? preview, bool isFavorite)
    {
        Map = map;
        Preview = preview;
        _isFavorite = isFavorite;
    }
}

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
    private readonly QuestMapPreviewService _mapPreviewService = new();
    private readonly QuestMapCacheService _mapCacheService = new();
    private readonly QuestAppSettings _appSettings;

    private List<ServerInfo> _allFoundServers = new();

    // ===================== Navigazione a schede =====================
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMultiplayerTab))]
    [NotifyPropertyChangedFor(nameof(IsLanTab))]
    [NotifyPropertyChangedFor(nameof(IsBotTab))]
    [NotifyPropertyChangedFor(nameof(IsHostTab))]
    [NotifyPropertyChangedFor(nameof(IsProfileTab))]
    private QuestTab _activeTab = QuestTab.Multiplayer;

    public bool IsMultiplayerTab => ActiveTab == QuestTab.Multiplayer;
    public bool IsLanTab => ActiveTab == QuestTab.Lan;
    public bool IsBotTab => ActiveTab == QuestTab.Bot;
    public bool IsHostTab => ActiveTab == QuestTab.Host;
    public bool IsProfileTab => ActiveTab == QuestTab.Profile;

    [RelayCommand] private void ShowMultiplayerTab() => ActiveTab = QuestTab.Multiplayer;
    [RelayCommand] private void ShowLanTab() => ActiveTab = QuestTab.Lan;
    [RelayCommand] private void ShowBotTab() => ActiveTab = QuestTab.Bot;
    [RelayCommand] private void ShowHostTab() => ActiveTab = QuestTab.Host;
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
        var outcome = _launchService.PrepareAndJoin(server, CurrentProfile, VideoFov, _appSettings.Video);
        LanStatusMessage = outcome.Message;
    }

    // ===================== Scheda Bot (allenamento in locale) =====================
    [ObservableProperty] private ObservableCollection<MapPickerItem> _availableMapItems = new();

    // NotifyCanExecuteChangedFor e' necessario qui (a differenza di Play/PlayLan): il pulsante
    // "AVVIA PARTITA CONTRO BOT" non ha un IsEnabled bindato a parte, si affida al solo
    // Command.CanExecute di Avalonia per abilitarsi/disabilitarsi - senza questo attributo
    // restava disabilitato per sempre dopo la primissima scansione mappe (SelectedMap passava da
    // null a un valore, ma nessuno lo segnalava al bottone).
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBotMatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartHostCommand))]
    private MapInfo? _selectedMap;
    [ObservableProperty] private int _botCount = 6;
    [ObservableProperty] private int _botSkill = 3;
    [ObservableProperty] private GameType _botGameType = GameType.FreeForAll;
    [ObservableProperty] private bool _botUseInstaGibMod = true;
    [ObservableProperty] private int _botFragLimit = 30;
    [ObservableProperty] private int _botTimeLimit = 15;
    [ObservableProperty] private string _botStatusMessage = "Scansione mappe...";
    [ObservableProperty] private bool _showOnlyFavoriteMaps;

    public IReadOnlyList<int> AvailableBotCounts { get; } = Enumerable.Range(1, 8).ToList();
    public IReadOnlyList<int> AvailableBotSkills { get; } = Enumerable.Range(1, 5).ToList();

    // ===================== Scheda Impostazioni video (FOV, anti-aliasing, luminosita') =====================
    [ObservableProperty] private int _videoFov = 100;
    [ObservableProperty] private int _videoAntiAliasing; // 0 = disattivato
    [ObservableProperty] private double _videoGamma = 1.0;

    public IReadOnlyList<int> AvailableAntiAliasingLevels { get; } = new[] { 0, 2, 4, 8 };

    partial void OnVideoFovChanged(int value) => SaveVideo(v => v.Fov = value);
    partial void OnVideoAntiAliasingChanged(int value) => SaveVideo(v => v.AntiAliasingSamples = value == 0 ? null : value);
    partial void OnVideoGammaChanged(double value) => SaveVideo(v => v.Gamma = value);

    private void SaveVideo(Action<VideoSettings> apply)
    {
        apply(_appSettings.Video);
        _settingsService.Save(_appSettings);
    }

    // ===================== Scheda Personaggio =====================
    [ObservableProperty] private string _profileColoredName;
    [ObservableProperty] private int _profileCrosshairStyle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CrosshairPreviewArmLength))]
    [NotifyPropertyChangedFor(nameof(CrosshairPreviewThickness))]
    [NotifyPropertyChangedFor(nameof(CrosshairPreviewDotSize))]
    private int _profileCrosshairSize;

    [ObservableProperty] private string _profileCrosshairColorHex;
    [ObservableProperty] private bool _profileCrosshairHealthColor;
    [ObservableProperty] private bool _profileCrosshairPulseOnHit;
    [ObservableProperty] private int _profileModelColor1;
    [ObservableProperty] private int _profileModelColor2;

    /// <summary>Anteprima procedurale del mirino (una croce + puntino centrale, forma originale non
    /// tratta dagli asset del gioco): mostra davvero taglia/colore scelti invece di far scegliere
    /// alla cieca un numero di stile. Lo stile esatto (1-15) verra' comunque applicato in gioco.</summary>
    public double CrosshairPreviewArmLength => Math.Clamp(ProfileCrosshairSize, 4, 96) * 1.4;
    public double CrosshairPreviewThickness => Math.Max(2, ProfileCrosshairSize / 10.0);
    public double CrosshairPreviewDotSize => Math.Max(3, ProfileCrosshairSize / 7.0);

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

    /// <summary>Gli 8 codici colore standard Quake III, colori reali approssimati per lo swatch.</summary>
    public IReadOnlyList<NameColorSwatch> AvailableNameColors { get; } = new[]
    {
        new NameColorSwatch("0", "#1A1A1A"), new NameColorSwatch("1", "#E33"),
        new NameColorSwatch("2", "#3C3"), new NameColorSwatch("3", "#DD3"),
        new NameColorSwatch("4", "#33E"), new NameColorSwatch("5", "#3CC"),
        new NameColorSwatch("6", "#D3D"), new NameColorSwatch("7", "#EEE"),
    };

    /// <summary>Palette rapida per il colore del mirino: colori comuni/molto visibili in gioco,
    /// non gli 8 codici Quake (il mirino usa un vero colore RGB, non un codice ^N).</summary>
    public IReadOnlyList<string> AvailableCrosshairColors { get; } = new[]
    {
        "#FFFFFF", "#FF3333", "#33FF33", "#33CCFF", "#FFFF33", "#FF33FF", "#000000",
    };

    [RelayCommand]
    private void AppendNameColor(string code) => ProfileColoredName += $"^{code}";

    [RelayCommand]
    private void SetCrosshairColor(string hex) => ProfileCrosshairColorHex = hex;

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

        var bot = _appSettings.LastBotMatch;
        _botCount = bot.BotCount;
        _botSkill = bot.BotSkill;
        _botGameType = bot.GameType;
        _botUseInstaGibMod = bot.UseInstaGibMod;
        _botFragLimit = bot.FragLimit;
        _botTimeLimit = bot.TimeLimit;

        var video = _appSettings.Video;
        _videoFov = video.Fov;
        _videoAntiAliasing = video.AntiAliasingSamples ?? 0;
        _videoGamma = video.Gamma ?? 1.0;

        RefreshStorageAccessStatus();

        // Le mappe si caricano da sole all'avvio (dalla cache se disponibile, altrimenti
        // scansionando i .pk3 una volta sola): l'utente non deve premere nulla per vederle,
        // a differenza di prima dove la galleria restava vuota finche' non si toccava
        // "Aggiorna elenco mappe".
        _ = LoadMapsAsync(forceRescan: false);
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
        var outcome = _launchService.PrepareAndJoin(server, CurrentProfile, VideoFov, _appSettings.Video);
        StatusMessage = outcome.Message;
    }

    private bool CanPlay(ServerInfo? server) => (server ?? SelectedServer) is not null;

    // ===================== Comandi Bot =====================
    [ObservableProperty] private bool _isScanningMaps;

    /// <summary>Vista filtrata di AvailableMapItems quando "Solo preferite" e' attivo. La galleria
    /// nella UI si aggancia a questa, non direttamente ad AvailableMapItems.</summary>
    public IEnumerable<MapPickerItem> VisibleMapItems =>
        ShowOnlyFavoriteMaps ? AvailableMapItems.Where(i => i.IsFavorite) : AvailableMapItems;

    partial void OnShowOnlyFavoriteMapsChanged(bool value) => OnPropertyChanged(nameof(VisibleMapItems));

    [RelayCommand(CanExecute = nameof(CanRefreshMaps))]
    private Task RefreshMapsAsync() => LoadMapsAsync(forceRescan: true);

    private bool CanRefreshMaps() => !IsScanningMaps;

    /// <summary>Carica le mappe: usa la cache su disco se le cartelle .pk3 non sono cambiate
    /// dall'ultima scansione (avvio istantaneo), altrimenti riscansiona i .pk3 davvero.
    /// forceRescan=true (tasto "Aggiorna elenco mappe") ignora la cache e riscansiona comunque.</summary>
    private async Task LoadMapsAsync(bool forceRescan)
    {
        IsScanningMaps = true;
        BotStatusMessage = "Carico le mappe...";

        try
        {
            var directories = new[] { _launchService.BaseGamePath, _launchService.InstaGibModPath };
            var fingerprint = await Task.Run(() => QuestMapCacheService.ComputeFingerprint(directories));

            var cachedMaps = forceRescan ? null : _mapCacheService.TryLoad(fingerprint);
            var usedCache = cachedMaps is not null;

            List<MapInfo> maps;
            if (cachedMaps is not null)
            {
                maps = cachedMaps;
            }
            else
            {
                BotStatusMessage = "Nessuna mappa in memoria o trovate mappe nuove: scansione dei file di gioco in corso (puo' richiedere qualche secondo)...";

                // Fuori dal thread UI: con i pacchetti mappe community (es. q3wpak0-4) si arriva
                // facilmente a un centinaio di mappe, e decodificare tutte le anteprime in modo
                // sincrono sul thread UI congelerebbe visibilmente l'app per diversi secondi.
                maps = await Task.Run(() =>
                {
                    var result = _launchService.ScanMaps();
                    var scanned = result.Maps.OrderBy(m => m.TechnicalName).ToList();

                    foreach (var m in scanned)
                        m.CachedPreviewPath = _mapPreviewService.GetOrCreatePreviewPath(m);

                    return scanned;
                });

                _mapCacheService.Save(fingerprint, maps);
            }

            var favorites = _appSettings.FavoriteMapTechnicalNames;
            var items = maps.Select(m =>
            {
                Bitmap? preview = null;
                if (m.CachedPreviewPath is not null)
                {
                    try { preview = new Bitmap(m.CachedPreviewPath); }
                    catch { /* file di cache corrotto/rimosso: si mostra il segnaposto */ }
                }
                return new MapPickerItem(m, preview, favorites.Contains(m.TechnicalName));
            }).ToList();

            AvailableMapItems = new ObservableCollection<MapPickerItem>(items);
            OnPropertyChanged(nameof(VisibleMapItems));

            var toSelect = AvailableMapItems.FirstOrDefault(i => i.Map.TechnicalName == (SelectedMap?.TechnicalName ?? _appSettings.LastBotMatch.MapTechnicalName))
                ?? AvailableMapItems.FirstOrDefault();
            SelectMap(toSelect);

            BotStatusMessage = AvailableMapItems.Count == 0
                ? "Nessuna mappa trovata: apri prima Quake3Quest almeno una volta."
                : usedCache
                    ? $"{AvailableMapItems.Count} mappe (dalla memoria)."
                    : $"{AvailableMapItems.Count} mappe trovate.";
        }
        catch (Exception ex)
        {
            BotStatusMessage = $"Caricamento mappe fallito: {ex.Message}";
        }
        finally
        {
            IsScanningMaps = false;
        }
    }

    [RelayCommand]
    private void SelectMap(MapPickerItem? item)
    {
        foreach (var existing in AvailableMapItems)
            existing.IsSelected = ReferenceEquals(existing, item);

        SelectedMap = item?.Map;
    }

    [RelayCommand]
    private void ToggleFavoriteMap(MapPickerItem? item)
    {
        if (item is null) return;

        item.IsFavorite = !item.IsFavorite;

        var favorites = _appSettings.FavoriteMapTechnicalNames;
        if (item.IsFavorite && !favorites.Contains(item.Map.TechnicalName))
            favorites.Add(item.Map.TechnicalName);
        else if (!item.IsFavorite)
            favorites.Remove(item.Map.TechnicalName);

        _settingsService.Save(_appSettings);
        OnPropertyChanged(nameof(VisibleMapItems));
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
            Fov = VideoFov,
            GameType = BotGameType,
            UseInstaGibMod = BotUseInstaGibMod,
            SvPureOff = true,
            FixAspectRatio = false, // vedi commento equivalente in QuestLaunchService.PrepareAndJoin
            Player = CurrentProfile,
        };

        var outcome = _launchService.PrepareAndLaunchBotMatch(options, _appSettings.Video);
        BotStatusMessage = outcome.Message;

        if (outcome.Success)
        {
            // Ricorda questa configurazione per la prossima volta (richiesta esplicita
            // dell'utente: "non tiene in memoria manco una impostazione").
            var last = _appSettings.LastBotMatch;
            last.MapTechnicalName = SelectedMap.TechnicalName;
            last.BotCount = BotCount;
            last.BotSkill = BotSkill;
            last.GameType = BotGameType;
            last.UseInstaGibMod = BotUseInstaGibMod;
            last.FragLimit = BotFragLimit;
            last.TimeLimit = BotTimeLimit;
            _settingsService.Save(_appSettings);
        }
    }

    private bool CanStartBotMatch() => SelectedMap is not null;

    // ===================== Scheda Ospita partita (LAN o Internet) =====================
    [ObservableProperty] private string _hostServerName = "Partita InstaGib del Quest";
    [ObservableProperty] private int _hostPort = 27960;
    [ObservableProperty] private int _hostMaxClients = 12;
    [ObservableProperty] private string _hostPassword = "";
    [ObservableProperty] private bool _hostBotsEnabled;
    [ObservableProperty] private int _hostBotMinPlayers = 4;
    [ObservableProperty] private bool _hostIsLan = true;
    [ObservableProperty] private GameType _hostGameType = GameType.FreeForAll;
    [ObservableProperty] private bool _hostUseInstaGibMod = true;
    [ObservableProperty] private int _hostFragLimit = 30;
    [ObservableProperty] private int _hostTimeLimit = 15;
    [ObservableProperty] private string _hostStatusMessage = "Scegli una mappa nella scheda \"Gioca contro bot\" (l'elenco e' condiviso), poi torna qui.";

    [RelayCommand(CanExecute = nameof(CanStartHost))]
    private void StartHost()
    {
        if (SelectedMap is null)
        {
            HostStatusMessage = "Scegli prima una mappa (scheda \"Gioca contro bot\", stesso elenco).";
            return;
        }

        RefreshStorageAccessStatus();

        var options = new MultiplayerMatchOptions
        {
            MapTechnicalName = SelectedMap.TechnicalName,
            MapSource = SelectedMap.Source,
            RequiresMissionPackBaseGame = SelectedMap.RequiresMissionPackBaseGame,
            ServerName = string.IsNullOrWhiteSpace(HostServerName) ? "Partita InstaGib del Quest" : HostServerName,
            Port = HostPort,
            MaxClients = Math.Clamp(HostMaxClients, 2, 32),
            Password = string.IsNullOrWhiteSpace(HostPassword) ? null : HostPassword,
            FragLimit = HostFragLimit,
            TimeLimit = HostTimeLimit,
            Fov = VideoFov,
            GameType = HostGameType,
            BotsEnabled = HostBotsEnabled,
            BotMinPlayers = HostBotMinPlayers,
            IsLan = HostIsLan,
            UseInstaGibMod = HostUseInstaGibMod,
            SvPureOff = true,
            FixAspectRatio = false, // vedi commento equivalente in QuestLaunchService.PrepareAndJoin
            Player = CurrentProfile,
        };

        var outcome = _launchService.PrepareAndHost(options, _appSettings.Video);
        HostStatusMessage = outcome.Message;
    }

    private bool CanStartHost() => SelectedMap is not null;
}
