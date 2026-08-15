using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>
/// Schermata "Guida e download": indica dove reperire i componenti necessari (motore ioquake3,
/// mod InstaGib129, Quake III Arena) e riepiloga i passi di installazione attesi da questo
/// launcher. Non scarica ne' installa nulla automaticamente: apre solo pagine ufficiali nel
/// browser predefinito, su richiesta esplicita dell'utente (click).
/// </summary>
public partial class HelpViewModel : ObservableObject
{
    [ObservableProperty] private string _statusMessage = string.Empty;

    // --- Crediti sviluppatore / contatti / donazione ---
    // Link "Donate" ufficiale di PayPal (business=email confermata dall'autore come collegata al
    // proprio conto PayPal): apre la pagina di pagamento PayPal gia' precompilata nel browser.
    public string StudioName => "VStudio Apps";
    public string StudioTagline => "Sviluppo Applicazioni iOS e Android";
    public string DeveloperEmail => "vstudioapps@gmail.com";
    public string PayPalUrl => "https://www.paypal.com/donate?business=vstudioapps@gmail.com&currency_code=EUR&item_name=Supporto+sviluppo+Quake+III+InstaGib+Launcher";
    public bool HasDonateLink => !string.IsNullOrWhiteSpace(PayPalUrl);

    /// <summary>Piccolo badge statico (non il logo animato, quello e' in Home) accanto ai crediti:
    /// stessa cartella Presets\Logos\ inclusa nell'app, nessuna dipendenza da personalizzazioni
    /// dell'utente. Null se il file non e' presente (build senza asset, o publish parziale):
    /// in quel caso l'immagine semplicemente non compare, mai un errore.</summary>
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
            "Motore ioquake3",
            "Versione moderna e gratuita del motore di Quake III Arena, necessaria per eseguire il gioco su Windows/Mac/Linux recenti.",
            "https://ioquake3.org/get-it/"),
        new HelpLinkItem(
            "Quake III Arena (baseq3)",
            "I file di gioco originali (pak0.pk3 e gli altri) NON sono scaricabili gratuitamente: vanno posseduti legittimamente, per esempio tramite Steam.",
            "https://store.steampowered.com/app/2200/Quake_III_Arena/"),
        new HelpLinkItem(
            "Mod InstaGib129",
            "La mod usata da questo launcher per le partite InstaGib (railgun a colpo singolo). Sito ufficiale dell'autore.",
            "http://www.instagibmod.com"),
        new HelpLinkItem(
            "Guida ufficiale ioquake3",
            "Documentazione ufficiale del progetto ioquake3: installazione, cvar, FAQ.",
            "https://ioquake3.org/help/players-guide/"),
    };

    public IReadOnlyList<HelpStepItem> InstallSteps { get; } = new[]
    {
        new HelpStepItem(1, "Installa ioquake3",
            "Scarica ed estrai ioquake3 in una cartella a tua scelta, per esempio C:\\Giochi\\ioquake3. Deve contenere ioquake3.x86_64.exe."),
        new HelpStepItem(2, "Copia i file di Quake III Arena",
            "Copia pak0.pk3 (e gli altri pak*.pk3) dalla tua installazione originale di Quake III Arena nella sottocartella baseq3\\ dentro la cartella di ioquake3."),
        new HelpStepItem(3, "Installa la mod InstaGib129",
            "Crea la sottocartella InstaGib129\\ e mettici dentro InstaGib129.pk3 scaricato dal sito ufficiale della mod."),
        new HelpStepItem(4, "(Facoltativo) Team Arena / missionpack",
            "Se possiedi l'espansione Team Arena, copia i suoi pak*.pk3 nella sottocartella missionpack\\ per sbloccare le mappe CTF aggiuntive."),
        new HelpStepItem(5, "Apri questo launcher",
            "Se hai usato il percorso predefinito C:\\Giochi\\ioquake3 l'app lo trova da sola. Altrimenti vai in Impostazioni e seleziona la cartella corretta: la Diagnostica ti dira' esattamente cosa manca, se manca qualcosa."),
    };

    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impossibile aprire il link: {ex.Message}";
        }
    }
}
