using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Models;

namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>
/// Schermata "Giocatori": elenco dei giocatori incontrati in partita, salvati manualmente o
/// aggiunti con un clic dal browser server multiplayer. Ogni giocatore ha una valutazione
/// (Forte/Amico/Da evitare/Neutrale) usata anche per evidenziarlo quando lo si ritrova in un
/// server online (vedi MultiplayerSetupViewModel.RefreshPlayerListsCommand).
/// </summary>
public partial class PlayersViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly Action _saveSettings;

    public ObservableCollection<KnownPlayerViewModel> Players { get; } = new();

    [ObservableProperty] private string _newPlayerName = string.Empty;
    [ObservableProperty] private PlayerRating _newPlayerRating = PlayerRating.Forte;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public IReadOnlyList<PlayerRating> AvailableRatings { get; } = Enum.GetValues<PlayerRating>();

    public PlayersViewModel(AppSettings settings, Action saveSettings)
    {
        _settings = settings;
        _saveSettings = saveSettings;

        foreach (var player in _settings.KnownPlayers.OrderByDescending(p => p.LastSeenUtc))
            Players.Add(Wrap(player));
    }

    private KnownPlayerViewModel Wrap(KnownPlayer player) => new(player, _saveSettings);

    [RelayCommand]
    private void AddPlayer()
    {
        var name = Quake3TextUtils.StripColorCodes(NewPlayerName).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Inserisci il nome del giocatore.";
            return;
        }

        AddOrUpdate(name, NewPlayerRating);
        NewPlayerName = string.Empty;
    }

    /// <summary>Aggiunge un giocatore nuovo o aggiorna quello esistente (stesso nome, senza fare
    /// distinzione tra maiuscole/minuscole). Usato sia dal form manuale sia dal browser server
    /// multiplayer ("Aggiungi ai conosciuti" su un giocatore trovato in un server).</summary>
    public void AddOrUpdate(string cleanName, PlayerRating rating)
    {
        var existing = _settings.KnownPlayers.FirstOrDefault(p => string.Equals(p.Name, cleanName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Rating = rating;
            existing.LastSeenUtc = DateTime.UtcNow;
            existing.TimesSeen++;

            var vm = Players.FirstOrDefault(p => ReferenceEquals(p.Model, existing));
            if (vm is not null)
            {
                Players.Remove(vm);
                Players.Insert(0, Wrap(existing));
            }
            StatusMessage = $"{cleanName} aggiornato ({RatingLabel(rating)}).";
        }
        else
        {
            var player = new KnownPlayer { Name = cleanName, Rating = rating };
            _settings.KnownPlayers.Add(player);
            Players.Insert(0, Wrap(player));
            StatusMessage = $"{cleanName} aggiunto ai giocatori conosciuti ({RatingLabel(rating)}).";
        }

        _saveSettings();
    }

    [RelayCommand]
    private void RemovePlayer(KnownPlayerViewModel? player)
    {
        if (player is null) return;
        _settings.KnownPlayers.Remove(player.Model);
        Players.Remove(player);
        _saveSettings();
        StatusMessage = $"{player.Name} rimosso dai giocatori conosciuti.";
    }

    /// <summary>Apre WhatsApp con un messaggio pronto che condivide il TUO gamertag, cosi' un
    /// amico puo' salvarti tra i suoi giocatori conosciuti e riconoscerti online. Stesso pattern
    /// "wa.me" gia' usato per l'invito a una partita (vedi Core.Services.ServerInviteService):
    /// l'utente sceglie il contatto e decide se inviare, l'app non manda nulla per conto suo.</summary>
    [RelayCommand]
    private void ShareGamertagViaWhatsApp()
    {
        var cleanName = Quake3TextUtils.StripColorCodes(_settings.PlayerProfile.ColoredName).Trim();
        if (string.IsNullOrWhiteSpace(cleanName)) cleanName = "Player";

        var message = $"🎮 Aggiungimi tra i tuoi giocatori conosciuti su Quake III InstaGib!\nIl mio nome in gioco: {cleanName}";
        ServerInviteService.OpenWhatsAppWithMessage(message);
        StatusMessage = "WhatsApp aperto con il tuo gamertag pronto da inviare.";
    }

    public bool IsKnown(string cleanName) =>
        _settings.KnownPlayers.Any(p => string.Equals(p.Name, cleanName, StringComparison.OrdinalIgnoreCase));

    public KnownPlayer? Find(string cleanName) =>
        _settings.KnownPlayers.FirstOrDefault(p => string.Equals(p.Name, cleanName, StringComparison.OrdinalIgnoreCase));

    private static string RatingLabel(PlayerRating rating) => rating switch
    {
        PlayerRating.Forte => "Forte",
        PlayerRating.DaEvitare => "Da evitare",
        PlayerRating.Amico => "Amico",
        _ => "Neutrale",
    };
}
