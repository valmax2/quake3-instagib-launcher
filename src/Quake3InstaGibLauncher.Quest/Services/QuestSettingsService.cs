using System.Text.Json;
using Quake3InstaGibLauncher.Quest.Models;

namespace Quake3InstaGibLauncher.Quest.Services;

/// <summary>
/// Persiste QuestAppSettings come JSON nella cartella privata dell'app (Context.FilesDir):
/// non richiede alcun permesso di storage (a differenza della cartella condivisa di Quake3Quest),
/// e' sempre disponibile ed e' cancellata automaticamente se l'utente disinstalla l'app. Stesso
/// pattern/formato JSON di SettingsService (Windows) e MacSettingsService (Mac), solo con un
/// percorso diverso perche' su Android non esiste un "AppData" tradizionale.
/// </summary>
public sealed class QuestSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private string SettingsPath => Path.Combine(
        global::Android.App.Application.Context.FilesDir!.AbsolutePath, "settings.json");

    public QuestAppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<QuestAppSettings>(json, JsonOptions);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // JSON corrotto o illeggibile: si riparte da impostazioni pulite invece di bloccare l'app.
        }

        return new QuestAppSettings();
    }

    public void Save(QuestAppSettings settings)
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // Storage app-privata piena o non scrivibile: caso estremo, non deve mai far
            // crashare l'app solo per un salvataggio di preferenze non riuscito.
        }
    }
}
