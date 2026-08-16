using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Quake3InstaGibLauncher.Quest.ViewModels;
using Quake3InstaGibLauncher.Quest.Views;

namespace Quake3InstaGibLauncher.Quest;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Il Quest e' un dispositivo "single view" (nessuna finestra classica desktop): l'unica
        // superficie disponibile e' la Activity Android stessa, esposta da Avalonia come
        // ISingleViewApplicationLifetime.
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new QuestMainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
