namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>Esito di un singolo controllo diagnostico, in linguaggio comprensibile anche a non tecnici.</summary>
public sealed class DiagnosticCheck
{
    public required string Title { get; init; }
    public required bool Success { get; init; }
    public required string Detail { get; init; }
}

/// <summary>Risultato aggregato della verifica dell'installazione, mostrato nella schermata iniziale.
/// Comune a tutte le piattaforme: ogni piattaforma produce questo stesso oggetto a partire dai
/// propri controlli specifici (percorsi Windows, percorsi macOS, ecc.).</summary>
public sealed class InstallationStatus
{
    public bool IsValid { get; set; }
    public List<string> MissingItems { get; set; } = new();
    public List<DiagnosticCheck> Checks { get; set; } = new();
}
