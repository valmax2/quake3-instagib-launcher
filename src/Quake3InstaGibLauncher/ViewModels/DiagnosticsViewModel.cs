using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Services;

namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>
/// Sezione diagnostica: controlli sull'installazione, errori di scansione dei .pk3, ultimo
/// comando generato e percorso dell'ultimo file .cfg di rotazione scritto. Nessun dato
/// sensibile (CD key, password) viene mai mostrato o registrato qui.
/// </summary>
public partial class DiagnosticsViewModel : ObservableObject
{
    public ObservableCollection<DiagnosticCheck> Checks { get; } = new();
    public ObservableCollection<string> ScanErrors { get; } = new();

    [ObservableProperty] private string _lastGeneratedCommand = "(nessun avvio ancora effettuato in questa sessione)";
    [ObservableProperty] private string _lastCfgPath = "(nessun file di rotazione generato in questa sessione)";
    [ObservableProperty] private string _appDataFolder = AppPaths.Root;
    [ObservableProperty] private bool _allChecksPassed;

    /// <summary>Versione dell'app (dal tag &lt;Version&gt; nel .csproj), utile per capire al volo
    /// se si sta usando davvero l'ultima build ricevuta invece di una precedente rimasta in giro.</summary>
    public string AppVersion => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version is { } v
        ? $"{v.Major}.{v.Minor}.{v.Build}"
        : "sconosciuta";

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
