namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>Le due "vaschette" in cui la schermata Tasti raggruppa i bind: comandi della console
/// di gioco (movimento, azioni, voti...) oppure messaggi di chat rapidi (say/say_team).
/// Puramente organizzativo lato UI: al motore non interessa, "say ciao" e' un comando come
/// qualunque altro.</summary>
public enum KeyBindingCategory
{
    Console,
    Chat,
}

/// <summary>Un singolo "bind" Quake III: un tasto/pulsante del mouse associato a un comando della
/// console. Enabled=false lo esclude dal file .cfg generato senza doverlo eliminare dall'elenco.</summary>
public sealed class KeyBindingEntry
{
    public required string Description { get; set; }
    public required string Command { get; set; }
    public required string Key { get; set; }
    public bool Enabled { get; set; } = true;

    /// <summary>True per i preset forniti dall'app (comunque modificabili), false per quelli
    /// aggiunti manualmente dall'utente (mostrati con un pulsante "elimina" nella UI).</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>Default Console: le impostazioni salvate PRIMA di questo campo (senza la
    /// proprieta') tornano tutte "Console" alla deserializzazione, comportamento corretto dato
    /// che prima esistevano solo comandi console.</summary>
    public KeyBindingCategory Category { get; set; } = KeyBindingCategory.Console;
}
