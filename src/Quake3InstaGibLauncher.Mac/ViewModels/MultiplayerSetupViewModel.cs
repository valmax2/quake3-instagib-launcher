using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Mac.Models;

namespace Quake3InstaGibLauncher.Mac.ViewModels;

/// <summary>
/// Schermata "Multiplayer InstaGib": ospita una partita non dedicata da questo Mac, oppure
/// cerca e si unisce a un server esistente (LAN o Internet, best-effort). Logica identica
/// alla versione Windows.
/// </summary>
public partial class MultiplayerSetupViewModel : ObservableObject
{
    private readonly Func<MacInstallationPaths> _getPaths;
    private readonly Func<string, string, Task<bool>> _confirmLaunch;
    private readonly Action<LaunchOutcome> _onLaunched;
    private readonly Action<string, string?> _onDiagnosticsReport;
    private readonly Action<MapCardViewModel> _onMapUsed;
    private readonly LaunchService _launchService;
    private readonly RotationCfgService _rotationCfgService;
    private readonly KeyBindingCfgService _keyBindingCfgService = new();
    private readonly Quake3ServerBrowser _serverBrowser = new();
    private readonly ServerInviteService _inviteService = new();
    private readonly Action _saveSettings;
    private readonly MacAppSettings _settings;

    public MapGalleryPanelViewModel Gallery { get; }

    [ObservableProperty] private bool _isHostTabActive = true;

    // --- Invito via WhatsApp ---
    [ObservableProperty] private bool _isDetectingIp;
    [ObservableProperty] private string _inviteStatusMessage = string.Empty;

    public ObservableCollection<ServerInfo> FoundServers { get; } = new();
    [ObservableProperty] private bool _isSearchingServers;
    [ObservableProperty] private string _searchStatusMessage = string.Empty;
    [ObservableProperty] private ServerInfo? _selectedServer;
    [ObservableProperty] private string _joinPassword = string.Empty;

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

    public IReadOnlyList<GameType> AvailableGameTypes { get; } =
        new[] { GameType.FreeForAll, GameType.TeamDeathmatch, GameType.Tournament, GameType.CaptureTheFlag };

    public MultiplayerSetupViewModel(
        MacAppSettings settings,
        Func<MacInstallationPaths> getPaths,
        Func<string, string, Task<bool>> confirmLaunch,
        Action<LaunchOutcome> onLaunched,
        Action<string, string?> onDiagnosticsReport,
        Action<MapCardViewModel> onMapUsed,
        LaunchService launchService,
        RotationCfgService rotationCfgService,
        Action saveSettings)
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
    }

    [RelayCommand] private void SelectHostTab() => IsHostTabActive = true;
    [RelayCommand] private void SelectJoinTab() => IsHostTabActive = false;
    [RelayCommand] private void SelectServer(ServerInfo? server) => SelectedServer = server;

    /// <summary>Compone un invito e apre WhatsApp con il messaggio gia' scritto: l'utente sceglie
    /// il contatto e decide se inviare. Logica identica alla versione Windows (vedi Core.Services.ServerInviteService).</summary>
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

            var outcome = _launchService.Launch(paths.ExecutablePath, paths.WorkingDirectory, args);
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

    [RelayCommand]
    private async Task SearchLanServersAsync()
    {
        IsSearchingServers = true;
        SearchStatusMessage = "Ricerca server sulla rete locale...";
        try
        {
            FoundServers.Clear();
            var results = await _serverBrowser.SearchLanAsync(Port, TimeSpan.FromSeconds(3));
            foreach (var s in results.OrderByDescending(s => s.Players))
                FoundServers.Add(s);

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
        SearchStatusMessage = "Interrogazione master server Internet in corso (best-effort)...";
        try
        {
            FoundServers.Clear();
            var results = await _serverBrowser.SearchInternetAsync();
            foreach (var s in results.OrderByDescending(s => s.Players))
                FoundServers.Add(s);

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
                // Il motore consulta sempre baseq3 come fallback qualunque sia la mod attiva in
                // questa sessione (decisa dal server in connessione, non piu' da noi): scrivere
                // qui il file dei tasti garantisce che "+exec" lo trovi sempre.
                var bindingsFileName = _keyBindingCfgService.WriteBindingsCfg(paths.BaseQ3Path, _settings.KeyBindings);
                if (bindingsFileName is not null) { args.Add("+exec"); args.Add(bindingsFileName); }
            }

            var modLabel = string.IsNullOrWhiteSpace(server.ModName) ? "baseq3 (nessuna mod dichiarata)" : server.ModName;
            var summary =
                $"Connessione al server esistente\n\"{server.HostName}\" ({server.EndPointText})\n" +
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

            var outcome = _launchService.Launch(paths.ExecutablePath, paths.WorkingDirectory, args);
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

    private static string ResolveActiveModDirectory(MacInstallationPaths paths, bool useInstaGibMod, bool requiresMissionPackBaseGame)
    {
        if (useInstaGibMod) return paths.InstaGibPath;
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
        sb.AppendLine("Partita multiplayer InstaGib (server ospitato da questo Mac)");
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
