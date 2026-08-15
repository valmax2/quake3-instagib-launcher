using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>
/// Logica condivisa della galleria mappe (ricerca, filtri, preferiti, rotazione, anteprima
/// ingrandita) riutilizzata sia dalla schermata Locale sia da quella Multiplayer.
/// </summary>
public partial class MapGalleryPanelViewModel : ObservableObject
{
    private readonly List<MapCardViewModel> _allMaps = new();
    private readonly Action _onFavoritesChanged;
    private readonly Action _onRotationChanged;

    public ObservableCollection<MapCardViewModel> FilteredMaps { get; } = new();
    public ObservableCollection<MapCardViewModel> RotationMaps { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _filterFfa;
    [ObservableProperty] private bool _filterTeam;
    [ObservableProperty] private bool _filterTournament;
    [ObservableProperty] private bool _filterCtf;
    [ObservableProperty] private bool _filterBaseQ3;
    [ObservableProperty] private bool _filterMissionPack;
    [ObservableProperty] private bool _filterCustom;
    [ObservableProperty] private bool _favoritesOnly;
    [ObservableProperty] private MapCardViewModel? _previewMap;
    [ObservableProperty] private MapCardViewModel? _selectedMap;
    [ObservableProperty] private string _rotationWarning = string.Empty;

    public MapGalleryPanelViewModel(Action onFavoritesChanged, Action onRotationChanged)
    {
        _onFavoritesChanged = onFavoritesChanged;
        _onRotationChanged = onRotationChanged;
    }

    /// <summary>
    /// Le mappe di missionpack (Team Arena) vivono in una cartella dati diversa (fs_basegame
    /// missionpack) da quelle normali: il motore idTech3 legge fs_basegame UNA VOLTA all'avvio e
    /// non lo ricarica passando da una mappa all'altra durante una rotazione (nessun fs_restart
    /// automatico a metà partita). Mischiare mappe missionpack e non-missionpack nella stessa
    /// rotazione fa quindi fallire il caricamento non appena la rotazione arriva a una mappa che
    /// vive nella cartella "sbagliata" per la sessione in corso — causa reale del crash/kick a
    /// metà partita segnalato dall'utente. Qui lo blocchiamo alla radice, prima che si formi una
    /// rotazione incompatibile, invece di lasciare che l'errore emerga solo al lancio del gioco.
    /// </summary>
    private static bool AreCompatible(MapCardViewModel a, MapCardViewModel b) =>
        a.Info.RequiresMissionPackBaseGame == b.Info.RequiresMissionPackBaseGame;

    /// <summary>Ricarica l'elenco mappe. <paramref name="lastUsedMapTechnicalName"/> e' il nome
    /// tecnico (non composito) dell'ultima mappa usata in questa modalita', usato per l'evidenza
    /// "ultima mappa utilizzata" e come selezione iniziale.</summary>
    public void SetMaps(IEnumerable<MapCardViewModel> maps, string? lastUsedMapTechnicalName)
    {
        _allMaps.Clear();
        _allMaps.AddRange(maps);

        foreach (var m in _allMaps)
            m.IsRecent = lastUsedMapTechnicalName is not null &&
                         string.Equals(m.TechnicalName, lastUsedMapTechnicalName, StringComparison.OrdinalIgnoreCase);

        if (SelectedMap is null && _allMaps.Count > 0)
            SelectedMap = _allMaps.FirstOrDefault(m => m.IsRecent) ?? _allMaps.FirstOrDefault();

        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnFilterFfaChanged(bool value) => ApplyFilters();
    partial void OnFilterTeamChanged(bool value) => ApplyFilters();
    partial void OnFilterTournamentChanged(bool value) => ApplyFilters();
    partial void OnFilterCtfChanged(bool value) => ApplyFilters();
    partial void OnFilterBaseQ3Changed(bool value) => ApplyFilters();
    partial void OnFilterMissionPackChanged(bool value) => ApplyFilters();
    partial void OnFilterCustomChanged(bool value) => ApplyFilters();
    partial void OnFavoritesOnlyChanged(bool value) => ApplyFilters();

    public void ApplyFilters()
    {
        var noTypeFilter = !FilterFfa && !FilterTeam && !FilterTournament && !FilterCtf;
        var noSourceFilter = !FilterBaseQ3 && !FilterMissionPack && !FilterCustom;
        var search = SearchText.Trim();

        IEnumerable<MapCardViewModel> query = _allMaps;

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(m =>
                m.TechnicalName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                m.LongName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!noTypeFilter)
        {
            query = query.Where(m =>
                (FilterFfa && m.SupportedGameTypes.Contains(GameType.FreeForAll)) ||
                (FilterTeam && m.SupportedGameTypes.Contains(GameType.TeamDeathmatch)) ||
                (FilterTournament && m.SupportedGameTypes.Contains(GameType.Tournament)) ||
                (FilterCtf && m.SupportedGameTypes.Contains(GameType.CaptureTheFlag)));
        }

        if (!noSourceFilter)
        {
            query = query.Where(m =>
                (FilterBaseQ3 && m.Source == MapSource.BaseQ3) ||
                (FilterMissionPack && m.Source == MapSource.MissionPack) ||
                (FilterCustom && (m.Source == MapSource.Custom || m.Source == MapSource.InstaGib129)));
        }

        if (FavoritesOnly)
            query = query.Where(m => m.IsFavorite);

        FilteredMaps.Clear();
        foreach (var m in query.OrderBy(m => m.LongName, StringComparer.OrdinalIgnoreCase))
            FilteredMaps.Add(m);
    }

    [RelayCommand]
    private void ToggleFavorite(MapCardViewModel? map)
    {
        if (map is null) return;
        map.IsFavorite = !map.IsFavorite;
        _onFavoritesChanged();
        if (FavoritesOnly) ApplyFilters();
    }

    [RelayCommand]
    private void OpenPreview(MapCardViewModel? map)
    {
        if (map is null) return;
        PreviewMap = map;
        SelectedMap = map;
    }

    [RelayCommand]
    private void ClosePreview() => PreviewMap = null;

    [RelayCommand]
    private void SelectMap(MapCardViewModel? map)
    {
        if (map is null) return;

        // Se la nuova mappa iniziale non e' compatibile con una rotazione gia' impostata
        // (missionpack vs normale), la rotazione va svuotata: tenerla porterebbe a un crash a
        // meta' partita non appena si arriva alla mappa incompatibile.
        if (RotationMaps.Count > 0 && RotationMaps.Any(r => !AreCompatible(r, map)))
        {
            RotationMaps.Clear();
            RotationWarning = "Rotazione svuotata: non puoi mischiare mappe Team Arena (missionpack) con mappe normali nella stessa rotazione, il motore va in crash a meta' partita.";
            _onRotationChanged();
        }

        SelectedMap = map;
    }

    [RelayCommand]
    private void AddToRotation(MapCardViewModel? map)
    {
        if (map is null) return;

        var incompatibleWith = SelectedMap is not null && !AreCompatible(SelectedMap, map)
            ? SelectedMap
            : RotationMaps.FirstOrDefault(r => !AreCompatible(r, map));

        if (incompatibleWith is not null)
        {
            RotationWarning = map.Info.RequiresMissionPackBaseGame
                ? $"\"{map.LongName}\" e' una mappa Team Arena (missionpack): non puoi metterla in rotazione insieme a \"{incompatibleWith.LongName}\", il motore va in crash a meta' partita passando dall'una all'altra."
                : $"\"{incompatibleWith.LongName}\" nella rotazione e' Team Arena (missionpack): non puoi aggiungere \"{map.LongName}\" (mappa normale) nella stessa rotazione, il motore va in crash a meta' partita.";
            return;
        }

        RotationWarning = string.Empty;
        RotationMaps.Add(map);
        _onRotationChanged();
    }

    [RelayCommand]
    private void RemoveFromRotation(MapCardViewModel? map)
    {
        if (map is null) return;
        RotationMaps.Remove(map);
        _onRotationChanged();
    }

    [RelayCommand]
    private void MoveRotationUp(MapCardViewModel? map)
    {
        if (map is null) return;
        var index = RotationMaps.IndexOf(map);
        if (index > 0) RotationMaps.Move(index, index - 1);
        _onRotationChanged();
    }

    [RelayCommand]
    private void MoveRotationDown(MapCardViewModel? map)
    {
        if (map is null) return;
        var index = RotationMaps.IndexOf(map);
        if (index >= 0 && index < RotationMaps.Count - 1) RotationMaps.Move(index, index + 1);
        _onRotationChanged();
    }

    [RelayCommand]
    private void ClearRotation()
    {
        RotationMaps.Clear();
        _onRotationChanged();
    }
}
