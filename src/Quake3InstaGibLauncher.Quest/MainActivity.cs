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
    /// sorgente Joystick, asse Y (su/giu'). Non verificato su hardware reale in questa sessione
    /// se il sistema Quest la mappa gia' automaticamente a D-Pad per i pannelli 2D (in tal caso
    /// arriverebbe da OnKeyDown, gestito sotto, e questo metodo non scatterebbe mai) o se arriva
    /// davvero come asse joystick grezzo: gestiamo entrambe le strade per sicurezza.
    /// </summary>
    public override bool OnGenericMotionEvent(MotionEvent? e)
    {
        if (e is not null &&
            e.Source.HasFlag(InputSourceType.Joystick) &&
            e.Action == MotionEventActions.Move)
        {
            var y = e.GetAxisValue(Axis.Y);
            TryRaiseScroll(y);
        }

        return base.OnGenericMotionEvent(e);
    }

    /// <summary>Fallback D-Pad: se il sistema Quest traduce la levetta in tasti direzionali
    /// standard per i pannelli 2D (comportamento comune per le app Android non-VR sul Quest),
    /// arrivano qui invece che in OnGenericMotionEvent.</summary>
    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e)
    {
        if (keyCode == Keycode.DpadUp) { ControllerScrollBridge.RaiseScroll(-1); return true; }
        if (keyCode == Keycode.DpadDown) { ControllerScrollBridge.RaiseScroll(1); return true; }

        return base.OnKeyDown(keyCode, e);
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
