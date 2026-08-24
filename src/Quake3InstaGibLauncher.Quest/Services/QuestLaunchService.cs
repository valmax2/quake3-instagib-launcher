using System.Text;
using Android.Content;
using Android.OS;
using Android.Provider;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Quest.Models;

namespace Quake3InstaGibLauncher.Quest.Services;

public sealed record QuestLaunchOutcome(bool Success, string Message);

/// <summary>
/// Prepara la riga di comando e avvia uno tra DUE motori installabili separatamente sul Quest,
/// a scelta dell'utente (interruttore "InstaGib / Originale" nella UI, vedi
/// QuestMainViewModel.UseInstaGibFork): il nostro fork InstaGib
/// (com.vstudioapps.ioq3quest.instagib, "Q3 InstaGib VR" nella libreria) oppure Quake3Quest
/// originale non modificato (com.drbeef.ioq3quest, installato dall'utente via SideQuest) - stesso
/// porting VR open source di ioquake3 per Meta Quest di Team Beef/DrBeef
/// (https://github.com/Team-Beef-Studios/ioq3quest), entrambi leggono la stessa cartella dati
/// condivisa (SdCardEngineRoot), quindi stesse mappe/mod visibili a entrambi.
///
/// APPLICATIONID DEL NOSTRO FORK DIVERSO DALL'ORIGINALE DI PROPOSITO (deciso il 17 agosto 2026):
/// prima riusava "com.drbeef.ioq3quest" e quindi SOSTITUIVA l'originale ad ogni installazione
/// (stessa identita', firma diversa => Android obbligava a disinstallare l'originale). Ora sono
/// due app separate, coesistono sul visore senza mai cancellarsi a vicenda - da qui la necessita'
/// di questo interruttore, per scegliere quale delle due lanciare.
///
/// Non esiste un equivalente Android di "avvia processo con argomenti da riga di comando" come
/// su desktop (Process.Start): le app Android si aprono con un Intent, non con argv. Questo
/// specifico motore pero' legge un file "commandline.txt" nella sua cartella dati ad ogni avvio e
/// lo tratta come se fosse la riga di comando (stesso meccanismo usato da molti port Android/SDL
/// di ioquake3) - e' quindi il vero equivalente Quest di CommandBuilder+Process.Start sul desktop.
/// Per questo riusiamo DIRETTAMENTE CommandBuilder (Core) per costruire gli argomenti, sia per
/// unirsi a un server (BuildJoinArguments) sia per una partita locale contro bot
/// (BuildLocalArguments): stessi cvar, stesso profilo giocatore, stessa logica gia' testata sul
/// launcher desktop, non duplicata qui.
///
/// CARTELLA REALE E MECCANISMO VERIFICATI VIA ADB SU HARDWARE REALE (Quest 3, sessione del
/// 2026-08-16): non e' "/sdcard/quake3/" come suggeriva la documentazione online (quella
/// convenzione vale per altri port/fork), ma "/sdcard/ioquake3quest/".
/// </summary>
public sealed class QuestLaunchService
{
    private readonly KeyBindingCfgService _keyBindingCfgService = new();

    /// <summary>Il nostro fork InstaGib (vedi doc del file). Scelta di default nell'interruttore
    /// dell'app: e' il motivo per cui questo launcher esiste.</summary>
    public const string InstaGibForkPackageName = "com.vstudioapps.ioq3quest.instagib";

    /// <summary>Quake3Quest originale non modificato di Team Beef/DrBeef, installato dall'utente
    /// via SideQuest - stesso motore, nessuna delle nostre modifiche InstaGib. Selezionabile
    /// dall'interruttore "InstaGib / Originale" per giocare a Quake III normale.</summary>
    public const string OriginalStablePackageName = "com.drbeef.ioq3quest";

    public const string SdCardEngineRoot = "/sdcard/ioquake3quest";
    public const string BaseGameFolderName = "baseq3";
    public const string InstaGibModFolderName = "InstaGib129";

    public string CommandlineTxtPath => Path.Combine(SdCardEngineRoot, "commandline.txt");
    public string BaseGamePath => Path.Combine(SdCardEngineRoot, BaseGameFolderName);
    public string InstaGibModPath => Path.Combine(SdCardEngineRoot, InstaGibModFolderName);

    /// <summary>Su Android 11+ (API 30+) serve il permesso speciale "Gestisci tutti i file" per
    /// poter scrivere nella cartella di un'altra app (quella di Quake3Quest). Sotto la 30, il
    /// permesso WRITE_EXTERNAL_STORAGE classico (gia' in manifest) basta.</summary>
    public bool HasStorageAccess()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            return Android.OS.Environment.IsExternalStorageManager;

        return true;
    }

    /// <summary>Apre la schermata di sistema per concedere l'accesso completo ai file. Il risultato
    /// si vede richiamando HasStorageAccess() quando l'utente torna nell'app (es. in OnResume).</summary>
    public void RequestStorageAccess()
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission);
        intent.SetData(Android.Net.Uri.Parse($"package:{context.PackageName}"));
        intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
    }

    public bool IsEngineInstalled(bool useInstaGibFork)
    {
        try
        {
            var packageName = useInstaGibFork ? InstaGibForkPackageName : OriginalStablePackageName;
            return global::Android.App.Application.Context.PackageManager?.GetLaunchIntentForPackage(packageName) is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>True se la cartella della mod InstaGib129 esiste e contiene almeno un file .pk3.</summary>
    public bool IsInstaGibModInstalled() =>
        Directory.Exists(InstaGibModPath) && Directory.EnumerateFiles(InstaGibModPath, "*.pk3").Any();

    /// <summary>Elenca le mappe reali trovate nei .pk3 di baseq3 e InstaGib129 sul visore, stesso
    /// scanner (Pk3Scanner) usato dal launcher desktop: nessuna lista finta o hardcoded.</summary>
    public Pk3ScanResult ScanMaps()
    {
        var scanner = new Pk3Scanner();
        var roots = new List<(string Directory, MapSource Source)> { (BaseGamePath, MapSource.BaseQ3) };
        if (IsInstaGibModInstalled())
            roots.Add((InstaGibModPath, MapSource.InstaGib129));

        return scanner.ScanInstallation(roots);
    }

    private string? CheckCommonPreconditions(bool useInstaGibFork)
    {
        if (!HasStorageAccess())
            return "Manca il permesso di accesso ai file. Concedilo e riprova.";

        if (!IsEngineInstalled(useInstaGibFork))
            return useInstaGibFork
                ? "Il motore InstaGib non risulta installato su questo visore."
                : "Quake3Quest originale non risulta installato su questo visore (installalo da SideQuest).";

        if (!Directory.Exists(SdCardEngineRoot))
            return "Apri prima il motore almeno una volta (crea lui la cartella dati), poi riprova da qui.";

        return null;
    }

    /// <summary>Si unisce a un server trovato dal browser. Non forza fs_game: come sul desktop, il
    /// motore negozia da solo la mod corretta col server durante la connessione (BuildJoinArguments,
    /// stessa logica, stessi cvar rete/profilo).</summary>
    public QuestLaunchOutcome PrepareAndJoin(ServerInfo server, PlayerProfile? player, int fov = 100, VideoSettings? video = null, IReadOnlyList<KeyBindingEntry>? keyBindings = null, bool useInstaGibFork = false)
    {
        var precondition = CheckCommonPreconditions(useInstaGibFork);
        if (precondition is not null) return new QuestLaunchOutcome(false, precondition);

        try
        {
            var args = CommandBuilder.BuildJoinArguments(
                server.Address, server.Port, modFolderHint: server.ModName, password: null,
                fov: fov,
                // false: su Quest la risoluzione/aspect ratio la gestisce il compositor VR
                // (r_mode -2 e' gia' impostato dall'autoexec.cfg di Quake3Quest stesso), forzarli
                // di nuovo qui rischierebbe solo di confliggere con il rendering stereo.
                fixAspectRatio: false, screenWidth: 0, screenHeight: 0, fullscreen: true,
                player: player);

            // Scriviamo sempre in BaseGamePath (mai nella cartella della mod): e' il server a
            // decidere la mod attiva in fase di connessione (vedi sopra), quindi baseq3 e' l'unica
            // cartella che "+exec" trova di sicuro qualunque sia la mod - stesso ragionamento del
            // launcher desktop (MultiplayerSetupViewModel.JoinSelectedServerAsync).
            AppendKeyBindings(args, keyBindings, BaseGamePath);

            return WriteCommandlineAndLaunch(args, $"Avvio verso {server.HostName}...", useInstaGibFork, video);
        }
        catch (Exception ex)
        {
            return new QuestLaunchOutcome(false, $"Errore durante l'avvio: {ex.Message}");
        }
    }

    /// <summary>Avvia una partita locale contro bot (allenamento), stessa logica/cvar del launcher
    /// desktop (BuildLocalArguments): mappa, numero/difficolta' bot, modalita' di gioco, mod
    /// InstaGib129 opzionale.</summary>
    public QuestLaunchOutcome PrepareAndLaunchBotMatch(LocalMatchOptions options, VideoSettings? video = null, IReadOnlyList<KeyBindingEntry>? keyBindings = null, bool useInstaGibFork = false)
    {
        var precondition = CheckCommonPreconditions(useInstaGibFork);
        if (precondition is not null) return new QuestLaunchOutcome(false, precondition);

        // Il controllo sul file InstaGib129.pk3 vale SOLO per il motore originale (SideQuest): li'
        // InstaGib e' davvero una mod caricata a runtime via fs_game, serve il file sul visore. Col
        // nostro fork InstaGib non e' affatto una mod: le regole (railgun+gauntlet, danno 999) sono
        // scritte nel codice C e compilate dentro l'app stessa (INSTAGIB_MODE in g_local.h) - nessun
        // .pk3 richiesto, il motore Quest non potrebbe comunque caricarlo a runtime (vedi doc di
        // classe). Prima questo controllo bloccava SEMPRE le partite bot sul fork con un errore
        // fuorviante ("mod mancante") anche se il fork gia' gioca InstaGib di suo.
        if (options.UseInstaGibMod && !useInstaGibFork && !IsInstaGibModInstalled())
            return new QuestLaunchOutcome(false, $"Mancano i file della mod: copia InstaGib129.pk3 dentro {InstaGibModPath} sul visore, poi riprova.");

        try
        {
            var args = CommandBuilder.BuildLocalArguments(options, rotationCfgFileName: null);
            // Stesso motivo del controllo sopra: la cartella InstaGib129 esiste solo se si usa il
            // motore originale con la mod reale copiata a mano. Sul fork non esiste (e non serve),
            // scriverci il file dei tasti fallirebbe sempre ("Cartella non trovata") - baseq3 va
            // sempre bene, il motore la consulta comunque (stesso ragionamento di PrepareAndJoin).
            AppendKeyBindings(args, keyBindings, (options.UseInstaGibMod && !useInstaGibFork) ? InstaGibModPath : BaseGamePath);
            return WriteCommandlineAndLaunch(args, $"Avvio partita contro {options.BotCount} bot su {options.MapTechnicalName}...", useInstaGibFork, video);
        }
        catch (Exception ex)
        {
            return new QuestLaunchOutcome(false, $"Errore durante l'avvio: {ex.Message}");
        }
    }

    /// <summary>Ospita una partita (non si unisce a una esistente): stessa logica/cvar del launcher
    /// desktop (BuildMultiplayerArguments) - nome partita, mappa, bot, password, LAN/Internet.</summary>
    public QuestLaunchOutcome PrepareAndHost(MultiplayerMatchOptions options, VideoSettings? video = null, IReadOnlyList<KeyBindingEntry>? keyBindings = null, bool useInstaGibFork = false)
    {
        var precondition = CheckCommonPreconditions(useInstaGibFork);
        if (precondition is not null) return new QuestLaunchOutcome(false, precondition);

        // Vedi commento gemello in PrepareAndLaunchBotMatch: il controllo sul .pk3 vale solo per il
        // motore originale, il fork ha InstaGib incorporato nel codice compilato.
        if (options.UseInstaGibMod && !useInstaGibFork && !IsInstaGibModInstalled())
            return new QuestLaunchOutcome(false, $"Mancano i file della mod: copia InstaGib129.pk3 dentro {InstaGibModPath} sul visore, poi riprova.");

        try
        {
            var args = CommandBuilder.BuildMultiplayerArguments(options, rotationCfgFileName: null);
            // Vedi commento gemello in PrepareAndLaunchBotMatch.
            AppendKeyBindings(args, keyBindings, (options.UseInstaGibMod && !useInstaGibFork) ? InstaGibModPath : BaseGamePath);
            return WriteCommandlineAndLaunch(args, $"Ospito \"{options.ServerName}\" su {options.MapTechnicalName}...", useInstaGibFork, video);
        }
        catch (Exception ex)
        {
            return new QuestLaunchOutcome(false, $"Errore durante l'avvio: {ex.Message}");
        }
    }

    /// <summary>Scrive (se ci sono bind attivi) il file .cfg dei tasti/comandi nella cartella
    /// indicata e aggiunge "+exec &lt;file&gt;" agli argomenti, stesso schema usato dal launcher
    /// desktop in tutti e tre i percorsi di avvio (locale/ospita/unisciti).</summary>
    private void AppendKeyBindings(List<string> args, IReadOnlyList<KeyBindingEntry>? keyBindings, string targetDirectory)
    {
        if (keyBindings is not { Count: > 0 }) return;

        var bindingsFileName = _keyBindingCfgService.WriteBindingsCfg(targetDirectory, keyBindings);
        if (bindingsFileName is not null)
        {
            args.Add("+exec");
            args.Add(bindingsFileName);
        }
    }

    private QuestLaunchOutcome WriteCommandlineAndLaunch(List<string> engineArgs, string successMessage, bool useInstaGibFork, VideoSettings? video = null)
    {
        var fullArgs = new List<string> { "+set", "fs_basepath", $"{SdCardEngineRoot}/" };
        fullArgs.AddRange(engineArgs);
        fullArgs.AddRange(BuildVideoOverrideArgs(video));

        File.WriteAllText(CommandlineTxtPath, JoinArgsForCommandlineTxt(fullArgs));

        // NOTA su "torna all'app dopo aver chiuso la partita": e' stato provato ad avviare il
        // motore nello STESSO task Android di questa app (niente NEW_TASK) per sfruttare il
        // normale "indietro" di Android - non ha risolto (per il nostro fork InstaGib ora risolto
        // diversamente: il motore stesso richiama il launcher prima di uscire, vedi Sys_Exit nel
        // repo del motore). Causa: il motore nativo chiude il proprio processo direttamente quando
        // si preme "Esci" invece di fare una normale navigazione "indietro" Android, e questo
        // scavalca qualunque impostazione di task facciamo noi lato launcher. Percio' qui restiamo
        // su NEW_TASK (piu' prevedibile/sicuro: nessun rischio che una chiusura brusca dell'altra
        // app comprometta il nostro stesso task) - vale sia per il nostro fork sia per l'originale,
        // che non ha (e non avra' mai) quella modifica lato motore.
        var context = global::Android.App.Application.Context;
        var packageName = useInstaGibFork ? InstaGibForkPackageName : OriginalStablePackageName;

        var intent = context.PackageManager?.GetLaunchIntentForPackage(packageName);
        if (intent is null)
            return new QuestLaunchOutcome(false, "Impossibile creare l'avvio per il motore selezionato.");

        intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);

        return new QuestLaunchOutcome(true, successMessage);
    }

    /// <summary>Cvar video applicati SOLO se l'utente li ha esplicitamente cambiati dai valori di
    /// fabbrica (0/null): luminosita' (r_gamma) e anti-aliasing (r_ext_multisample) sono cvar
    /// standard ioquake3, dovrebbero esistere su qualunque fork incluso questo. La densita' di
    /// rendering VR e' un tentativo best-effort (vr_pixelDensity): non verificato su hardware
    /// reale quale sia il nome esatto usato da questo specifico fork, innocuo se il motore lo
    /// ignora perche' sconosciuto.</summary>
    private static IEnumerable<string> BuildVideoOverrideArgs(VideoSettings? video)
    {
        if (video is null) yield break;

        if (video.AntiAliasingSamples is { } samples && samples > 0)
        {
            yield return "+set"; yield return "r_ext_multisample"; yield return samples.ToString();
        }

        if (video.Gamma is { } gamma && Math.Abs(gamma - 1.0) > 0.001)
        {
            yield return "+set"; yield return "r_gamma"; yield return gamma.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (video.VrPixelDensity is { } density && Math.Abs(density - 1.0) > 0.001)
        {
            yield return "+set"; yield return "vr_pixelDensity"; yield return density.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (video.HeightAdjust is { } heightAdjust && Math.Abs(heightAdjust) > 0.001)
        {
            yield return "+set"; yield return "vr_heightAdjust"; yield return heightAdjust.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>CommandBuilder produce una lista di token gia' pronta per ProcessStartInfo.ArgumentList
    /// (dove ogni elemento e' passato al processo esattamente com'e', spazi inclusi, senza quoting
    /// manuale). commandline.txt invece e' testo semplice che il motore ritokenizza da solo: un
    /// token con spazi (es. un nome colorato "^1Mario Rossi" o un hostname) andrebbe letto come due
    /// argomenti separati se non lo racchiudiamo tra virgolette qui.</summary>
    private static string JoinArgsForCommandlineTxt(IEnumerable<string> tokens)
    {
        var sb = new StringBuilder();
        foreach (var token in tokens)
        {
            if (sb.Length > 0) sb.Append(' ');
            if (token.Contains(' '))
                sb.Append('"').Append(token).Append('"');
            else
                sb.Append(token);
        }
        return sb.ToString();
    }
}
