using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Core.Services;

/// <summary>
/// Elenco dei bind preimpostati offerti dalla schermata "Tasti/Comandi" (comandi console + messaggi
/// chat rapidi), condiviso da tutte le versioni del launcher (Windows, macOS, Quest) cosi' che
/// preset e comportamento restino identici ovunque - estratto da KeyBindingsViewModel (Windows),
/// dove viveva come metodo privato duplicabile per ogni piattaforma.
/// </summary>
public static class KeyBindingDefaults
{
    public static List<KeyBindingEntry> BuildDefaultPresets() => new()
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
