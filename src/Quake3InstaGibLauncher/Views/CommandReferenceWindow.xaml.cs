using System.Collections.ObjectModel;
using System.Windows;
using Quake3InstaGibLauncher.ViewModels;

namespace Quake3InstaGibLauncher.Views;

/// <summary>
/// Finestra di sola consultazione: elenco di comandi console e frasi chat pronte all'uso, con un
/// pulsante "copia" per ciascuna riga. Serve da riferimento veloce per chi vuole creare un tasto
/// personalizzato nella sezione Tasti ma non ricorda la sintassi esatta del comando (es. "say " +
/// il messaggio, o "callvote " + il tipo di voto): si copia qui, si incolla nel campo "Comando".
/// Contenuto statico, nessuna dipendenza da AppSettings: percio' e' una semplice Window con
/// code-behind invece di passare per una ViewModel/DataContext esterno come il resto dell'app.
/// </summary>
public partial class CommandReferenceWindow : Window
{
    public ObservableCollection<CommandReferenceItem> ConsoleCommands { get; } = new();
    public ObservableCollection<CommandReferenceItem> ChatPhrases { get; } = new();

    public CommandReferenceWindow()
    {
        InitializeComponent();
        DataContext = this;

        foreach (var item in BuildConsoleCommands()) ConsoleCommands.Add(item);
        foreach (var item in BuildChatPhrases()) ChatPhrases.Add(item);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CommandReferenceItem item }) return;

        try
        {
            Clipboard.SetText(item.Command);
        }
        catch
        {
            // Appunti non disponibili (raro, es. bloccati da un'altra app): non deve mai
            // far crashare la finestra, l'utente puo' comunque selezionare il testo a mano.
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Comandi console reali di Quake III/ioquake3, oltre ai preset gia' attivi di
    /// default nella sezione Tasti: utili per chi vuole aggiungerne altri come tasto
    /// personalizzato senza dover cercare la sintassi online.</summary>
    private static List<CommandReferenceItem> BuildConsoleCommands() => new()
    {
        new("Segnala pronto (inizia la partita)", "ready"),
        new("Segnala non pronto", "notready"),
        new("Suicidio (sblocca se incastrato)", "kill"),
        new("Passa a squadra Rossa", "team red"),
        new("Passa a squadra Blu", "team blue"),
        new("Passa a spettatore", "team spectator"),
        new("Torna in gioco (da spettatore)", "team free"),
        new("Cambia campo visivo (FOV) a 90", "cg_fov 90"),
        new("Cambia campo visivo (FOV) a 120", "cg_fov 120"),
        new("Vista in terza persona (on/off)", "toggle cg_thirdperson"),
        new("Mostra crosshair pulsazione danno (on/off)", "toggle cg_crosshairPulse"),
        new("Registra demo della partita", "record demo1"),
        new("Ferma registrazione demo", "stoprecord"),
        new("Cambia arma: Gauntlet", "weapon 1"),
        new("Cambia arma: Railgun/InstaGib", "weapon 2"),
        new("Proponi voto: kick di un giocatore", "callvote kick NomeGiocatore"),
        new("Proponi voto: cambia modalita' FFA", "callvote g_gametype 0"),
        new("Proponi voto: cambia modalita' CTF", "callvote g_gametype 4"),
        new("Apri chat privata (a un giocatore)", "messagemode3"),
        new("Messaggio privato diretto", "tell NomeGiocatore Messaggio qui"),
        new("Disconnetti dal server", "disconnect"),
        new("Riconnetti all'ultimo server", "reconnect"),
    };

    /// <summary>Frasi pronte per say/say_team, oltre ai preset gia' attivi di default nella
    /// sezione Tasti: stesso schema "say " + messaggio, cosi' e' facile crearne altre copiando e
    /// modificando solo il testo dopo "say ".</summary>
    private static List<CommandReferenceItem> BuildChatPhrases() => new()
    {
        new("Ben giocato!", "say Ben giocato!"),
        new("Rivincita?", "say Rivincita?"),
        new("Sto arrivando!", "say_team Sto arrivando!"),
        new("Nemico avvistato!", "say_team Nemico avvistato!"),
        new("Ho la bandiera!", "say_team Ho la bandiera!"),
        new("Difendi la base!", "say_team Difendi la base!"),
        new("Un attimo, sono AFK", "say Un attimo, torno subito (AFK)"),
        new("Bella mira!", "say Bella mira!"),
        new("Peccato!", "say Peccato!"),
        new("Ci risentiamo!", "say Ci vediamo alla prossima partita!"),
    };
}
