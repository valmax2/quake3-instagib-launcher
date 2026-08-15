using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Quake3InstaGibLauncher.Core.Services;
using Quake3InstaGibLauncher.Mac.Services;
using Quake3InstaGibLauncher.Mac.ViewModels;
using Quake3InstaGibLauncher.Mac.Views;

namespace Quake3InstaGibLauncher.Mac;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppPaths.EnsureDirectoriesExist();

            var settingsService = new MacSettingsService();
            var installationLocator = new MacInstallationLocator();
            var mapCacheService = new MapCacheService(MacImageConversionService.ConvertToPngBytes);
            var launchService = new LaunchService();
            var rotationCfgService = new RotationCfgService();

            var mainWindow = new MainWindow();

            var mainViewModel = new MainViewModel(
                settingsService,
                installationLocator,
                mapCacheService,
                launchService,
                rotationCfgService,
                pickExecutableFile: () => PickExecutableFileAsync(mainWindow),
                pickDataFolder: () => PickFolderAsync(mainWindow),
                pickImageFile: () => PickImageFileAsync(mainWindow),
                openFolder: OpenFolderInFinder,
                applyTheme: (theme, window) => ThemeService.Apply(theme, window ?? mainWindow),
                detectScreenResolution: () => DetectScreenResolution(mainWindow));

            mainWindow.DataContext = mainViewModel;
            desktop.MainWindow = mainWindow;

            // Suoni sci-fi sintetici al passaggio del mouse e al clic sui pulsanti (nessun
            // contenuto originale di Quake III, vedi UiSoundService): un unico handler globale
            // sulla finestra invece che uno per ogni Button, stesso approccio (class handler) di
            // App.xaml.cs lato Windows. RoutingStrategies.Tunnel cattura l'evento anche per
            // ClickEvent/PointerEnteredEvent, che in Avalonia non risalgono l'albero da soli.
            mainWindow.AddHandler(Button.ClickEvent, (_, _) => UiSoundService.PlayClick(),
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            mainWindow.AddHandler(InputElement.PointerEnteredEvent, (_, e) =>
            {
                if (e.Source is Button) UiSoundService.PlayHover();
            }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);

            desktop.Startup += async (_, _) => await mainViewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task<string?> PickExecutableFileAsync(MainWindow window)
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Seleziona l'eseguibile ioquake3 (dentro Contents/MacOS/ del bundle .app)",
            AllowMultiple = false,
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private static async Task<string?> PickFolderAsync(MainWindow window)
    {
        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Seleziona la cartella dati (contiene baseq3, InstaGib129, missionpack)",
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    /// <summary>Selezione di un'immagine (sfondo finestra/pulsanti/Home) per Personalizza
    /// interfaccia. I GIF sono ammessi ma mostrati come immagine statica (Avalonia non anima i
    /// GIF nativamente).</summary>
    private static async Task<string?> PickImageFileAsync(MainWindow window)
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Scegli un'immagine",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Immagini") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" } },
            },
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    /// <summary>Risoluzione reale (pixel fisici) dello schermo su cui si trova la finestra,
    /// tramite l'API Screens di Avalonia (cross-platform: niente P/Invoke specifico per
    /// piattaforma, a differenza della versione Windows che usa GetSystemMetrics).</summary>
    private static (int Width, int Height)? DetectScreenResolution(MainWindow window)
    {
        var screen = window.Screens?.ScreenFromWindow(window) ?? window.Screens?.Primary;
        if (screen is null) return null;

        var bounds = screen.Bounds;
        return (bounds.Width, bounds.Height);
    }

    /// <summary>Apre una cartella nel Finder (macOS) usando il comando "open", equivalente
    /// dell'apertura di Esplora Risorse su Windows.</summary>
    private static void OpenFolderInFinder(string path)
    {
        if (!Directory.Exists(path)) return;

        try
        {
            if (OperatingSystem.IsMacOS())
                Process.Start(new ProcessStartInfo { FileName = "open", ArgumentList = { path }, UseShellExecute = false });
            else
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch
        {
            // Apertura Finder non critica: se fallisce l'utente puo' comunque navigare manualmente.
        }
    }
}
