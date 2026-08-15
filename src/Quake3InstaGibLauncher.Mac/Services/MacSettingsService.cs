using System.Text.Json;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Mac.Models;

namespace Quake3InstaGibLauncher.Mac.Services;

/// <summary>
/// Carica e salva le impostazioni persistenti dell'app in
/// ~/Library/Application Support/Quake3InstaGibLauncher/settings.json (vedi Core.Services.AppPaths,
/// che risolve automaticamente il percorso corretto per macOS). Scrittura atomica. Non salva mai
/// CD key, password del server in chiaro nei log, o altri dati sensibili.
/// </summary>
public sealed class MacSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public MacAppSettings Load()
    {
        if (!File.Exists(AppPaths.SettingsFile))
            return ApplyDefaultLogoIfMissing(new MacAppSettings());

        try
        {
            var json = File.ReadAllText(AppPaths.SettingsFile);
            var settings = JsonSerializer.Deserialize<MacAppSettings>(json, JsonOptions) ?? new MacAppSettings();
            return ApplyDefaultLogoIfMissing(settings);
        }
        catch
        {
            return ApplyDefaultLogoIfMissing(new MacAppSettings());
        }
    }

    /// <summary>Stesso fix della versione Windows (vedi SettingsService.cs): applica il logo
    /// VStudio Apps di default anche su un settings.json gia' esistente dove il campo
    /// LogoImagePath e' vuoto, non solo su un'installazione nuova. Non tocca nulla se l'utente ha
    /// gia' impostato un logo personalizzato proprio.</summary>
    private static MacAppSettings ApplyDefaultLogoIfMissing(MacAppSettings settings)
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

    public void Save(MacAppSettings settings)
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
