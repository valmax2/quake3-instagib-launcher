using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Models;
using Quake3InstaGibLauncher.Services;

namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>Schermata "Locale InstaGib": configurazione di una partita contro bot e avvio reale.</summary>
public partial class LocalSetupViewModel : ObservableObject
{
    private readonly Func<InstallationPaths> _getPaths;
    private readonly Func<string, string, Task<bool>> _confirmLaunch;
    private readonly Action<LaunchOutcome> _onLaunched;
    private readonly Action<string, string?> _onDiagnosticsReport;
    private readonly Action<MapCardViewModel> _onMapUsed;
    private readonly LaunchService _launchService;
    private readonly RotationCfgService _rotationCfgService;
    private readonly StartupCfgService _startupCfgService = new();
    private readonly AppSettings _settings;
    private readonly KeyBindingCfgService _keyBindingCfgService = new();

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
        AppSettings settings,
        Func<InstallationPaths> getPaths,
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
            // Rete di sicurezza finale: la UI (MapGalleryPanelViewModel) gia' impedisce di
            // costruire una rotazione che mischi mappe missionpack e normali (crash garantito a
            // meta' partita, vedi commenti li'), ma controlliamo di nuovo qui prima di scrivere
            // davvero il file .cfg, cosi' nessun percorso futuro puo' aggirare la protezione.
            var incompatible = Gallery.RotationMaps.FirstOrDefault(r => r.Info.RequiresMissionPackBaseGame != map.Info.RequiresMissionPackBaseGame);
            if (incompatible is not null)
            {
                StatusMessage = $"Rotazione non valida: \"{incompatible.LongName}\" non e' compatibile con \"{map.LongName}\" (missionpack vs normale). Rimuovila dalla rotazione prima di avviare.";
                return;
            }

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

            // Consolida tutti i "+set"/"+exec" in un unico file .cfg: vedi
            // CommandBuilder.ConsolidateForStartupCfg per il perche' (limite del motore sul numero
            // di argomenti "+" accettati insieme, superato facilmente con una configurazione
            // completa e causa reale del ritorno silenzioso al menu principale).
            var (finalArgs, startupCfgContent) = CommandBuilder.ConsolidateForStartupCfg(args);
            if (!string.IsNullOrEmpty(startupCfgContent))
            {
                var startupResult = _startupCfgService.WriteStartupCfg(targetDir, startupCfgContent);
                finalArgs.Add("+exec");
                finalArgs.Add(startupResult.FileNameForExec);
            }

            var summary = BuildSummary(options, cfgFullPath);
            var advanced = CommandBuilder.ToDisplayCommand(paths.ExecutablePath, finalArgs);

            _onDiagnosticsReport(advanced, cfgFullPath);

            var confirmed = await _confirmLaunch(summary, advanced);
            if (!confirmed)
            {
                StatusMessage = "Avvio annullato.";
                return;
            }

            var outcome = _launchService.Launch(paths.ExecutablePath, paths.RootPath, finalArgs);
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

    /// <summary>Cartella in cui il motore cerca per prima i file sciolti (fs_game ha sempre
    /// precedenza sulla basegame): e' li' che va scritto il .cfg di rotazione perche' "+exec" lo trovi.</summary>
    private static string ResolveActiveModDirectory(InstallationPaths paths, bool useInstaGibMod, bool requiresMissionPackBaseGame)
    {
        if (useInstaGibMod)
            return paths.InstaGibPath;

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
