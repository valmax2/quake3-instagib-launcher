using System.Windows;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Services;

/// <summary>
/// Applica la lingua dell'interfaccia sostituendo il dizionario risorse delle stringhe (stesso
/// pattern "sostituisci, non mutare" di ThemeService: le view usano {DynamicResource Str.xxx} cosi'
/// il cambio si vede subito, senza riavviare l'app).
///
/// COPERTURA ATTUALE: barra di navigazione e schermata Home. Le altre schermate (Locale,
/// Multiplayer, Impostazioni, Diagnostica, Guida, Personalizza, Giocatori) mostrano ancora testo
/// solo in italiano: la traduzione completa e' un lavoro incrementale successivo (tracciato come
/// attivita' separata), non fatto in questo passaggio per evitare di tradurre centinaia di stringhe
/// senza revisione accurata.
/// </summary>
public static class LocalizationService
{
    public static AppLanguage Current { get; private set; } = AppLanguage.Italian;

    public static void Apply(AppLanguage language)
    {
        Current = language;

        var source = language switch
        {
            AppLanguage.English => new Uri("Strings/Strings.en.xaml", UriKind.Relative),
            _ => new Uri("Strings/Strings.it.xaml", UriKind.Relative),
        };

        var resources = Application.Current.Resources;
        var existing = resources.MergedDictionaries
            .FirstOrDefault(d => d.Source is not null && d.Source.OriginalString.StartsWith("Strings/Strings.", StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            resources.MergedDictionaries.Remove(existing);

        resources.MergedDictionaries.Add(new ResourceDictionary { Source = source });
    }
}
