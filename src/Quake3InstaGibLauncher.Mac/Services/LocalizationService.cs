using Avalonia;
using Avalonia.Controls;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Mac.Services;

/// <summary>
/// Applica la lingua dell'interfaccia sostituendo un dizionario risorse di stringhe (stesso
/// pattern "sostituisci, non mutare" usato per i brush a immagine di ThemeService): le view usano
/// {DynamicResource Str.xxx} cosi' il cambio si vede subito, senza riavviare l'app.
///
/// COPERTURA: barra di navigazione, schermata Home, e le tre schermate nuove di questa sessione
/// (Giocatori, Tasti, Personaggio) — stesso ambito scelto per la versione Windows in questo stesso
/// passaggio di lavoro. Le altre schermate (Locale, Multiplayer, Impostazioni, Diagnostica, Guida,
/// Personalizza) restano solo in italiano: tradurle tutte e' un lavoro incrementale separato.
/// </summary>
public static class LocalizationService
{
    public static AppLanguage Current { get; private set; } = AppLanguage.Italian;

    public static void Apply(AppLanguage language)
    {
        Current = language;

        var dict = language == AppLanguage.English ? Strings.English : Strings.Italian;
        var resources = new ResourceDictionary();
        foreach (var kv in dict)
            resources[kv.Key] = kv.Value;

        var app = Application.Current!;
        // Rimuove il dizionario stringhe precedente (se presente) e aggiunge quello nuovo: stesso
        // approccio "sostituisci l'intero dizionario" della versione Windows, cosi' ogni chiave
        // mancante nella lingua nuova non lascia residui della lingua precedente.
        for (var i = app.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            if (app.Resources.MergedDictionaries[i] is ResourceDictionary rd && rd.ContainsKey("__q3ilauncher_strings__"))
                app.Resources.MergedDictionaries.RemoveAt(i);
        }
        resources["__q3ilauncher_strings__"] = true; // marcatore per ritrovarlo la prossima volta
        app.Resources.MergedDictionaries.Add(resources);
    }
}
