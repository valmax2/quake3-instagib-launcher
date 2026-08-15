using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Mac.Models;

namespace Quake3InstaGibLauncher.Mac.ViewModels;

/// <summary>
/// Schermata "Personaggio": nome (con codici colore Quake III cliccabili da una palette), stile e
/// colore del mirino, colori del modello. Applicati come cvar reali al lancio del gioco (vedi
/// Core.Services.CommandBuilder), sia in Locale sia in Multiplayer/Unisciti.
/// </summary>
public partial class PlayerProfileViewModel : ObservableObject
{
    private readonly Quake3InstaGibLauncher.Core.Models.PlayerProfile _profile;
    private readonly Action _saveSettings;

    [ObservableProperty] private string _coloredName;
    [ObservableProperty] private int _crosshairStyle;
    [ObservableProperty] private int _crosshairSize;
    [ObservableProperty] private string _crosshairColorHex;
    [ObservableProperty] private bool _crosshairHealthColor;
    [ObservableProperty] private bool _crosshairPulseOnHit;
    [ObservableProperty] private int _modelColor1;
    [ObservableProperty] private int _modelColor2;
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>1-8: i colori modello classici di Quake III, in ordine.</summary>
    public IReadOnlyList<ModelColorOption> AvailableModelColors { get; } = new[]
    {
        new ModelColorOption(1, "Bianco"), new ModelColorOption(2, "Rosso"), new ModelColorOption(3, "Verde"),
        new ModelColorOption(4, "Blu"), new ModelColorOption(5, "Giallo"), new ModelColorOption(6, "Grigio"),
        new ModelColorOption(7, "Viola"), new ModelColorOption(8, "Fucsia"),
    };

    public IReadOnlyList<int> AvailableCrosshairStyles { get; } = Enumerable.Range(1, 15).ToList();

    public PlayerProfileViewModel(MacAppSettings settings, Action saveSettings)
    {
        _profile = settings.PlayerProfile;
        _saveSettings = saveSettings;

        _coloredName = _profile.ColoredName;
        _crosshairStyle = _profile.CrosshairStyle;
        _crosshairSize = _profile.CrosshairSize;
        _crosshairColorHex = _profile.CrosshairColorHex;
        _crosshairHealthColor = _profile.CrosshairHealthColor;
        _crosshairPulseOnHit = _profile.CrosshairPulseOnHit;
        _modelColor1 = _profile.ModelColor1;
        _modelColor2 = _profile.ModelColor2;
    }

    partial void OnColoredNameChanged(string value) { _profile.ColoredName = value; _saveSettings(); }
    partial void OnCrosshairStyleChanged(int value) { _profile.CrosshairStyle = value; _saveSettings(); }
    partial void OnCrosshairSizeChanged(int value) { _profile.CrosshairSize = value; _saveSettings(); }
    partial void OnCrosshairColorHexChanged(string value) { _profile.CrosshairColorHex = value; _saveSettings(); }
    partial void OnCrosshairHealthColorChanged(bool value) { _profile.CrosshairHealthColor = value; _saveSettings(); }
    partial void OnCrosshairPulseOnHitChanged(bool value) { _profile.CrosshairPulseOnHit = value; _saveSettings(); }
    partial void OnModelColor1Changed(int value) { _profile.ModelColor1 = value; _saveSettings(); }
    partial void OnModelColor2Changed(int value) { _profile.ModelColor2 = value; _saveSettings(); }

    [RelayCommand]
    private void ResetName()
    {
        ColoredName = "^1Player";
        StatusMessage = "Nome ripristinato.";
    }
}

public sealed record ModelColorOption(int Value, string Name);
