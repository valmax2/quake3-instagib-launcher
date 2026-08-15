using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;

namespace Quake3InstaGibLauncher.Mac.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject
{
    public ObservableCollection<DiagnosticCheck> Checks { get; } = new();
    public ObservableCollection<string> ScanErrors { get; } = new();

    [ObservableProperty] private string _lastGeneratedCommand = "(nessun avvio ancora effettuato in questa sessione)";
    [ObservableProperty] private string _lastCfgPath = "(nessun file di rotazione generato in questa sessione)";
    [ObservableProperty] private string _appDataFolder = AppPaths.Root;
    [ObservableProperty] private bool _allChecksPassed;

    public void UpdateChecks(InstallationStatus status)
    {
        Checks.Clear();
        foreach (var c in status.Checks) Checks.Add(c);
        AllChecksPassed = status.IsValid;
    }

    public void UpdateScanErrors(IEnumerable<string> errors)
    {
        ScanErrors.Clear();
        foreach (var e in errors) ScanErrors.Add(e);
    }

    public void ReportLaunch(string command, string? cfgPath)
    {
        LastGeneratedCommand = command;
        if (cfgPath is not null) LastCfgPath = cfgPath;
    }
}
