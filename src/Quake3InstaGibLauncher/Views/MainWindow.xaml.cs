using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Quake3InstaGibLauncher.ViewModels;

namespace Quake3InstaGibLauncher.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;

        // Versione in coda al titolo: modo rapido per verificare a colpo d'occhio se si sta
        // usando davvero l'ultima build ricevuta invece di una precedente rimasta in giro.
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (version is not null)
            Title = $"{Title} v{version.Major}.{version.Minor}.{version.Build}";
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (vm.Settings.WindowWidth > 0) Width = vm.Settings.WindowWidth;
        if (vm.Settings.WindowHeight > 0) Height = vm.Settings.WindowHeight;
        if (vm.Settings.WindowMaximized) WindowState = WindowState.Maximized;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        vm.Settings.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            vm.Settings.WindowWidth = Width;
            vm.Settings.WindowHeight = Height;
        }

        vm.SaveSettingsQuiet();
    }

    /// <summary>Attiva la barra del titolo scura di Windows 11 (DWM), coerente con il tema
    /// dell'app, senza dover reimplementare la finestra con un chrome personalizzato.</summary>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var useDark = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }
        catch
        {
            // Su versioni di Windows che non supportano l'attributo, la finestra resta col tema di default.
        }
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
