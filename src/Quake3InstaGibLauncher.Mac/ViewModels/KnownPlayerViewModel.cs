using CommunityToolkit.Mvvm.ComponentModel;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Mac.ViewModels;

/// <summary>
/// Wrapper bindabile attorno a un KnownPlayer (modello dati puro nel Core). Ogni modifica a
/// valutazione/note scrive subito nel modello condiviso e salva subito su disco.
/// </summary>
public partial class KnownPlayerViewModel : ObservableObject
{
    public KnownPlayer Model { get; }
    private readonly Action _persist;

    public string Name => Model.Name;
    public int TimesSeen => Model.TimesSeen;
    public string LastSeenText => Model.LastSeenUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string SightingSummary => Model.TimesSeen == 1
        ? $"Visto 1 volta — il {LastSeenText}"
        : $"Visto {Model.TimesSeen} volte — l'ultima il {LastSeenText}";

    [ObservableProperty] private PlayerRating _rating;
    [ObservableProperty] private string _notes;

    public KnownPlayerViewModel(KnownPlayer model, Action persist)
    {
        Model = model;
        _persist = persist;
        _rating = model.Rating;
        _notes = model.Notes;
    }

    partial void OnRatingChanged(PlayerRating value)
    {
        Model.Rating = value;
        _persist();
    }

    partial void OnNotesChanged(string value)
    {
        Model.Notes = value;
        _persist();
    }
}
