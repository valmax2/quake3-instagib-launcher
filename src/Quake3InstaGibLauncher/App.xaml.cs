using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Services;
using Quake3InstaGibLauncher.ViewModels;

namespace Quake3InstaGibLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        AppPaths.EnsureDirectoriesExist();

        // Suoni UI (passaggio mouse/clic) su OGNI pulsante dell'app, senza dover modificare
        // ciascuna vista: un gestore di classe intercetta l'evento per tutti i Button esistenti
        // e futuri. UiSoundService.Enabled viene sincronizzato da ThemeService.Apply.
        EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent, new RoutedEventHandler((_, _) => UiSoundService.PlayClick()));
        EventManager.RegisterClassHandler(typeof(Button), UIElement.MouseEnterEvent, new MouseEventHandler((_, _) => UiSoundService.PlayHover()));

        // Composizione manuale dei servizi (nessun contenitore DI necessario per un'app di
        // queste dimensioni: mantiene la build semplice e senza dipendenze aggiuntive).
        var settingsService = new SettingsService();
        var installationService = new GameInstallationService();
        var mapCacheService = new MapCacheService(LevelshotImageService.ConvertToPngBytes);
        var launchService = new LaunchService();
        var rotationCfgService = new RotationCfgService();

        var mainViewModel = new MainViewModel(
            settingsService,
            installationService,
            mapCacheService,
            launchService,
            rotationCfgService);

        var mainWindow = new Views.MainWindow { DataContext = mainViewModel };
        MainWindow = mainWindow;
        mainWindow.Show();

        _ = mainViewModel.InitializeAsync();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        MessageBox.Show(
            $"Si e' verificato un errore imprevisto:\n\n{e.Exception.Message}\n\nDettagli salvati in:\n{AppPaths.LogsDir}",
            "Quake III InstaGib Launcher - Errore",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogCrash(ex);
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            AppPaths.EnsureDirectoriesExist();
            var logFile = Path.Combine(AppPaths.LogsDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            // Il log contiene solo lo stack trace dell'eccezione: nessuna CD key, password
            // o altro dato sensibile viene mai scritto su disco da questa applicazione.
            File.WriteAllText(logFile, ex.ToString());
        }
        catch
        {
            // Se anche la scrittura del log fallisce non c'e' altro da fare: evitiamo un secondo crash.
        }
    }
}
