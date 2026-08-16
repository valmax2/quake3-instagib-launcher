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
/// Prepara la riga di comando e avvia Quake3Quest (com.drbeef.ioq3quest, il porting VR open
/// source di ioquake3 per Meta Quest di Team Beef/DrBeef - https://github.com/Team-Beef-Studios/ioq3quest).
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
    public const string QuakeQuestPackageName = "com.drbeef.ioq3quest";
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

    public bool IsQuakeQuestInstalled()
    {
        try
        {
            return global::Android.App.Application.Context.PackageManager?.GetLaunchIntentForPackage(QuakeQuestPackageName) is not null;
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

    private string? CheckCommonPreconditions()
    {
        if (!HasStorageAccess())
            return "Manca il permesso di accesso ai file. Concedilo e riprova.";

        if (!IsQuakeQuestInstalled())
            return "Quake3Quest non risulta installato su questo visore.";

        if (!Directory.Exists(SdCardEngineRoot))
            return "Apri prima Quake3Quest almeno una volta (crea lui la cartella dati), poi riprova da qui.";

        return null;
    }

    /// <summary>Si unisce a un server trovato dal browser. Non forza fs_game: come sul desktop, il
    /// motore negozia da solo la mod corretta col server durante la connessione (BuildJoinArguments,
    /// stessa logica, stessi cvar rete/profilo).</summary>
    public QuestLaunchOutcome PrepareAndJoin(ServerInfo server, PlayerProfile? player, int fov = 100, VideoSettings? video = null)
    {
        var precondition = CheckCommonPreconditions();
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

            return WriteCommandlineAndLaunch(args, $"Avvio Quake3Quest verso {server.HostName}...", video);
        }
        catch (Exception ex)
        {
            return new QuestLaunchOutcome(false, $"Errore durante l'avvio: {ex.Message}");
        }
    }

    /// <summary>Avvia una partita locale contro bot (allenamento), stessa logica/cvar del launcher
    /// desktop (BuildLocalArguments): mappa, numero/difficolta' bot, modalita' di gioco, mod
    /// InstaGib129 opzionale.</summary>
    public QuestLaunchOutcome PrepareAndLaunchBotMatch(LocalMatchOptions options, VideoSettings? video = null)
    {
        var precondition = CheckCommonPreconditions();
        if (precondition is not null) return new QuestLaunchOutcome(false, precondition);

        if (options.UseInstaGibMod && !IsInstaGibModInstalled())
            return new QuestLaunchOutcome(false, $"Mancano i file della mod: copia InstaGib129.pk3 dentro {InstaGibModPath} sul visore, poi riprova.");

        try
        {
            var args = CommandBuilder.BuildLocalArguments(options, rotationCfgFileName: null);
            return WriteCommandlineAndLaunch(args, $"Avvio partita contro {options.BotCount} bot su {options.MapTechnicalName}...", video);
        }
        catch (Exception ex)
        {
            return new QuestLaunchOutcome(false, $"Errore durante l'avvio: {ex.Message}");
        }
    }

    /// <summary>Ospita una partita (non si unisce a una esistente): stessa logica/cvar del launcher
    /// desktop (BuildMultiplayerArguments) - nome partita, mappa, bot, password, LAN/Internet.</summary>
    public QuestLaunchOutcome PrepareAndHost(MultiplayerMatchOptions options, VideoSettings? video = null)
    {
        var precondition = CheckCommonPreconditions();
        if (precondition is not null) return new QuestLaunchOutcome(false, precondition);

        if (options.UseInstaGibMod && !IsInstaGibModInstalled())
            return new QuestLaunchOutcome(false, $"Mancano i file della mod: copia InstaGib129.pk3 dentro {InstaGibModPath} sul visore, poi riprova.");

        try
        {
            var args = CommandBuilder.BuildMultiplayerArguments(options, rotationCfgFileName: null);
            return WriteCommandlineAndLaunch(args, $"Ospito \"{options.ServerName}\" su {options.MapTechnicalName}...", video);
        }
        catch (Exception ex)
        {
            return new QuestLaunchOutcome(false, $"Errore durante l'avvio: {ex.Message}");
        }
    }

    private QuestLaunchOutcome WriteCommandlineAndLaunch(List<string> engineArgs, string successMessage, VideoSettings? video = null)
    {
        var fullArgs = new List<string> { "+set", "fs_basepath", $"{SdCardEngineRoot}/" };
        fullArgs.AddRange(engineArgs);
        fullArgs.AddRange(BuildVideoOverrideArgs(video));

        File.WriteAllText(CommandlineTxtPath, JoinArgsForCommandlineTxt(fullArgs));

        // NOTA su "torna all'app dopo aver chiuso la partita": e' stato provato ad avviare
        // Quake3Quest nello STESSO task Android di questa app (niente NEW_TASK) per sfruttare il
        // normale "indietro" di Android - non ha risolto: l'utente continua a finire alla Home
        // del Quest invece che qui. Causa piu' probabile: Quake3Quest (motore nativo) chiude il
        // proprio processo direttamente quando si preme "Esci" invece di fare una normale
        // navigazione "indietro" Android, e questo scavalca qualunque impostazione di task facciamo
        // noi lato launcher. Percio' si torna al normale NEW_TASK (piu' prevedibile/sicuro: nessun
        // rischio che una chiusura brusca dell'altra app comprometta il nostro stesso task).
        var context = global::Android.App.Application.Context;

        var intent = context.PackageManager?.GetLaunchIntentForPackage(QuakeQuestPackageName);
        if (intent is null)
            return new QuestLaunchOutcome(false, "Impossibile creare l'avvio per Quake3Quest.");

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
