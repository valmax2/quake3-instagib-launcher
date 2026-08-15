namespace Quake3InstaGibLauncher.Mac.Services;

/// <summary>
/// Stringhe dell'interfaccia in italiano/inglese, usate da LocalizationService. Definite in C#
/// (non in file .axaml separati) per semplicita': un Dictionary popola un ResourceDictionary a
/// runtime, evitando file di risorse XAML aggiuntivi da mantenere in sincronia con questo elenco.
/// </summary>
internal static class Strings
{
    public static readonly Dictionary<string, string> Italian = new()
    {
        // Nav
        ["Str.Nav.Home"] = "HOME",
        ["Str.Nav.Local"] = "LOCALE",
        ["Str.Nav.Multiplayer"] = "MULTIPLAYER",
        ["Str.Nav.Settings"] = "IMPOSTAZIONI",
        ["Str.Nav.Diagnostics"] = "DIAGNOSTICA",
        ["Str.Nav.Help"] = "GUIDA",
        ["Str.Nav.Customize"] = "PERSONALIZZA",
        ["Str.Nav.Players"] = "GIOCATORI",
        ["Str.Nav.KeyBindings"] = "TASTI",
        ["Str.Nav.PlayerProfile"] = "PERSONAGGIO",
        ["Str.Language.Tooltip"] = "Lingua / Language",

        // Home
        ["Str.Home.Subtitle"] = "INSTAGIB LAUNCHER · macOS",
        ["Str.Home.LocalButton"] = "⚔  LOCALE INSTAGIB",
        ["Str.Home.MultiplayerButton"] = "🌐  MULTIPLAYER INSTAGIB",
        ["Str.Home.HelpButton"] = "📥  GUIDA E DOWNLOAD",
        ["Str.Home.SettingsButton"] = "IMPOSTAZIONI",
        ["Str.Home.DiagnosticsButton"] = "DIAGNOSTICA",
        ["Str.Home.CustomizeButton"] = "PERSONALIZZA",

        ["Str.Common.PreviewLabel"] = "Anteprima:",

        // Giocatori
        ["Str.Players.Title"] = "GIOCATORI",
        ["Str.Players.Intro"] = "Giocatori incontrati in partita, salvati manualmente o dal browser server multiplayer. La valutazione ti aiuta a riconoscerli se li ritrovi in un server.",
        ["Str.Players.AddHeader"] = "AGGIUNGI MANUALMENTE",
        ["Str.Players.NameLabel"] = "Nome",
        ["Str.Players.RatingLabel"] = "Valutazione",
        ["Str.Players.Add"] = "Aggiungi",
        ["Str.Players.ListFormat"] = "GIOCATORI CONOSCIUTI ({0})",
        ["Str.Players.EmptyHint"] = "Nessun giocatore salvato ancora. Aggiungine uno qui sopra, oppure salvane uno direttamente dal browser server in Multiplayer.",
        ["Str.Players.NotesLabel"] = "Note:",
        ["Str.Players.Remove"] = "Rimuovi",

        // Tasti
        ["Str.KeyBindings.Title"] = "TASTI E COMANDI",
        ["Str.KeyBindings.Intro"] = "Comandi gia' preimpostati (spunta quelli che vuoi usare, cambia il tasto se vuoi) piu' la possibilita' di aggiungerne di tuoi. Applicati automaticamente ad ogni avvio, sia in Locale sia in Multiplayer.",
        ["Str.KeyBindings.ColActive"] = "Attivo",
        ["Str.KeyBindings.ColCommand"] = "Comando",
        ["Str.KeyBindings.ColKey"] = "Tasto",
        ["Str.KeyBindings.ResetPresets"] = "Ripristina tasti predefiniti",
        ["Str.KeyBindings.AddCustomHeader"] = "AGGIUNGI COMANDO PERSONALIZZATO",
        ["Str.KeyBindings.AddCustomInfo"] = "Qualsiasi comando della console Quake III (es. +attack, vstr taunt1, cg_thirdperson 1).",
        ["Str.KeyBindings.DescriptionOptional"] = "Descrizione (facoltativa)",
        ["Str.KeyBindings.Add"] = "Aggiungi",

        // Personaggio
        ["Str.PlayerProfile.Title"] = "PERSONAGGIO",
        ["Str.PlayerProfile.Intro"] = "Nome, mirino e colori applicati automaticamente ad ogni avvio (Locale, Multiplayer e Unisciti a un server).",
        ["Str.PlayerProfile.NameHeader"] = "NOME (GAMERTAG)",
        ["Str.PlayerProfile.NameInfo"] = "Posiziona il cursore dove vuoi e clicca un colore della palette per colorare da li' in poi (puoi mischiarne quanti vuoi nello stesso nome, come in Quake III).",
        ["Str.PlayerProfile.PaletteHeader"] = "Palette colori (clic per inserire alla posizione del cursore):",
        ["Str.PlayerProfile.ResetName"] = "Ripristina nome predefinito",
        ["Str.PlayerProfile.CrosshairHeader"] = "MIRINO",
        ["Str.PlayerProfile.CrosshairStyle"] = "Forma (1-15)",
        ["Str.PlayerProfile.CrosshairColor"] = "Colore (#RRGGBB, richiede ioquake3)",
        ["Str.PlayerProfile.CrosshairSizeLabel"] = "Dimensione:",
        ["Str.PlayerProfile.CrosshairHealthColor"] = "Cambia colore in base alla salute",
        ["Str.PlayerProfile.CrosshairPulse"] = "Pulsa quando colpisci un avversario",
        ["Str.PlayerProfile.ModelColorsHeader"] = "COLORI MODELLO",
        ["Str.PlayerProfile.PrimaryColor"] = "Colore primario",
        ["Str.PlayerProfile.SecondaryColor"] = "Colore secondario",
    };

    public static readonly Dictionary<string, string> English = new()
    {
        // Nav
        ["Str.Nav.Home"] = "HOME",
        ["Str.Nav.Local"] = "LOCAL",
        ["Str.Nav.Multiplayer"] = "MULTIPLAYER",
        ["Str.Nav.Settings"] = "SETTINGS",
        ["Str.Nav.Diagnostics"] = "DIAGNOSTICS",
        ["Str.Nav.Help"] = "HELP",
        ["Str.Nav.Customize"] = "CUSTOMIZE",
        ["Str.Nav.Players"] = "PLAYERS",
        ["Str.Nav.KeyBindings"] = "KEYS",
        ["Str.Nav.PlayerProfile"] = "CHARACTER",
        ["Str.Language.Tooltip"] = "Lingua / Language",

        // Home
        ["Str.Home.Subtitle"] = "INSTAGIB LAUNCHER · macOS",
        ["Str.Home.LocalButton"] = "⚔  LOCAL INSTAGIB",
        ["Str.Home.MultiplayerButton"] = "🌐  MULTIPLAYER INSTAGIB",
        ["Str.Home.HelpButton"] = "📥  HELP & DOWNLOAD",
        ["Str.Home.SettingsButton"] = "SETTINGS",
        ["Str.Home.DiagnosticsButton"] = "DIAGNOSTICS",
        ["Str.Home.CustomizeButton"] = "CUSTOMIZE",

        ["Str.Common.PreviewLabel"] = "Preview:",

        // Players
        ["Str.Players.Title"] = "PLAYERS",
        ["Str.Players.Intro"] = "Players you've met in matches, saved manually or from the multiplayer server browser. Your rating helps you recognize them if you run into them again.",
        ["Str.Players.AddHeader"] = "ADD MANUALLY",
        ["Str.Players.NameLabel"] = "Name",
        ["Str.Players.RatingLabel"] = "Rating",
        ["Str.Players.Add"] = "Add",
        ["Str.Players.ListFormat"] = "KNOWN PLAYERS ({0})",
        ["Str.Players.EmptyHint"] = "No player saved yet. Add one above, or save one directly from the server browser in Multiplayer.",
        ["Str.Players.NotesLabel"] = "Notes:",
        ["Str.Players.Remove"] = "Remove",

        // Keys
        ["Str.KeyBindings.Title"] = "KEYS AND COMMANDS",
        ["Str.KeyBindings.Intro"] = "Commands already preset (check the ones you want to use, change the key if you like) plus the option to add your own. Applied automatically on every launch, both Local and Multiplayer.",
        ["Str.KeyBindings.ColActive"] = "Active",
        ["Str.KeyBindings.ColCommand"] = "Command",
        ["Str.KeyBindings.ColKey"] = "Key",
        ["Str.KeyBindings.ResetPresets"] = "Reset default keys",
        ["Str.KeyBindings.AddCustomHeader"] = "ADD CUSTOM COMMAND",
        ["Str.KeyBindings.AddCustomInfo"] = "Any Quake III console command (e.g. +attack, vstr taunt1, cg_thirdperson 1).",
        ["Str.KeyBindings.DescriptionOptional"] = "Description (optional)",
        ["Str.KeyBindings.Add"] = "Add",

        // Character
        ["Str.PlayerProfile.Title"] = "CHARACTER",
        ["Str.PlayerProfile.Intro"] = "Name, crosshair and colors applied automatically on every launch (Local, Multiplayer and Join a server).",
        ["Str.PlayerProfile.NameHeader"] = "NAME (GAMERTAG)",
        ["Str.PlayerProfile.NameInfo"] = "Place the cursor where you want and click a palette color to color from there on (you can mix as many as you like in the same name, just like in Quake III).",
        ["Str.PlayerProfile.PaletteHeader"] = "Color palette (click to insert at the cursor position):",
        ["Str.PlayerProfile.ResetName"] = "Reset default name",
        ["Str.PlayerProfile.CrosshairHeader"] = "CROSSHAIR",
        ["Str.PlayerProfile.CrosshairStyle"] = "Shape (1-15)",
        ["Str.PlayerProfile.CrosshairColor"] = "Color (#RRGGBB, requires ioquake3)",
        ["Str.PlayerProfile.CrosshairSizeLabel"] = "Size:",
        ["Str.PlayerProfile.CrosshairHealthColor"] = "Change color based on health",
        ["Str.PlayerProfile.CrosshairPulse"] = "Pulse when you hit an opponent",
        ["Str.PlayerProfile.ModelColorsHeader"] = "MODEL COLORS",
        ["Str.PlayerProfile.PrimaryColor"] = "Primary color",
        ["Str.PlayerProfile.SecondaryColor"] = "Secondary color",
    };
}
