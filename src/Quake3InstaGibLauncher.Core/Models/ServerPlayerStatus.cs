namespace Quake3InstaGibLauncher.Core.Models;

/// <summary>Una riga della risposta "getstatus" di un server: nome (con eventuali codici colore)
/// e ping riportati dal server per un giocatore attualmente connesso.</summary>
/// <param name="Name">Nome del giocatore, cosi' come lo riporta il server (colori inclusi).</param>
/// <param name="Ping">Ping in ms riportato dal server. I bot rispondono sempre con 0: e' l'unico
/// modo, nel protocollo Quake III, per distinguerli da un giocatore umano senza accesso al
/// server stesso (vedi ServerInfo.IsBotOnly).</param>
public sealed record ServerPlayerStatus(string Name, int Ping)
{
    public bool IsLikelyBot => Ping <= 0;
}
