using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace Quake3InstaGibLauncher.Quest;

/// <summary>
/// Activity unica dell'app: e' un "pannello 2D" nella libreria del Quest, non un'esperienza VR
/// immersiva (niente OpenXR/Meta XR SDK). Il Quest esegue normalmente app Android piatte in una
/// finestra fluttuante nell'Home environment: e' il modo piu' semplice e affidabile per un
/// pannello di controllo come questo (server browser + tasto "Gioca"), senza dover reinventare
/// un motore 3D solo per un'interfaccia. Landscape fisso: piu' comodo da leggere/puntare col
/// controller Quest rispetto a un layout verticale.
/// </summary>
[Activity(
    Label = "Q3 InstaGib Quest",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/Icon",
    MainLauncher = true,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    /// <summary>
    /// Riferimento all'Activity corrente, usato da QuestLaunchService per avviare Quake3Quest
    /// SENZA il flag NEW_TASK. Bug reale segnalato dall'utente: avviando Quake3Quest da
    /// Application.Context (l'unico modo consentito da Android per farlo senza un'Activity: il
    /// flag NEW_TASK e' obbligatorio in quel caso) si crea un task separato nella cronologia
    /// recenti, quindi uscendo/tornando indietro da Quake3Quest il sistema NON torna a questa
    /// app ma alla Home del visore. Avviandolo invece da questa Activity, nello stesso task,
    /// il normale comportamento "indietro" di Android riporta qui.
    /// </summary>
    public static MainActivity? Current { get; private set; }

    protected override void OnResume()
    {
        base.OnResume();
        Current = this;
    }

    protected override void OnPause()
    {
        if (ReferenceEquals(Current, this))
            Current = null;
        base.OnPause();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
