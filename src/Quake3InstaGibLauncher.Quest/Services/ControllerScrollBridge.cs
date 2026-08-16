namespace Quake3InstaGibLauncher.Quest.Services;

/// <summary>
/// Ponte statico tra l'input del controller Quest (letto in MainActivity, unico posto che riceve
/// eventi Android grezzi) e la UI Avalonia (che non ha accesso diretto agli eventi Activity).
/// direction: -1 = su, +1 = giu'. Un solo evento "a scatti" per movimento della levetta oltre la
/// soglia morta, non uno stream continuo: piu' semplice da gestire in modo affidabile lato UI
/// (scorre una "pagina" per volta) rispetto a uno scroll fluido pixel-per-pixel.
/// </summary>
public static class ControllerScrollBridge
{
    public static event Action<int>? ScrollRequested;

    public static void RaiseScroll(int direction) => ScrollRequested?.Invoke(direction);
}
