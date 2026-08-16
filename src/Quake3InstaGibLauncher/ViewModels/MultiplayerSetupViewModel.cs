using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Models;
using Quake3InstaGibLauncher.Services;

namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>
/// Schermata "Multiplayer InstaGib": il PC dell'utente ospita una partita non dedicata
/// (l'host gioca dalla stessa istanza). Configura mappa/rotazione, server e avvia realmente ioquake3.
/// </summary>
public partial class MultiplayerSetupViewModel : ObservableObject
{
    private readonly Func<InstallationPaths> _getPaths;
    private readonly Func<string, string, Task<bool>> _confirmLaunch;
    private readonly Action<LaunchOutcome> _onLaunched;
    private readonly Action<string, string?> _onDiagnosticsReport;
    private readonly Action<MapCardViewModel> _onMapUsed;
    private readonly LaunchService _launchService;
    private readonly RotationCfgService _rotationCfgService;
    private readonly Quake3ServerBrowser _serverBrowser = new();
    private readonly ServerInviteService _inviteService = new();
    private readonly KeyBindingCfgService _keyBindingCfgService = new();
    private readonly Action _saveSettings;
    private readonly AppSettings _settings;
    private readonly PlayersViewModel _playersVm;

    public MapGalleryPanelViewModel Gallery { get; }

    /// <summary>True = scheda "Ospita" visibile, false = scheda "Cerca e unisciti" visibile.</summary>
    [ObservableProperty] private bool _isHostTabActive = true;

    // --- Invito via WhatsApp ---
    [ObservableProperty] private bool _isDetectingIp;
    [ObservableProperty] private string _inviteStatusMessage = string.Empty;

    // --- Cerca e unisciti a un server esistente ---
    private readonly List<ServerInfo> _allFoundServers = new();

    /// <summary>Proprieta' osservabile piena (non una collezione fissa svuotata e riempita
    /// elemento per elemento): un bug di prestazioni reale, segnalato dall'utente come "lento
    /// nella gestione filtri", veniva da qui. Con centinaia di server trovati su Internet,
    /// Clear() + centinaia di Add() singoli forzavano l'ItemsControl a rigenerare il proprio
    /// albero visivo un elemento alla volta ad ogni singola modifica di un filtro (percepibile
    /// come un blocco/lag della UI). Riassegnando l'intera collezione in un solo colpo (vedi
    /// ApplyServerFilters) il binding si aggiorna con un'unica notifica invece di centinaia.</summary>
    [ObservableProperty] private ObservableCollection<ServerInfo> _filteredServers = new();
    [ObservableProperty] private bool _isSearchingServers;
    [ObservableProperty] private string _searchStatusMessage = string.Empty;
    [ObservableProperty] private ServerInfo? _selectedServer;
    [ObservableProperty] private string _joinPassword = string.Empty;

    // --- Filtri elenco server ---
    [ObservableProperty] private bool _serverFilterFfa;
    [ObservableProperty] private bool _serverFilterTeam;
    [ObservableProperty] private bool _serverFilterTournament;
    [ObservableProperty] private bool _serverFilterCtf;
    [ObservableProperty] private bool _serverFilterInstaGibOnly;
    [ObservableProperty] private bool _serverFilterHideFull;
    [ObservableProperty] private bool _serverFilterHideEmpty;
    [ObservableProperty] private bool _serverFilterLimitPing;
    [ObservableProperty] private int _serverMaxPingMs = 150;
    [ObservableProperty] private bool _serverSortByPlayers;
    [ObservableProperty] private bool _serverFilterKnownPlayersOnly;
    [ObservableProperty] private bool _serverFilterHideBotOnly;
    [ObservableProperty] private bool _isLoadingPlayerLists;
    [ObservableProperty] private string _playerListStatusMessage = string.Empty;

    // --- Preset server salvati ---
    public ObservableCollection<ServerPreset> SavedPresets { get; } = new();
    [ObservableProperty] private ServerPreset? _selectedPreset;
    [ObservableProperty] private string _newPresetName = string.Empty;

    [ObservableProperty] private string _serverName;
    [ObservableProperty] private int _port;
    [ObservableProperty] private int _maxClients;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private int _fragLimit;
    [ObservableProperty] private int _timeLimit;
    [ObservableProperty] private int _fov;
    [ObservableProperty] private GameType _selectedGameType;
    [ObservableProperty] private bool _botsEnabled;
    [ObservableProperty] private int _botMinPlayers;
    [ObservableProperty] private bool _isLan;
    [ObservableProperty] private bool _useInstaGibMod;
    [ObservableProperty] private bool _svPureOff;
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Stato reale della regola Firewall di Windows per ioquake3.x86_64.exe, controllato
    /// sul serio (vedi FirewallStatusService) invece di mostrare sempre lo stesso promemoria
    /// statico anche quando l'eccezione e' gia' presente. Null = non ancora controllato, oppure
    /// impossibile determinarlo (in quel caso l'interfaccia mostra il promemoria generico, mai un
    /// falso "non consentito").</summary>
    [ObservableProperty] private bool? _firewallAllowed;

    public IReadOnlyList<GameType> AvailableGameTypes { get; } =
        new[] { GameType.FreeForAll, GameType.TeamDeathmatch, GameType.Tournament, GameType.CaptureTheFlag };

    public MultiplayerSetupViewModel(
        AppSettings settings,
        Func<InstallationPaths> getPaths,
        Func<string, string, Task<bool>> confirmLaunch,
        Action<LaunchOutcome> onLaunched,
        Action<string, string?> onDiagnosticsReport,
        Action<MapCardViewModel> onMapUsed,
        LaunchService launchService,
        RotationCfgService rotationCfgService,
        Action saveSettings,
        PlayersViewModel playersVm)
    {
        _settings = settings;
        _getPaths = getPaths;
        _confirmLaunch = confirmLaunch;
        _onLaunched = onLaunched;
        _onDiagnosticsReport = onDiagnosticsReport;
        _onMapUsed = onMapUsed;
        _launchService = launchService;
        _rotationCfgService = rotationCfgService;
        _saveSettings = saveSettings;
        _playersVm = playersVm;

        Gallery = new MapGalleryPanelViewModel(onFavoritesChanged: () => { }, onRotationChanged: () => { });

        foreach (var preset in settings.SavedServerPresets)
            SavedPresets.Add(preset);

        _serverName = settings.ServerName;
        _port = settings.ServerPort;
        _maxClients = settings.ServerMaxClients;
        _fragLimit = settings.ServerFragLimit;
        _timeLimit = settings.ServerTimeLimit;
        _fov = settings.Fov;
        _selectedGameType = settings.ServerGameType;
        _botsEnabled = settings.ServerBotsEnabled;
        _botMinPlayers = settings.ServerBotMinPlayers;
        _isLan = settings.ServerIsLan;
        _useInstaGibMod = settings.LocalUseInstaGibMod;
        _svPureOff = settings.LocalSvPureOff;

        _serverFilterFfa = settings.ServerFilterFfa;
        _serverFilterTeam = settings.ServerFilterTeam;
        _serverFilterTournament = settings.ServerFilterTournament;
        _serverFilterCtf = settings.ServerFilterCtf;
        _serverFilterInstaGibOnly = settings.ServerFilterInstaGibOnly;
        _serverFilterHideFull = settings.ServerFilterHideFull;
        _serverFilterHideEmpty = settings.ServerFilterHideEmpty;
        _serverFilterLimitPing = settings.ServerFilterLimitPing;
        _serverMaxPingMs = settings.ServerMaxPingMs;
        _serverSortByPlayers = settings.ServerSortByPlayers;
        _serverFilterKnownPlayersOnly = settings.ServerFilterKnownPlayersOnly;
        _serverFilterHideBotOnly = settings.ServerFilterHideBotOnly;

        // Se l'ultima sessione era gia' impostata su "Anche da Internet", controlla subito lo
        // stato reale del firewall invece di aspettare che l'utente cambi opzione manualmente.
        if (!_isLan)
            RefreshFirewallStatus();
    }

    partial void OnIsLanChanged(bool value)
    {
        if (!value) RefreshFirewallStatus();
    }

    partial void OnFirewallAllowedChanged(bool? value)
    {
        OnPropertyChanged(nameof(FirewallStatusKnownAllowed));
        OnPropertyChanged(nameof(FirewallStatusNeedsAction));
        OnPropertyChanged(nameof(FirewallStatusUnknown));
    }

    /// <summary>True solo quando il controllo e' riuscito E la regola esiste gia': la UI mostra il
    /// segno di spunta verde solo in questo caso, mai per il caso "non sappiamo" (null).</summary>
    public bool FirewallStatusKnownAllowed => FirewallAllowed == true;

    /// <summary>True solo quando il controllo e' riuscito E la regola NON esiste: la UI mostra il
    /// promemoria "da fare" solo in questo caso, non quando semplicemente non abbiamo potuto
    /// controllare (quel caso mostra il testo generico invariato, vedi sotto).</summary>
    public bool FirewallStatusNeedsAction => FirewallAllowed == false;

    /// <summary>True quando il controllo non e' ancora partito o non e' stato possibile
    /// determinare lo stato (servizio Windows Firewall disattivato, permessi insufficienti,
    /// percorso non ancora valido): in questo caso, e SOLO in questo, resta il testo generico
    /// originale, per non affermare ne' "consentito" ne' "da fare" senza esserne certi.</summary>
    public bool FirewallStatusUnknown => FirewallAllowed is null;

    /// <summary>Interroga sul serio il Firewall di Windows (vedi FirewallStatusService) invece di
    /// limitarsi a mostrare sempre lo stesso promemoria statico. Anche disponibile come comando
    /// per un pulsante "Ricontrolla" nella UI, per chi ha appena aggiunto l'eccezione a mano e
    /// vuole vedere subito il segno di spunta senza riaprire la schermata.</summary>
    [RelayCommand]
    private void RefreshFirewallStatus()
    {
        try
        {
            FirewallAllowed = FirewallStatusService.IsExecutableAllowedInbound(_getPaths().ExecutablePath);
        }
        catch
        {
            // Percorso non ancora valido (installazione non trovata): resta "sconosciuto".
            FirewallAllowed = null;
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        var map = Gallery.SelectedMap;
        if (map is null)
        {
            StatusMessage = "Seleziona prima una mappa dalla galleria.";
            return;
        }

        try
        {
            // Rete di sicurezza finale: vedi lo stesso controllo in LocalSetupViewModel.StartAsync.
            var incompatible = Gallery.RotationMaps.FirstOrDefault(r => r.Info.RequiresMissionPackBaseGame != map.Info.RequiresMissionPackBaseGame);
            if (incompatible is not null)
            {
                StatusMessage = $"Rotazione non valida: \"{incompatible.LongName}\" non e' compatibile con \"{map.LongName}\" (missionpack vs normale). Rimuovila dalla rotazione prima di avviare.";
                return;
            }

            var rotation = Gallery.RotationMaps.Select(m => m.TechnicalName).ToList();

            var options = new MultiplayerMatchOptions
            {
                MapTechnicalName = map.TechnicalName,
                RotationMaps = rotation,
                MapSource = map.Source,
                RequiresMissionPackBaseGame = map.Info.RequiresMissionPackBaseGame,
                ServerName = ServerName,
                Port = Port,
                MaxClients = MaxClients,
                Password = string.IsNullOrWhiteSpace(Password) ? null : Password,
                FragLimit = FragLimit,
                TimeLimit = TimeLimit,
                Fov = Fov,
                GameType = SelectedGameType,
                BotsEnabled = BotsEnabled,
                BotMinPlayers = BotMinPlayers,
                IsLan = IsLan,
                UseInstaGibMod = UseInstaGibMod,
                SvPureOff = SvPureOff,
                FixAspectRatio = _settings.FixAspectRatio,
                ScreenWidth = _settings.ScreenWidth,
                ScreenHeight = _settings.ScreenHeight,
                Fullscreen = _settings.Fullscreen,
                Player = _settings.PlayerProfile,
            };

            var paths = _getPaths();
            string? cfgFileName = null;
            string? cfgFullPath = null;
            var targetDir = ResolveActiveModDirectory(paths, UseInstaGibMod, map.Info.RequiresMissionPackBaseGame);

            if (rotation.Count > 0)
            {
                var allRotationMaps = new List<string> { map.TechnicalName };
                allRotationMaps.AddRange(rotation);
                var cfgResult = _rotationCfgService.WriteRotationCfg(targetDir, allRotationMaps);
                cfgFileName = cfgResult.FileNameForExec;
                cfgFullPath = cfgResult.FullPathInGameDir;
            }

            var args = CommandBuilder.BuildMultiplayerArguments(options, cfgFileName);

            if (_settings.KeyBindings is { Count: > 0 })
            {
                var bindingsFileName = _keyBindingCfgService.WriteBindingsCfg(targetDir, _settings.KeyBindings);
                if (bindingsFileName is not null) { args.Add("+exec"); args.Add(bindingsFileName); }
            }
            var summary = BuildSummary(options, cfgFullPath);
            var advanced = CommandBuilder.ToDisplayCommand(paths.ExecutablePath, args);

            _onDiagnosticsReport(advanced, cfgFullPath);

            var confirmed = await _confirmLaunch(summary, advanced);
            if (!confirmed)
            {
                StatusMessage = "Avvio annullato.";
                return;
            }

            var outcome = _launchService.Launch(paths.ExecutablePath, paths.RootPath, args);
            _onLaunched(outcome);

            if (outcome.Success)
            {
                StatusMessage = $"Server avviato (PID {outcome.ProcessId}). Porta UDP {Port}.";
                PersistLastUsedSettings(options);
                _onMapUsed(map);
            }
            else
            {
                StatusMessage = $"Avvio non riuscito: {outcome.ErrorMessage}";
            }
        }
        catch (LaunchValidationException ex)
        {
            StatusMessage = $"Parametri non validi: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore imprevisto durante l'avvio: {ex.Message}";
        }
    }

    [RelayCommand] private void SelectHostTab() => IsHostTabActive = true;
    [RelayCommand] private void SelectJoinTab() => IsHostTabActive = false;
    [RelayCommand] private void SelectServer(ServerInfo? server) => SelectedServer = server;

    /// <summary>Doppio clic su una riga della lista server: seleziona e si unisce subito, senza
    /// dover cliccare anche il pulsante "Unisciti" sotto.</summary>
    [RelayCommand]
    private async Task SelectAndJoinServerAsync(ServerInfo? server)
    {
        if (server is null) return;
        SelectedServer = server;
        await JoinSelectedServerAsync();
    }

    /// <summary>
    /// Compone un invito e apre WhatsApp con il messaggio gia' scritto (link "wa.me"): l'utente
    /// sceglie il contatto e decide se inviare, il launcher non manda nulla per conto proprio.
    /// Se "Internet" e' selezionato prova a rilevare l'IP pubblico (interrogando api.ipify.org,
    /// nessun altro dato inviato); altrimenti usa l'indirizzo di rete locale.
    /// </summary>
    [RelayCommand]
    private async Task ShareViaWhatsAppAsync()
    {
        IsDetectingIp = true;
        InviteStatusMessage = IsLan ? "Rilevamento indirizzo di rete locale..." : "Rilevamento IP pubblico (api.ipify.org)...";
        try
        {
            var address = IsLan
                ? ServerInviteService.GetLocalLanAddress()
                : await _inviteService.TryDetectPublicIpAsync();

            address ??= ServerInviteService.GetLocalLanAddress() ?? "IP-DA-COMPLETARE";

            var mapName = Gallery.SelectedMap?.LongName ?? _settings.ServerLastMap;
            var message = ServerInviteService.BuildInviteMessage(ServerName, address, Port, mapName);
            ServerInviteService.OpenWhatsAppWithMessage(message);

            InviteStatusMessage = $"WhatsApp aperto con l'invito (indirizzo: {address}:{Port}).";
        }
        catch (Exception ex)
        {
            InviteStatusMessage = $"Impossibile aprire WhatsApp: {ex.Message}";
        }
        finally
        {
            IsDetectingIp = false;
        }
    }

    // ===================== Cerca e unisciti a un server esistente =====================

    [RelayCommand]
    private async Task SearchLanServersAsync()
    {
        IsSearchingServers = true;
        SearchStatusMessage = "Ricerca server sulla rete locale...";
        try
        {
            _allFoundServers.Clear();
            var results = await _serverBrowser.SearchLanAsync(Port, TimeSpan.FromSeconds(3));
            _allFoundServers.AddRange(results);
            ApplyServerFilters();

            SearchStatusMessage = results.Count == 0
                ? "Nessun server trovato sulla rete locale."
                : $"{results.Count} server trovato/i sulla rete locale.";
        }
        catch (Exception ex)
        {
            SearchStatusMessage = $"Ricerca non riuscita: {ex.Message}";
        }
        finally
        {
            IsSearchingServers = false;
        }
    }

    [RelayCommand]
    private async Task SearchInternetServersAsync()
    {
        IsSearchingServers = true;
        SearchStatusMessage = "Interrogazione master server Internet in corso (potrebbe non rispondere, best-effort)...";
        try
        {
            _allFoundServers.Clear();
            var results = await _serverBrowser.SearchInternetAsync();
            _allFoundServers.AddRange(results);
            ApplyServerFilters();

            SearchStatusMessage = results.Count == 0
                ? "Nessun server risposto dal master Internet (o rete non raggiungibile)."
                : $"{results.Count} server trovato/i su Internet.";
        }
        catch (Exception ex)
        {
            SearchStatusMessage = $"Ricerca non riuscita: {ex.Message}";
        }
        finally
        {
            IsSearchingServers = false;
        }
    }

    // I filtri vengono salvati SUBITO ad ogni modifica (stesso schema "commit immediato" usato
    // altrove nell'app): la prossima volta che si apre Multiplayer, la ricerca riparte gia' con
    // gli stessi filtri dell'ultima sessione invece di tornare sempre ai valori di default.
    partial void OnServerFilterFfaChanged(bool value) { _settings.ServerFilterFfa = value; _saveSettings(); ApplyServerFilters(); }
    partial void OnServerFilterTeamChanged(bool value) { _settings.ServerFilterTeam = value; _saveSettings(); ApplyServerFilters(); }
    partial void OnServerFilterTournamentChanged(bool value) { _settings.ServerFilterTournament = value; _saveSettings(); ApplyServerFilters(); }
    partial void OnServerFilterCtfChanged(bool value) { _settings.ServerFilterCtf = value; _saveSettings(); ApplyServerFilters(); }
    partial void OnServerFilterInstaGibOnlyChanged(bool value) { _settings.ServerFilterInstaGibOnly = value; _saveSettings(); ApplyServerFilters(); }
    partial void OnServerFilterHideFullChanged(bool value) { _settings.ServerFilterHideFull = value; _saveSettings(); ApplyServerFilters(); }
    partial void OnServerFilterHideEmptyChanged(bool value) { _settings.ServerFilterHideEmpty = value; _saveSettings(); ApplyServerFilters(); }
    partial void OnServerFilterLimitPingChanged(bool value) { _settings.ServerFilterLimitPing = value; _saveSettings(); ApplyServerFilters(); }
    partial void OnServerMaxPingMsChanged(int value) { _settings.ServerMaxPingMs = value; _saveSettings(); ApplyServerFilters(); }
    partial void OnServerSortByPlayersChanged(bool value) { _settings.ServerSortByPlayers = value; _saveSettings(); ApplyServerFilters(); }
    partial void OnServerFilterKnownPlayersOnlyChanged(bool value) { _settings.ServerFilterKnownPlayersOnly = value; _saveSettings(); ApplyServerFilters(); }
    partial void OnServerFilterHideBotOnlyChanged(bool value) { _settings.ServerFilterHideBotOnly = value; _saveSettings(); ApplyServerFilters(); }

    /// <summary>
    /// Interroga (in parallelo, con concorrenza limitata) ogni server attualmente visibile con
    /// "getstatus" per sapere chi ci sta giocando, e incrocia i nomi con i Giocatori conosciuti
    /// salvati dall'utente. E' un'operazione separata dalla ricerca server (piu' pesante: una
    /// richiesta di rete per ciascun server), quindi va avviata esplicitamente.
    /// </summary>
    [RelayCommand]
    private async Task RefreshPlayerListsAsync()
    {
        var targets = FilteredServers.ToList();
        if (targets.Count == 0)
        {
            PlayerListStatusMessage = "Nessun server in elenco: cerca prima dei server.";
            return;
        }

        IsLoadingPlayerLists = true;
        PlayerListStatusMessage = $"Interrogazione di {targets.Count} server in corso...";

        try
        {
            using var throttle = new SemaphoreSlim(8);
            var tasks = targets.Select(async server =>
            {
                await throttle.WaitAsync();
                try
                {
                    var players = await _serverBrowser.GetPlayerStatusAsync(server.Address, server.Port, TimeSpan.FromSeconds(1.5));
                    server.PlayerNames = players.Select(p => p.Name).ToList();
                    server.MatchedKnownPlayers = players
                        .Select(p => Quake3TextUtils.StripColorCodes(p.Name))
                        .Where(n => _playersVm.IsKnown(n))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    server.HumanPlayerCount = players.Count(p => !p.IsLikelyBot);
                    server.BotPlayerCount = players.Count(p => p.IsLikelyBot);
                    server.PlayerListLoaded = true;
                }
                finally { throttle.Release(); }
            });

            await Task.WhenAll(tasks);

            var withKnown = targets.Count(s => s.HasKnownPlayers);
            PlayerListStatusMessage = withKnown > 0
                ? $"Fatto: {withKnown} server con giocatori conosciuti trovati!"
                : "Fatto: nessun giocatore conosciuto tra i server interrogati.";

            ApplyServerFilters(); // forza il refresh visivo delle card (ServerInfo non e' osservabile)
        }
        catch (Exception ex)
        {
            PlayerListStatusMessage = $"Interrogazione non riuscita: {ex.Message}";
        }
        finally
        {
            IsLoadingPlayerLists = false;
        }
    }

    /// <summary>Salva un giocatore visto in un server tra i Giocatori conosciuti (valutazione
    /// iniziale "Forte": e' il caso d'uso principale, segnarsi un avversario da voler risfidare).</summary>
    [RelayCommand]
    private void RememberPlayer(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return;
        var clean = Quake3TextUtils.StripColorCodes(rawName).Trim();
        if (clean.Length == 0) return;

        _playersVm.AddOrUpdate(clean, PlayerRating.Forte);
        PlayerListStatusMessage = $"{clean} salvato tra i giocatori conosciuti.";
    }

    /// <summary>Ricalcola FilteredServers a partire dai server trovati (_allFoundServers) e dai
    /// filtri correnti. Nessuna nuova richiesta di rete: agisce solo sui risultati gia' ricevuti.</summary>
    private void ApplyServerFilters()
    {
        var noTypeFilter = !ServerFilterFfa && !ServerFilterTeam && !ServerFilterTournament && !ServerFilterCtf;

        IEnumerable<ServerInfo> query = _allFoundServers;

        if (!noTypeFilter)
        {
            query = query.Where(s => s.ParsedGameType switch
            {
                GameType.FreeForAll => ServerFilterFfa,
                GameType.TeamDeathmatch => ServerFilterTeam,
                GameType.Tournament => ServerFilterTournament,
                GameType.CaptureTheFlag => ServerFilterCtf,
                _ => false,
            });
        }

        if (ServerFilterInstaGibOnly)
            query = query.Where(s => s.IsLikelyInstaGib);

        if (ServerFilterHideFull)
            query = query.Where(s => !s.IsFull);

        if (ServerFilterHideEmpty)
            query = query.Where(s => !s.IsEmpty);

        if (ServerFilterLimitPing)
            query = query.Where(s => s.PingMs is null || s.PingMs <= ServerMaxPingMs);

        if (ServerFilterKnownPlayersOnly)
            query = query.Where(s => s.HasKnownPlayers);

        // Richiede "Carica giocatori" per essere efficace (IsBotOnly resta false finche' i dati
        // non arrivano): un server non ancora interrogato non viene mai nascosto per errore.
        if (ServerFilterHideBotOnly)
            query = query.Where(s => !s.IsBotOnly);

        query = ServerSortByPlayers
            ? query.OrderByDescending(s => s.Players)
            : query.OrderBy(s => s.PingMs ?? long.MaxValue);

        FilteredServers = new ObservableCollection<ServerInfo>(query);
    }

    [RelayCommand]
    private async Task JoinSelectedServerAsync()
    {
        var server = SelectedServer;
        if (server is null)
        {
            SearchStatusMessage = "Seleziona prima un server dalla lista.";
            return;
        }

        try
        {
            var paths = _getPaths();
            var args = CommandBuilder.BuildJoinArguments(
                server.Address, server.Port, server.ModName,
                string.IsNullOrWhiteSpace(JoinPassword) ? null : JoinPassword, Fov,
                _settings.FixAspectRatio, _settings.ScreenWidth, _settings.ScreenHeight, _settings.Fullscreen,
                _settings.PlayerProfile);

            if (_settings.KeyBindings is { Count: > 0 })
            {
                // Il motore consulta SEMPRE baseq3 come fallback, qualunque sia la mod attiva in
                // questa sessione (che qui non decidiamo piu' noi: e' il server a deciderla in
                // fase di connessione, vedi CommandBuilder.BuildJoinArguments): scrivere qui il
                // file dei tasti garantisce che "+exec" lo trovi sempre, anche su un server che
                // gira una mod diversa da InstaGib129 e mai installata in locale.
                var bindingsFileName = _keyBindingCfgService.WriteBindingsCfg(paths.BaseQ3Path, _settings.KeyBindings);
                if (bindingsFileName is not null) { args.Add("+exec"); args.Add(bindingsFileName); }
            }

            var modLabel = string.IsNullOrWhiteSpace(server.ModName) ? "baseq3 (nessuna mod dichiarata)" : server.ModName;
            var summary =
                $"Connessione al server esistente\n" +
                $"\"{server.HostName}\" ({server.EndPointText})\n" +
                $"Mappa: {server.MapName} · Giocatori: {server.Players}/{server.MaxPlayers}\n" +
                (server.NeedsPassword ? "Il server richiede una password.\n" : "") +
                $"Mod del server: {modLabel} (il client si adatta automaticamente in connessione)";

            var advanced = CommandBuilder.ToDisplayCommand(paths.ExecutablePath, args);
            _onDiagnosticsReport(advanced, null);

            var confirmed = await _confirmLaunch(summary, advanced);
            if (!confirmed)
            {
                SearchStatusMessage = "Connessione annullata.";
                return;
            }

            var outcome = _launchService.Launch(paths.ExecutablePath, paths.RootPath, args);
            _onLaunched(outcome);
            SearchStatusMessage = outcome.Success
                ? $"Connessione avviata verso {server.EndPointText}."
                : $"Connessione non riuscita: {outcome.ErrorMessage}";
        }
        catch (LaunchValidationException ex)
        {
            SearchStatusMessage = $"Parametri non validi: {ex.Message}";
        }
        catch (Exception ex)
        {
            SearchStatusMessage = $"Errore imprevisto: {ex.Message}";
        }
    }

    // ===================== Preset server salvati =====================

    [RelayCommand]
    private void SaveCurrentAsPreset()
    {
        var name = string.IsNullOrWhiteSpace(NewPresetName) ? ServerName : NewPresetName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Indica un nome per il preset.";
            return;
        }

        var preset = new ServerPreset
        {
            Name = name,
            ServerName = ServerName,
            Port = Port,
            MaxClients = MaxClients,
            GameType = SelectedGameType,
            FragLimit = FragLimit,
            TimeLimit = TimeLimit,
            Fov = Fov,
            BotsEnabled = BotsEnabled,
            BotMinPlayers = BotMinPlayers,
            IsLan = IsLan,
            MapTechnicalName = Gallery.SelectedMap?.TechnicalName ?? _settings.ServerLastMap,
        };

        // Se esiste gia' un preset con lo stesso nome, lo sostituiamo invece di duplicarlo.
        var existing = SavedPresets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) SavedPresets.Remove(existing);

        SavedPresets.Add(preset);
        _settings.SavedServerPresets = SavedPresets.ToList();
        _saveSettings();

        NewPresetName = string.Empty;
        StatusMessage = $"Preset \"{name}\" salvato.";
    }

    [RelayCommand]
    private void LoadPreset(ServerPreset? preset)
    {
        preset ??= SelectedPreset;
        if (preset is null) return;

        ServerName = preset.ServerName;
        Port = preset.Port;
        MaxClients = preset.MaxClients;
        SelectedGameType = preset.GameType;
        FragLimit = preset.FragLimit;
        TimeLimit = preset.TimeLimit;
        Fov = preset.Fov;
        BotsEnabled = preset.BotsEnabled;
        BotMinPlayers = preset.BotMinPlayers;
        IsLan = preset.IsLan;

        var mapCard = Gallery.FilteredMaps.FirstOrDefault(m =>
            string.Equals(m.TechnicalName, preset.MapTechnicalName, StringComparison.OrdinalIgnoreCase));
        if (mapCard is not null) Gallery.SelectedMap = mapCard;

        StatusMessage = $"Preset \"{preset.Name}\" caricato.";
    }

    [RelayCommand]
    private void DeletePreset(ServerPreset? preset)
    {
        preset ??= SelectedPreset;
        if (preset is null) return;

        SavedPresets.Remove(preset);
        _settings.SavedServerPresets = SavedPresets.ToList();
        _saveSettings();
        StatusMessage = $"Preset \"{preset.Name}\" eliminato.";
    }

    /// <summary>Cartella in cui il motore cerca per prima i file sciolti (fs_game ha sempre
    /// precedenza sulla basegame): e' li' che va scritto il .cfg di rotazione perche' "+exec" lo trovi.</summary>
    private static string ResolveActiveModDirectory(InstallationPaths paths, bool useInstaGibMod, bool requiresMissionPackBaseGame)
    {
        if (useInstaGibMod)
            return paths.InstaGibPath;

        return requiresMissionPackBaseGame ? paths.MissionPackPath : paths.BaseQ3Path;
    }

    private void PersistLastUsedSettings(MultiplayerMatchOptions options)
    {
        _settings.ServerName = options.ServerName;
        _settings.ServerPort = options.Port;
        _settings.ServerMaxClients = options.MaxClients;
        _settings.ServerBotsEnabled = options.BotsEnabled;
        _settings.ServerBotMinPlayers = options.BotMinPlayers;
        _settings.ServerFragLimit = options.FragLimit;
        _settings.ServerTimeLimit = options.TimeLimit;
        _settings.Fov = options.Fov;
        _settings.ServerGameType = options.GameType;
        _settings.ServerIsLan = options.IsLan;
        _settings.ServerLastMap = options.MapTechnicalName;
        _settings.ServerRotationMaps = options.RotationMaps.ToList();
        _settings.LocalUseInstaGibMod = options.UseInstaGibMod;
        _settings.LocalSvPureOff = options.SvPureOff;
    }

    private static string BuildSummary(MultiplayerMatchOptions options, string? cfgPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Partita multiplayer InstaGib (server ospitato da questo PC)");
        sb.AppendLine($"Nome server: {options.ServerName} · Porta UDP: {options.Port}");
        sb.AppendLine($"Mappa iniziale: {options.MapTechnicalName}" + (options.RotationMaps.Count > 0
            ? $" (rotazione di {options.RotationMaps.Count + 1} mappe)"
            : string.Empty));
        sb.AppendLine($"Modalita': {options.GameType.ToDisplayName()} · Giocatori max: {options.MaxClients}");
        sb.AppendLine(options.BotsEnabled
            ? $"Bot: attivi, riempimento fino a {options.BotMinPlayers} giocatori totali"
            : "Bot: nessuno");
        sb.AppendLine($"Fraglimit: {options.FragLimit} · Timelimit: {options.TimeLimit} minuti · FOV: {options.Fov}");
        sb.AppendLine($"Visibilita': {(options.IsLan ? "solo rete locale (LAN)" : "anche da Internet (richiede port forwarding)")}");
        sb.AppendLine(string.IsNullOrEmpty(options.Password) ? "Password: nessuna" : "Password: impostata");
        if (cfgPath is not null)
            sb.AppendLine($"File di rotazione generato: {cfgPath}");
        return sb.ToString();
    }
}
