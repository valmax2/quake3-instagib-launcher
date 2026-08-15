using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Quake3InstaGibLauncher.Mac.ViewModels;

/// <summary>Schermata "Guida e download", equivalente macOS di quella Windows.</summary>
public partial class HelpViewModel : ObservableObject
{
    [ObservableProperty] private string _statusMessage = string.Empty;

    // --- Crediti sviluppatore / contatti / donazione (stesso contenuto della versione Windows) ---
    public string StudioName => "VStudio Apps";
    public string StudioTagline => "Sviluppo Applicazioni iOS e Android";
    public string DeveloperEmail => "vstudioapps@gmail.com";
    public string PayPalUrl => "https://www.paypal.com/donate?business=vstudioapps@gmail.com&currency_code=EUR&item_name=Supporto+sviluppo+Quake+III+InstaGib+Launcher";
    public bool HasDonateLink => !string.IsNullOrWhiteSpace(PayPalUrl);

    public string? DeveloperLogoPath => ResolveLogoPngPath();

    private static string? ResolveLogoPngPath()
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Presets", "Logos", "vstudio_apps_bronze_quake_logo_2d.png");
            return System.IO.File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<HelpLinkItem> DownloadLinks { get; } = new[]
    {
        new HelpLinkItem(
            "Motore ioquake3 (macOS)",
            "Versione moderna e gratuita del motore di Quake III Arena per macOS.",
            "https://ioquake3.org/get-it/"),
        new HelpLinkItem(
            "Quake III Arena (baseq3)",
            "I file di gioco originali (pak0.pk3 e gli altri) NON sono scaricabili gratuitamente: vanno posseduti legittimamente, per esempio tramite Steam.",
            "https://store.steampowered.com/app/2200/Quake_III_Arena/"),
        new HelpLinkItem(
            "Mod InstaGib129",
            "Lo stesso file InstaGib129.pk3 usato su Windows funziona anche su macOS (i .pk3 sono bytecode multipiattaforma).",
            "http://www.instagibmod.com"),
        new HelpLinkItem(
            "Guida ufficiale ioquake3",
            "Documentazione ufficiale: installazione, cvar, FAQ, incluse le note specifiche per macOS.",
            "https://ioquake3.org/help/players-guide/"),
    };

    public IReadOnlyList<HelpStepItem> InstallSteps { get; } = new[]
    {
        new HelpStepItem(1, "Installa ioquake3",
            "Scarica ioquake3 per macOS e trascina ioquake3.app in /Applications (o in una sottocartella /Applications/ioquake3/)."),
        new HelpStepItem(2, "Prepara la cartella dati",
            "Crea ~/Library/Application Support/Quake3/baseq3 (nel tuo Home, cartella Libreria normalmente nascosta: da Finder usa Vai → Vai alla cartella... e incolla il percorso)."),
        new HelpStepItem(3, "Copia i file di Quake III Arena",
            "Copia pak0.pk3 (e gli altri pak*.pk3) dalla tua installazione originale di Quake III Arena dentro quella cartella baseq3."),
        new HelpStepItem(4, "Installa la mod InstaGib129",
            "Crea la sottocartella InstaGib129 accanto a baseq3 e mettici dentro InstaGib129.pk3."),
        new HelpStepItem(5, "Apri questo launcher",
            "Se hai seguito i percorsi standard l'app li trova da sola. Altrimenti vai in Impostazioni e seleziona manualmente l'eseguibile e la cartella dati: la Diagnostica ti dira' esattamente cosa manca."),
    };

    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch (Exception ex) { StatusMessage = $"Impossibile aprire il link: {ex.Message}"; }
    }
}
