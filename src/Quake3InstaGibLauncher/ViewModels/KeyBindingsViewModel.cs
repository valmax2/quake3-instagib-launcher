using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quake3InstaGibLauncher.Core.Models;
using Quake3InstaGibLauncher.Models;

namespace Quake3InstaGibLauncher.ViewModels;

/// <summary>
/// Schermata "Tasti": elenco di comandi Quake III gia' preimpostati, raggruppati in due sezioni —
/// COMANDI CONSOLE (movimento, azioni, voti, cambio arma...) e MESSAGGI CHAT (say/say_team rapidi,
/// es. "Buona partita!") — ciascuno associabile a un tasto a piacere, piu' la possibilita' di
/// aggiungere comandi/messaggi personalizzati in entrambe le categorie. Applicati al lancio del
/// gioco tramite un file .cfg di "bind" generato al volo (vedi KeyBindingCfgService), esattamente
/// come la rotazione mappe.
///
/// NOTA DI DESIGN: l'idea originale era una finestra "overlay" che appare sopra al gioco mentre si
/// gioca, con due tasti per scegliere tra comandi console e bind chat. Una vera sovrapposizione
/// grafica al rendering del motore e' una categoria tecnica a parte (fragile, e su alcuni server
/// puo' assomigliare a un cheat anche se non lo e'). Il risultato pratico voluto — premere un
/// tasto e vedere partire subito un comando o un messaggio in chat — si ottiene in modo identico e
/// molto piu' robusto con un bind Quake III vero (che e' esattamente cosa fa questa schermata):
/// nessuna finestra extra, nessun aggancio al motore, funziona sempre.
/// </summary>
public partial class KeyBindingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly Action _saveSettings;

    /// <summary>Elenco completo (entrambe le categorie): usato per costruire il file .cfg al lancio.</summary>
    public ObservableCollection<KeyBindingViewModel> Bindings { get; } = new();

    /// <summary>Vista filtrata di <see cref="Bindings"/> per la sezione "Comandi console" della UI.</summary>
    public ObservableCollection<KeyBindingViewModel> ConsoleBindings { get; } = new();

    /// <summary>Vista filtrata di <see cref="Bindings"/> per la sezione "Messaggi chat" della UI.</summary>
    public ObservableCollection<KeyBindingViewModel> ChatBindings { get; } = new();

    [ObservableProperty] private string _newCommand = string.Empty;
    [ObservableProperty] private string _newKey = string.Empty;
    [ObservableProperty] private string _newDescription = string.Empty;
    [ObservableProperty] private KeyBindingCategory _newCategory = KeyBindingCategory.Console;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public IReadOnlyList<KeyBindingCategory> AvailableCategories { get; } = Enum.GetValues<KeyBindingCategory>();

    public KeyBindingsViewModel(AppSettings settings, Action saveSettings)
    {
        _settings = settings;
        _saveSettings = saveSettings;

        if (_settings.KeyBindings is null || _settings.KeyBindings.Count == 0)
            _settings.KeyBindings = BuildDefaultPresets();

        foreach (var entry in _settings.KeyBindings)
            AddToCollections(new KeyBindingViewModel(entry, Persist));
    }

    /// <summary>Elenco dei bind attivi, pronto per KeyBindingCfgService: solo quelli con
    /// Enabled=true finiscono davvero nel file .cfg generato al lancio.</summary>
    public IReadOnlyList<KeyBindingEntry> ActiveBindings => Bindings.Select(b => b.Model).ToList();

    private void AddToCollections(KeyBindingViewModel vm)
    {
        Bindings.Add(vm);
        (vm.Model.Category == KeyBindingCategory.Chat ? ChatBindings : ConsoleBindings).Add(vm);
    }

    private void RemoveFromCollections(KeyBindingViewModel vm)
    {
        Bindings.Remove(vm);
        ConsoleBindings.Remove(vm);
        ChatBindings.Remove(vm);
    }

    [RelayCommand]
    private void AddCustomBinding()
    {
        var key = NewKey.Trim();
        var command = NewCommand.Trim();
        var description = string.IsNullOrWhiteSpace(NewDescription) ? command : NewDescription.Trim();

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(command))
        {
            StatusMessage = "Indica sia il tasto sia il comando.";
            return;
        }

        var entry = new KeyBindingEntry { Description = description, Command = command, Key = key, Enabled = true, IsBuiltIn = false, Category = NewCategory };
        _settings.KeyBindings!.Add(entry);
        AddToCollections(new KeyBindingViewModel(entry, Persist));

        NewCommand = string.Empty;
        NewKey = string.Empty;
        NewDescription = string.Empty;
        Persist();
        StatusMessage = $"\"{description}\" aggiunto sul tasto {key.ToUpperInvariant()}.";
    }

    [RelayCommand]
    private void RemoveBinding(KeyBindingViewModel? binding)
    {
        if (binding is null) return;
        _settings.KeyBindings!.Remove(binding.Model);
        RemoveFromCollections(binding);
        Persist();
        StatusMessage = $"\"{binding.Description}\" rimosso.";
    }

    /// <summary>Ripristina SOLO questo bind predefinito al suo valore originale (tasto, comando,
    /// attivo/no), lasciando invariati tutti gli altri: utile se ne modifichi uno per sbaglio senza
    /// voler perdere le personalizzazioni fatte altrove. Disponibile solo per i preset dell'app
    /// (IsBuiltIn=true): un comando aggiunto a mano non ha un "originale" a cui tornare, per quello
    /// c'e' il pulsante "elimina".</summary>
    [RelayCommand]
    private void ResetSingleBinding(KeyBindingViewModel? binding)
    {
        if (binding is null || !binding.IsBuiltIn) return;

        var original = BuildDefaultPresets().FirstOrDefault(d => d.Description == binding.Description);
        if (original is null) return;

        binding.Key = original.Key;
        binding.Command = original.Command;
        binding.Enabled = original.Enabled;
        StatusMessage = $"\"{binding.Description}\" ripristinato al valore predefinito.";
    }

    [RelayCommand]
    private void ResetPresets()
    {
        _settings.KeyBindings = BuildDefaultPresets();
        Bindings.Clear();
        ConsoleBindings.Clear();
        ChatBindings.Clear();
        foreach (var entry in _settings.KeyBindings)
            AddToCollections(new KeyBindingViewModel(entry, Persist));
        Persist();
        StatusMessage = "Tasti predefiniti ripristinati.";
    }

    /// <summary>Apre la finestra di sola consultazione con comandi/frasi extra copiabili (vedi
    /// CommandReferenceWindow): utile per chi vuole aggiungere un tasto personalizzato ma non
    /// ricorda la sintassi esatta del comando Quake III da scrivere nel campo qui sopra.</summary>
    [RelayCommand]
    private void ShowCommandReference()
    {
        var window = new Views.CommandReferenceWindow
        {
            Owner = System.Windows.Application.Current?.MainWindow,
        };
        window.ShowDialog();
    }

    private void Persist()
    {
        _settings.KeyBindings = Bindings.Select(b => b.Model).ToList();
        _saveSettings();
    }

    private static List<KeyBindingEntry> BuildDefaultPresets() => new()
    {
        // ===== Comandi console =====
        new KeyBindingEntry { Description = "Salto", Command = "+moveup", Key = "SPACE", Enabled = true, IsBuiltIn = true, Category = KeyBindingCategory.Console },
        new KeyBindingEntry { Description = "Accovacciati", Command = "+movedown", Key = "C", Enabled = true, IsBuiltIn = true, Category = KeyBindingCategory.Console },
        new KeyBindingEntry { Description = "Corri (tieni premuto)", Command = "+speed", Key = "SHIFT", Enabled = true, IsBuiltIn = true, Category = KeyBindingCategory.Console },
        new KeyBindingEntry { Description = "Usa/Azione", Command = "+button2", Key = "E", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Console },
        new KeyBindingEntry { Description = "Tabella punteggi", Command = "+scores", Key = "TAB", Enabled = true, IsBuiltIn = true, Category = KeyBindingCategory.Console },
        new KeyBindingEntry { Description = "Cattura schermata", Command = "screenshot", Key = "F12", Enabled = true, IsBuiltIn = true, Category = KeyBindingCategory.Console },
        new KeyBindingEntry { Description = "Arma: Gauntlet (mischia)", Command = "weapon 1", Key = "1", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Console },
        new KeyBindingEntry { Description = "Arma: Railgun/InstaGib", Command = "weapon 2", Key = "2", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Console },
        new KeyBindingEntry { Description = "Vota SI", Command = "vote yes", Key = "F1", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Console },
        new KeyBindingEntry { Description = "Vota NO", Command = "vote no", Key = "F2", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Console },
        new KeyBindingEntry { Description = "Proponi voto: prossima mappa", Command = "callvote nextmap", Key = "F3", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Console },
        new KeyBindingEntry { Description = "Proponi voto: ricomincia mappa", Command = "callvote map_restart", Key = "F4", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Console },

        // ===== Messaggi chat rapidi (say/say_team) =====
        new KeyBindingEntry { Description = "Chat con tutti (apri)", Command = "messagemode", Key = "T", Enabled = true, IsBuiltIn = true, Category = KeyBindingCategory.Chat },
        new KeyBindingEntry { Description = "Chat con la squadra (apri)", Command = "messagemode2", Key = "Y", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Chat },
        new KeyBindingEntry { Description = "Ciao a tutti!", Command = "say Ciao a tutti!", Key = "F5", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Chat },
        new KeyBindingEntry { Description = "Buona partita!", Command = "say Buona partita a tutti!", Key = "F6", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Chat },
        new KeyBindingEntry { Description = "Che vinca il migliore!", Command = "say Che vinca il migliore!", Key = "F7", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Chat },
        new KeyBindingEntry { Description = "Complimenti per la partita!", Command = "say Complimenti per la partita, gg!", Key = "F8", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Chat },
        new KeyBindingEntry { Description = "Grazie!", Command = "say Grazie!", Key = "F9", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Chat },
        new KeyBindingEntry { Description = "Scusa!", Command = "say Scusa!", Key = "F10", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Chat },
        new KeyBindingEntry { Description = "Copro qui! (squadra)", Command = "say_team Copro qui!", Key = "F11", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Chat },
        new KeyBindingEntry { Description = "Serve aiuto! (squadra)", Command = "say_team Serve aiuto!", Key = "K", Enabled = false, IsBuiltIn = true, Category = KeyBindingCategory.Chat },
    };
}
