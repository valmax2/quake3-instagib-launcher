using System.Runtime.InteropServices;

namespace Quake3InstaGibLauncher.Services;

/// <summary>Rileva la risoluzione reale (pixel fisici, non "device independent pixel" WPF che
/// varierebbero con lo scaling DPI) dello schermo primario, per impostare automaticamente la
/// risoluzione corretta del gioco ed evitare le barre nere laterali.</summary>
public static class DisplayInfo
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public static (int Width, int Height) GetPrimaryScreenResolution()
    {
        var width = GetSystemMetrics(SM_CXSCREEN);
        var height = GetSystemMetrics(SM_CYSCREEN);
        return (width, height);
    }
}
