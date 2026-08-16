using Android.App;
using Android.Content.PM;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Quake3InstaGibLauncher.Quest.Services;

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
    // Soglia morta sulla levetta (evita scroll accidentali per piccoli movimenti/drift del
    // controller) e tempo minimo tra uno "scatto" di scroll e il successivo (altrimenti la levetta
    // tenuta inclinata spara decine di eventi al secondo, troppo veloce da seguire).
    private const float StickDeadZone = 0.5f;
    private static readonly TimeSpan ScrollStepCooldown = TimeSpan.FromMilliseconds(220);
    private DateTime _lastScrollAt = DateTime.MinValue;

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    /// <summary>
    /// Levetta analogica del controller Quest: Android la espone come "generic motion event" con
    /// sorgente Joystick, asse Y (su/giu'). Usiamo Dispatch* invece di On* (primo tentativo, non
    /// funzionava sul visore reale): Dispatch* intercetta l'evento PRIMA che la gerarchia di View
    /// (inclusa la superficie di rendering di Avalonia) possa consumarlo, mentre On* scatta solo
    /// come fallback se nessuna View l'ha gia' gestito - probabile causa del mancato funzionamento.
    /// Non consumiamo mai l'evento (chiamiamo comunque base dopo): serve solo per "origliare" il
    /// movimento della levetta senza interferire con l'uso normale del puntatore laser/click.
    /// </summary>
    public override bool DispatchGenericMotionEvent(MotionEvent? e)
    {
        if (e is not null)
        {
            // Log diagnostico temporaneo (rimuovere una volta confermato che la levetta funziona):
            // permette di vedere via "adb logcat" se l'evento arriva davvero e con quale sorgente/asse,
            // per capire subito se il problema e' "non arriva nulla" o "arriva ma con dati diversi
            // da quelli attesi".
            Android.Util.Log.Debug("Q3InstaGibQuest", $"GenericMotion: source={e.Source} action={e.Action} axisY={e.GetAxisValue(Axis.Y)} axisHatY={e.GetAxisValue(Axis.HatY)}");

            if (e.Source.HasFlag(InputSourceType.Joystick) && e.Action == MotionEventActions.Move)
            {
                var y = e.GetAxisValue(Axis.Y);
                if (Math.Abs(y) < StickDeadZone)
                    y = e.GetAxisValue(Axis.HatY); // alcuni driver riportano il D-Pad analogico su HAT_Y invece che Y

                TryRaiseScroll(y);
            }
        }

        return base.DispatchGenericMotionEvent(e);
    }

    /// <summary>Fallback D-Pad: se il sistema Quest traduce la levetta in tasti direzionali
    /// standard per i pannelli 2D invece di un asse joystick grezzo.</summary>
    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e is not null && e.Action == KeyEventActions.Down)
        {
            Android.Util.Log.Debug("Q3InstaGibQuest", $"KeyDown: keyCode={e.KeyCode} source={e.Source}");

            if (e.KeyCode == Keycode.DpadUp) ControllerScrollBridge.RaiseScroll(-1);
            else if (e.KeyCode == Keycode.DpadDown) ControllerScrollBridge.RaiseScroll(1);
        }

        return base.DispatchKeyEvent(e);
    }

    private void TryRaiseScroll(float axisY)
    {
        if (MathF.Abs(axisY) < StickDeadZone) return;

        var now = DateTime.UtcNow;
        if (now - _lastScrollAt < ScrollStepCooldown) return;
        _lastScrollAt = now;

        ControllerScrollBridge.RaiseScroll(axisY > 0 ? 1 : -1);
    }
}
