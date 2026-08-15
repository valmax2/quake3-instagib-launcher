using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Models;
using Quake3InstaGibLauncher.Services;

namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>
/// Schermata Impostazioni: percorso di ioquake3, FOV, pulsanti per aprire le cartelle,
/// aggiornare la scansione mappe, svuotare la cache anteprime e ripristinare i valori predefiniti.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly Func<string, Task> _applyNewGameRoot;
    private readonly Func<Task> _refreshMaps;
    private readonly Action _clearCache;
    private readonly Action _resetToDefaults;
    private readonly Action<string> _openFolder;
    private readonly Action _saveSettings;

    [ObservableProperty] private string _gameRootPath;
    [ObservableProperty] private int _fov;
    [ObservableProperty] private bool _fixAspectRatio;
    [ObservableProperty] private int _screenWidth;
    [ObservableProperty] private int _screenHeight;
    [ObservableProperty] private bool _fullscreen;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isRefreshing;

    [ObservableProperty] private string _steamFriendInput;

    public SettingsViewModel(
        AppSettings settings,
        Func<string, Task> applyNewGameRoot,
        Func<Task> refreshMaps,
        Action clearCache,
        Action resetToDefaults,
        Action<string> openFolder,
        Action saveSettings)
    {
        _settings = settings;
        _applyNewGameRoot = applyNewGameRoot;
        _refreshMaps = refreshMaps;
        _clearCache = clearCache;
        _resetToDefaults = resetToDefaults;
        _saveSettings = saveSettings;
        _openFolder = openFolder;

        _gameRootPath = settings.GameRootPath;
        _fov = settings.Fov;
        _fixAspectRatio = settings.FixAspectRatio;
        _screenWidth = settings.ScreenWidth;
        _screenHeight = settings.ScreenHeight;
        _fullscreen = settings.Fullscreen;

        _steamFriendInput = settings.SteamLastFriendId;
    }

    partial void OnFovChanged(int value) { _settings.Fov = value; _saveSettings(); }
    partial void OnFixAspectRatioChanged(bool value) { _settings.FixAspectRatio = value; _saveSettings(); }
    partial void OnScreenWidthChanged(int value) { _settings.ScreenWidth = value; _saveSettings(); }
    partial void OnScreenHeightChanged(int value) { _settings.ScreenHeight = value; _saveSettings(); }
    partial void OnFullscreenChanged(bool value) { _settings.Fullscreen = value; _saveSettings(); }
    partial void OnSteamFriendInputChanged(string value) { _settings.SteamLastFriendId = value; _saveSettings(); }

    /// <summary>Apre la finestra "Aggiungi amico" di Steam, dove si puo' cercare per nome
    /// direttamente dentro il client (Steam stesso, non questa app, fa la ricerca).</summary>
    [RelayCommand]
    private void SearchSteamFriend()
    {
        var opened = SteamChatService.OpenAddFriendSearch();
        StatusMessage = opened
            ? "Ricerca amici Steam aperta."
            : "Impossibile aprire Steam: verifica che sia installato.";
    }

    /// <summary>Avvia direttamente una chat con l'amico indicato (SteamID64 o link al profilo,
    /// es. copiato con "Copia URL pagina" dal profilo Steam dell'amico).</summary>
    [RelayCommand]
    private void StartSteamChat()
    {
        var opened = SteamChatService.TryOpenChat(SteamFriendInput);
        StatusMessage = opened
            ? "Chat Steam aperta."
            : "SteamID non riconosciuto: incolla il link numerico del profilo (es. steamcommunity.com/profiles/7656119...) oppure il SteamID64 a 17 cifre.";
    }

    /// <summary>Ripristina la risoluzione rilevata automaticamente dal sistema (utile se l'utente
    /// l'ha modificata a mano e vuole tornare al valore corretto per il proprio monitor).</summary>
    [RelayCommand]
    private void DetectScreenResolution()
    {
        var (width, height) = DisplayInfo.GetPrimaryScreenResolution();
        if (width <= 0 || height <= 0)
        {
            StatusMessage = "Impossibile rilevare la risoluzione dello schermo.";
            return;
        }

        ScreenWidth = width;
        ScreenHeight = height;
        StatusMessage = $"Risoluzione rilevata: {width}x{height}.";
    }

    [RelayCommand]
    private void BrowseGameRoot()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Seleziona la cartella di installazione di ioquake3",
            InitialDirectory = Directory.Exists(GameRootPath) ? GameRootPath : @"C:\",
        };

        if (dialog.ShowDialog() == true)
            GameRootPath = dialog.FolderName;
    }

    [RelayCommand]
    private async Task ApplyGameRootAsync()
    {
        StatusMessage = "Verifica del nuovo percorso in corso...";
        await _applyNewGameRoot(GameRootPath);
        StatusMessage = "Percorso aggiornato.";
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

    [RelayCommand] private void OpenGameFolder() => _openFolder(GameRootPath);
    [RelayCommand] private void OpenBaseQ3Folder() => _openFolder(Path.Combine(GameRootPath, "baseq3"));
    [RelayCommand] private void OpenModFolder() => _openFolder(Path.Combine(GameRootPath, "InstaGib129"));
    [RelayCommand] private void OpenMissionPackFolder() => _openFolder(Path.Combine(GameRootPath, "missionpack"));
}
