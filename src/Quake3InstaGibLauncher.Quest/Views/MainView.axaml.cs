using Avalonia;
using Avalonia.Controls;
using Quake3InstaGibLauncher.Quest.Services;
using Quake3InstaGibLauncher.Quest.ViewModels;

namespace Quake3InstaGibLauncher.Quest.Views;

public partial class MainView : UserControl
{
    // Passo di scorrimento per ogni "scatto" della levetta: una card/riga circa, non uno scroll
    // minuscolo che richiederebbe decine di movimenti per attraversare la lista.
    private const double ScrollStepPixels = 110;

    public MainView()
    {
        InitializeComponent();

        AttachedToVisualTree += (_, _) => ControllerScrollBridge.ScrollRequested += OnControllerScrollRequested;
        DetachedFromVisualTree += (_, _) => ControllerScrollBridge.ScrollRequested -= OnControllerScrollRequested;
    }

    /// <summary>Scorre lo ScrollViewer della scheda attualmente visibile, in risposta alla levetta
    /// del controller Quest (vedi MainActivity.OnGenericMotionEvent/OnKeyDown). L'evento arriva dal
    /// thread di input Android: Dispatcher.UIThread non serve esplicitamente qui perche' Avalonia.Android
    /// gira gia' gli eventi Activity sul thread UI, ma il controllo e' innocuo da lasciare per sicurezza.</summary>
    private void OnControllerScrollRequested(int direction)
    {
        if (DataContext is not QuestMainViewModel vm) return;

        ScrollViewer? target = vm.ActiveTab switch
        {
            QuestTab.Multiplayer => InternetScrollViewer,
            QuestTab.Lan => LanScrollViewer,
            QuestTab.Bot => BotMapsScrollViewer,
            QuestTab.Host => HostScrollViewer,
            QuestTab.Profile => ProfileScrollViewer,
            QuestTab.Players => PlayersScrollViewer,
            _ => null,
        };

        if (target is null) return;

        var offset = target.Offset;
        target.Offset = new Vector(offset.X, Math.Max(0, offset.Y + direction * ScrollStepPixels));
    }
}
