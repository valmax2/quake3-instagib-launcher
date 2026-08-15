using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Mac.Models;

namespace Quake3InstaGibLauncher.Mac.ViewModels;

/// <summary>Schermata "Locale InstaGib": configurazione di una partita contro bot e avvio reale.
/// Logica identica alla versione Windows; cambia solo la risoluzione dei percorsi (macOS ha
/// eseguibile e cartella dati separati).</summary>
public partial class LocalSetupViewModel : ObservableObject
{
    private readonly Func<MacInstallationPaths> _getPaths;
    private readonly Func<string, string, Task<bool>> _confirmLaunch;
    private readonly Action<LaunchOutcome> _onLaunched;
    private readonly Action<string, string?> _onDiagnosticsReport;
    private readonly Action<MapCardViewModel> _onMapUsed;
    private readonly LaunchService _launchService;
    private readonly RotationCfgService _rotationCfgService;
    private readonly KeyBindingCfgService _keyBindingCfgService = new();
    private readonly MacAppSettings _settings;

    public MapGalleryPanelViewModel Gallery { get; }

    [ObservableProperty] private int _totalPlayers;
    [ObservableProperty] private int _botSkill;
    [ObservableProperty] private int _fragLimit;
    [ObservableProperty] private int _timeLimit;
    [ObservableProperty] private int _fov;
    [ObservableProperty] private GameType _selectedGameType;
    [ObservableProperty] private bool _useInstaGibMod;
    [ObservableProperty] private bool _svPureOff;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public int BotCount => Math.Max(0, TotalPlayers - 1);
    public IReadOnlyList<GameType> AvailableGameTypes { get; } =
        new[] { GameType.FreeForAll, GameType.TeamDeathmatch, GameType.Tournament, GameType.CaptureTheFlag };

    partial void OnTotalPlayersChanged(int value) => OnPropertyChanged(nameof(BotCount));

    public LocalSetupViewModel(
        MacAppSettings settings,
        Func<MacInstallationPaths> getPaths,
        Func<string, string, Task<bool>> confirmLaunch,
        Action<LaunchOutcome> onLaunched,
        Action<string, string?> onDiagnosticsReport,
        Action<MapCardViewModel> onMapUsed,
        LaunchService launchService,
        RotationCfgService rotationCfgService)
    {
        _settings = settings;
        _getPaths = getPaths;
        _confirmLaunch = confirmLaunch;
        _onLaunched = onLaunched;
        _onDiagnosticsReport = onDiagnosticsReport;
        _onMapUsed = onMapUsed;
        _launchService = launchService;
        _rotationCfgService = rotationCfgService;

        Gallery = new MapGalleryPanelViewModel(onFavoritesChanged: () => { }, onRotationChanged: () => { });

        _totalPlayers = settings.LocalTotalPlayers;
        _botSkill = settings.LocalBotSkill;
        _fragLimit = settings.LocalFragLimit;
        _timeLimit = settings.LocalTimeLimit;
        _fov = settings.Fov;
        _selectedGameType = settings.LocalGameType;
        _useInstaGibMod = settings.LocalUseInstaGibMod;
        _svPureOff = settings.LocalSvPureOff;
    }

    public bool CurrentMapCompatibleWithMode =>
        Gallery.SelectedMap is null || Gallery.SelectedMap.SupportedGameTypes.Count == 0 ||
        Gallery.SelectedMap.SupportedGameTypes.Contains(SelectedGameType);

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

            var options = new LocalMatchOptions
            {
                MapTechnicalName = map.TechnicalName,
                RotationMaps = rotation,
                MapSource = map.Source,
                RequiresMissionPackBaseGame = map.Info.RequiresMissionPackBaseGame,
                TotalPlayers = TotalPlayers,
                BotSkill = BotSkill,
                FragLimit = FragLimit,
                TimeLimit = TimeLimit,
                Fov = Fov,
                GameType = SelectedGameType,
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

            var args = CommandBuilder.BuildLocalArguments(options, cfgFileName);

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
                StatusMessage = $"ioquake3 avviato (PID {outcome.ProcessId}).";
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

    private static string ResolveActiveModDirectory(MacInstallationPaths paths, bool useInstaGibMod, bool requiresMissionPackBaseGame)
    {
        if (useInstaGibMod) return paths.InstaGibPath;
        return requiresMissionPackBaseGame ? paths.MissionPackPath : paths.BaseQ3Path;
    }

    private void PersistLastUsedSettings(LocalMatchOptions options)
    {
        _settings.LocalTotalPlayers = options.TotalPlayers;
        _settings.LocalBotSkill = options.BotSkill;
        _settings.LocalFragLimit = options.FragLimit;
        _settings.LocalTimeLimit = options.TimeLimit;
        _settings.Fov = options.Fov;
        _settings.LocalGameType = options.GameType;
        _settings.LocalUseInstaGibMod = options.UseInstaGibMod;
        _settings.LocalSvPureOff = options.SvPureOff;
        _settings.LocalLastMap = options.MapTechnicalName;
        _settings.LocalRotationMaps = options.RotationMaps.ToList();
    }

    private static string BuildSummary(LocalMatchOptions options, string? cfgPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Partita locale InstaGib");
        sb.AppendLine($"Mappa iniziale: {options.MapTechnicalName}" + (options.RotationMaps.Count > 0
            ? $" (rotazione di {options.RotationMaps.Count + 1} mappe)"
            : string.Empty));
        sb.AppendLine($"Modalita': {options.GameType.ToDisplayName()}");
        sb.AppendLine($"Giocatori totali: {options.TotalPlayers} (tu + {options.BotCount} bot, difficolta' {options.BotSkill}/5)");
        sb.AppendLine($"Fraglimit: {options.FragLimit} · Timelimit: {options.TimeLimit} minuti · FOV: {options.Fov}");
        sb.AppendLine($"Mod InstaGib129: {(options.UseInstaGibMod ? "attiva" : "disattivata")} · sv_pure: {(options.SvPureOff ? "0 (disattivato)" : "1")}");
        if (cfgPath is not null)
            sb.AppendLine($"File di rotazione generato: {cfgPath}");
        return sb.ToString();
    }
}
