using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Mac.Models;

namespace Quake3InstaGibLauncher.Mac.ViewModels;

/// <summary>
/// Schermata Impostazioni macOS: percorso dell'eseguibile e della cartella dati (due percorsi
/// distinti, a differenza di Windows), FOV, risoluzione video, pulsanti per aprire le cartelle,
/// aggiornare la scansione mappe, svuotare la cache anteprime e ripristinare i valori predefiniti.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly MacAppSettings _settings;
    private readonly Func<string, string, Task> _applyNewPaths;
    private readonly Func<Task> _refreshMaps;
    private readonly Action _clearCache;
    private readonly Action _resetToDefaults;
    private readonly Action<string> _openFolder;
    private readonly Func<Task<string?>> _pickExecutableFile;
    private readonly Func<Task<string?>> _pickDataFolder;
    private readonly Func<(int Width, int Height)?> _detectScreenResolution;

    [ObservableProperty] private string _executablePath;
    [ObservableProperty] private string _dataRootPath;
    [ObservableProperty] private int _fov;
    [ObservableProperty] private bool _fixAspectRatio;
    [ObservableProperty] private int _screenWidth;
    [ObservableProperty] private int _screenHeight;
    [ObservableProperty] private bool _fullscreen;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string _steamFriendInput;

    public SettingsViewModel(
        MacAppSettings settings,
        Func<string, string, Task> applyNewPaths,
        Func<Task> refreshMaps,
        Action clearCache,
        Action resetToDefaults,
        Action<string> openFolder,
        Func<Task<string?>> pickExecutableFile,
        Func<Task<string?>> pickDataFolder,
        Func<(int Width, int Height)?> detectScreenResolution)
    {
        _settings = settings;
        _applyNewPaths = applyNewPaths;
        _refreshMaps = refreshMaps;
        _clearCache = clearCache;
        _resetToDefaults = resetToDefaults;
        _openFolder = openFolder;
        _pickExecutableFile = pickExecutableFile;
        _pickDataFolder = pickDataFolder;
        _detectScreenResolution = detectScreenResolution;

        _executablePath = settings.ExecutablePath;
        _dataRootPath = settings.DataRootPath;
        _fov = settings.Fov;
        _fixAspectRatio = settings.FixAspectRatio;
        _screenWidth = settings.ScreenWidth;
        _screenHeight = settings.ScreenHeight;
        _fullscreen = settings.Fullscreen;
        _steamFriendInput = settings.SteamLastFriendId;
    }

    partial void OnFovChanged(int value) => _settings.Fov = value;
    partial void OnFixAspectRatioChanged(bool value) => _settings.FixAspectRatio = value;
    partial void OnScreenWidthChanged(int value) => _settings.ScreenWidth = value;
    partial void OnScreenHeightChanged(int value) => _settings.ScreenHeight = value;
    partial void OnFullscreenChanged(bool value) => _settings.Fullscreen = value;
    partial void OnSteamFriendInputChanged(string value) => _settings.SteamLastFriendId = value;

    /// <summary>Apre la finestra "Aggiungi amico" di Steam, dove si puo' cercare per nome
    /// direttamente dentro il client.</summary>
    [RelayCommand]
    private void SearchSteamFriend()
    {
        var opened = SteamChatService.OpenAddFriendSearch();
        StatusMessage = opened
            ? "Ricerca amici Steam aperta."
            : "Impossibile aprire Steam: verifica che sia installato.";
    }

    /// <summary>Avvia direttamente una chat con l'amico indicato (SteamID64 o link al profilo).</summary>
    [RelayCommand]
    private void StartSteamChat()
    {
        var opened = SteamChatService.TryOpenChat(SteamFriendInput);
        StatusMessage = opened
            ? "Chat Steam aperta."
            : "SteamID non riconosciuto: incolla il link numerico del profilo (es. steamcommunity.com/profiles/7656119...) oppure il SteamID64 a 17 cifre.";
    }

    [RelayCommand]
    private void DetectScreenResolution()
    {
        var detected = _detectScreenResolution();
        if (detected is { } d && d.Width > 0 && d.Height > 0)
        {
            ScreenWidth = d.Width;
            ScreenHeight = d.Height;
            StatusMessage = $"Risoluzione rilevata: {d.Width}x{d.Height}.";
        }
        else
        {
            StatusMessage = "Impossibile rilevare la risoluzione dello schermo.";
        }
    }

    [RelayCommand]
    private async Task BrowseExecutableAsync()
    {
        var picked = await _pickExecutableFile();
        if (picked is not null) ExecutablePath = picked;
    }

    [RelayCommand]
    private async Task BrowseDataFolderAsync()
    {
        var picked = await _pickDataFolder();
        if (picked is not null) DataRootPath = picked;
    }

    [RelayCommand]
    private async Task ApplyPathsAsync()
    {
        StatusMessage = "Verifica dei percorsi in corso...";
        await _applyNewPaths(ExecutablePath, DataRootPath);
        StatusMessage = "Percorsi aggiornati.";
    }

    [RelayCommand]
    private async Task RefreshMapsAsync()
    {
        IsRefreshing = true;
        StatusMessage = "Aggiornamento elenco mappe in corso...";
        try
        {
            await _refreshMaps();
            StatusMessage = "Elenco mappe aggiornato.";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        _clearCache();
        StatusMessage = "Cache anteprime cancellata. Rigenerazione in corso...";
        await RefreshMapsAsync();
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        _resetToDefaults();
        StatusMessage = "Impostazioni ripristinate ai valori predefiniti.";
    }

    [RelayCommand] private void OpenDataFolder() => _openFolder(DataRootPath);
    [RelayCommand] private void OpenBaseQ3Folder() => _openFolder(System.IO.Path.Combine(DataRootPath, "baseq3"));
    [RelayCommand] private void OpenModFolder() => _openFolder(System.IO.Path.Combine(DataRootPath, "InstaGib129"));
    [RelayCommand] private void OpenMissionPackFolder() => _openFolder(System.IO.Path.Combine(DataRootPath, "missionpack"));
}
