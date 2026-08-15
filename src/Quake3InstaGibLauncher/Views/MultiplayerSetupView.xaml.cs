using System.Windows;
using System.Windows.Controls;
using Quake3InstaGibLauncher.ViewModels;

namespace Quake3InstaGibLauncher.Views;

public partial class MultiplayerSetupView : UserControl
{
    public MultiplayerSetupView() => InitializeComponent();

    // PasswordBox non supporta il binding diretto della proprieta' Password per motivi di
    // sicurezza (evita che rimanga in memoria in un binding): la sincronizziamo manualmente.
    // La password non viene mai scritta su disco ne' loggata da questa applicazione.
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MultiplayerSetupViewModel vm && sender is PasswordBox box)
            vm.Password = box.Password;
    }

    private void OnJoinPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MultiplayerSetupViewModel vm && sender is PasswordBox box)
            vm.JoinPassword = box.Password;
    }
}
