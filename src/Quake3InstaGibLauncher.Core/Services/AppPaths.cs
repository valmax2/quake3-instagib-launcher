using System.IO;

namespace Quake3InstaGibLauncher.Core.Services;

/// <summary>
/// Percorsi della cartella dati dell'applicazione (impostazioni, cache anteprime, cfg generati).
/// Su Windows vive sotto %APPDATA%\Quake3InstaGibLauncher; su macOS sotto
/// ~/Library/Application Support/Quake3InstaGibLauncher (convenzione standard di macOS, diversa
/// da quella restituita da Environment.SpecialFolder.ApplicationData su Unix). In nessun caso
/// vengono toccati i file del gioco, eccetto i piccoli .cfg di rotazione mappe generati
/// esplicitamente dall'utente (vedi RotationCfgService), scritti dentro la cartella del gioco stesso.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = ComputeRoot();

    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");
    public static string CacheDir { get; } = Path.Combine(Root, "cache");
    public static string PreviewsDir { get; } = Path.Combine(CacheDir, "previews");
    public static string MapsIndexFile { get; } = Path.Combine(CacheDir, "maps.json");
    public static string CfgHistoryDir { get; } = Path.Combine(Root, "cfg");
    public static string LogsDir { get; } = Path.Combine(Root, "logs");

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(CacheDir);
        Directory.CreateDirectory(PreviewsDir);
        Directory.CreateDirectory(CfgHistoryDir);
        Directory.CreateDirectory(LogsDir);
    }

    private static string ComputeRoot()
    {
        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "Quake3InstaGibLauncher");
        }

        // Windows (percorso principale supportato) e fallback generico per altri Unix.
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Quake3InstaGibLauncher");
    }
}
