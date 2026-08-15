using Avalonia.Controls;
using Quake3InstaGibLauncher.Mac.ViewModels;

namespace Quake3InstaGibLauncher.Mac.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.CopyRequested += OnCopyRequested;
        };

        Opened += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                if (vm.Settings.WindowWidth > 0) Width = vm.Settings.WindowWidth;
                if (vm.Settings.WindowHeight > 0) Height = vm.Settings.WindowHeight;
            }
        };

        Closing += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Settings.WindowWidth = Width;
                vm.Settings.WindowHeight = Height;
                vm.SaveSettingsQuiet();
            }
        };
    }

    private async void OnCopyRequested(string text)
    {
        var clipboard = Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }
}
