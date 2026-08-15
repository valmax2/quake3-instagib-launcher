using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Quake3InstaGibLauncher.Core.Services;

/// <summary>
/// Genera un messaggio di invito per la partita multiplayer ospitata e apre WhatsApp con il
/// testo gia' pronto (link "wa.me"), cosi' l'utente puo' scegliere il contatto e inviare con un
/// click, senza che l'app scriva o invii nulla per conto suo: l'utente resta sempre l'ultimo
/// a decidere se e a chi mandare il messaggio.
/// </summary>
public sealed class ServerInviteService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(4) };

    /// <summary>
    /// Prova a rilevare l'indirizzo IP pubblico interrogando un servizio pubblico di sola lettura
    /// (api.ipify.org: restituisce solo il tuo IP, nessun altro dato viene inviato). Se la rete
    /// non e' raggiungibile o il servizio non risponde, restituisce null senza sollevare eccezioni:
    /// il chiamante ricade allora sull'indirizzo di rete locale.
    /// </summary>
    public async Task<string?> TryDetectPublicIpAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await HttpClient.GetStringAsync("https://api.ipify.org", cancellationToken);
            var ip = response.Trim();
            return IPAddress.TryParse(ip, out _) ? ip : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Migliore indirizzo IPv4 locale disponibile, utile per inviti sulla stessa rete LAN.</summary>
    public static string? GetLocalLanAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530); // nessun pacchetto viene realmente inviato: serve solo a far scegliere al SO l'interfaccia locale
            return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString();
        }
        catch
        {
            return null;
        }
    }

    public static string BuildInviteMessage(string serverName, string address, int port, string mapName)
    {
        return $"🎮 Vieni a giocare InstaGib con me!\n" +
               $"Server: {serverName}\n" +
               $"Mappa: {mapName}\n" +
               $"Connettiti da Quake III / ioquake3 con:\nconnect {address}:{port}\n" +
               $"(serve ioquake3 con la mod InstaGib129 installata)";
    }

    /// <summary>Apre WhatsApp (app o web) con il messaggio gia' scritto nel campo di composizione:
    /// l'utente sceglie il contatto e decide se inviare. Non viene inviato nulla automaticamente.</summary>
    public static void OpenWhatsAppWithMessage(string message)
    {
        var encoded = Uri.EscapeDataString(message);
        var url = $"https://wa.me/?text={encoded}";
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}
