using CommunityToolkit.Mvvm.ComponentModel;
using Quake3InstaGibLauncher.Models;

namespace Quake3InstaGibLauncher.Services;

/// <summary>
/// Singleton osservabile che espone la modalita' di stile pulsanti scelta dall'utente (Piatto /
/// Rialzato / Bagliore) cosi' che il ControlTemplate condiviso in Theme.xaml possa reagire dal
/// vivo tramite un DataTrigger con Source="{x:Static}", senza dover ricaricare la finestra.
/// </summary>
public sealed partial class ButtonStyleState : ObservableObject
{
    public static ButtonStyleState Instance { get; } = new();

    [ObservableProperty] private ButtonStyleMode _currentMode = ButtonStyleMode.Flat;

    private ButtonStyleState() { }
}
