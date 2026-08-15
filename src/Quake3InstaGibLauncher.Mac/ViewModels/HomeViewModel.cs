using CommunityToolkit.Mvvm.ComponentModel;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Mac.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty] private bool _installationOk;
    [ObservableProperty] private string _installationSummary = "Verifica in corso...";
    [ObservableProperty] private int _mapCount;

    public void UpdateFromStatus(InstallationStatus status, int mapCount)
    {
        InstallationOk = status.IsValid;
        MapCount = mapCount;
        InstallationSummary = status.IsValid
            ? $"ioquake3 e InstaGib129 trovati correttamente · {mapCount} mappe individuate"
            : $"Installazione incompleta: {status.MissingItems.Count} elemento/i mancante/i (vedi Diagnostica)";
    }
}
