using System.IO;
using System.Text.Json;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Models;

namespace Quake3InstaGibLauncher.Services;

/// <summary>
/// Carica e salva le impostazioni persistenti dell'app in
/// %APPDATA%\Quake3InstaGibLauncher\settings.json. Scrittura atomica (file temporaneo + move)
/// per non corrompere le impostazioni in caso di chiusura improvvisa. Non salva mai CD key,
/// password del server in chiaro nei log, o altri dati sensibili.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        if (!File.Exists(AppPaths.SettingsFile))
            return ApplyDefaultLogoIfMissing(new AppSettings());

        try
        {
            var json = File.ReadAllText(AppPaths.SettingsFile);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            return ApplyDefaultLogoIfMissing(settings);
        }
        catch
        {
            // File corrotto o versione incompatibile: si riparte da valori predefiniti
            // piuttosto che far crashare l'app all'avvio.
            return ApplyDefaultLogoIfMissing(new AppSettings());
        }
    }

    /// <summary>
    /// Applica il logo VStudio Apps di default se non e' ancora impostato nessun logo
    /// personalizzato, ANCHE su un settings.json gia' esistente salvato prima che questa versione
    /// introducesse il valore di default (il campo LogoImagePath sul modello UiThemeSettings da
    /// solo non basta: la deserializzazione JSON sovrascrive il default del costruttore con
    /// qualunque valore fosse gia' presente nel file, inclusi null/vuoto salvati da versioni
    /// precedenti — bug reale osservato: un utente che aveva gia' usato l'app continuava a vedere
    /// il vecchio emblema vettoriale anche dopo aver ricevuto la build con il nuovo logo incluso).
    /// Non tocca nulla se l'utente ha gia' impostato un logo personalizzato proprio (anche uno
    /// diverso da quello predefinito): questo controllo interviene solo quando il campo e' vuoto.
    /// </summary>
    private static AppSettings ApplyDefaultLogoIfMissing(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Theme.LogoImagePath))
            return settings;

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Presets", "Logos", "vstudio_apps_bronze_quake_logo_360.gif");
            if (File.Exists(path))
            {
                settings.Theme.LogoImagePath = path;
                settings.Theme.LogoImageIsAnimatedGif = true;
            }
        }
        catch
        {
            // Percorso non risolvibile: si resta sull'emblema vettoriale, mai un crash qui.
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        AppPaths.EnsureDirectoriesExist();

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var tempFile = AppPaths.SettingsFile + ".tmp";
        File.WriteAllText(tempFile, json);
        File.Move(tempFile, AppPaths.SettingsFile, overwrite: true);
    }

    public void ResetToDefaults()
    {
        if (File.Exists(AppPaths.SettingsFile))
            File.Delete(AppPaths.SettingsFile);
    }
}
