using CommunityToolkit.Mvvm.ComponentModel;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Mac.ViewModels;

/// <summary>Avvolge un MapInfo con lo stato di UI (preferito, ultima mappa usata, selezione).</summary>
public partial class MapCardViewModel : ObservableObject
{
    public MapInfo Info { get; }

    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isRecent;

    public MapCardViewModel(MapInfo info) => Info = info;

    public string TechnicalName => Info.TechnicalName;
    public string LongName => Info.LongName;
    public string SourceLabel => Info.SourceLabel;
    public MapSource Source => Info.Source;
    public string? PreviewPath => Info.CachedPreviewPath;
    public bool HasPreview => !string.IsNullOrEmpty(Info.CachedPreviewPath);
    public bool HasArenaMetadata => Info.HasArenaMetadata;

    public string GameTypesText => Info.SupportedGameTypes.Count == 0
        ? "Deathmatch"
        : string.Join(" · ", Info.SupportedGameTypes.Select(t => t.ToDisplayName()));

    public IReadOnlyList<GameType> SupportedGameTypes => Info.SupportedGameTypes;
}
