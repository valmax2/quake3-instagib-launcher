using System.Net;
using System.Net.Sockets;
using System.Text;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Core.Services;

/// <summary>
/// Client del protocollo di rete "fuori banda" (out-of-band) di Quake III, lo stesso usato dal
/// browser server integrato nel gioco: pacchetti UDP che iniziano con 4 byte 0xFF seguiti da un
/// comando testuale ("getinfo", "getservers", ...). Non e' un exploit ne' un protocollo privato:
/// e' documentato pubblicamente ed e' esattamente cio' che fa il menu Multiplayer di ioquake3.
///
/// - Ricerca LAN: broadcast UDP "getinfo" sulla subnet locale, sulla porta indicata.
/// - Ricerca Internet: interroga un master server ("getservers"), poi interroga ogni server
///   trovato con "getinfo" per i dettagli. Questa parte dipende dalla raggiungibilita' di rete
///   del master server e non e' stata verificata in questo ambiente di sviluppo: se il master
///   non risponde entro il timeout, la ricerca restituisce semplicemente una lista vuota.
/// </summary>
public sealed class Quake3ServerBrowser
{
    private const int OobHeaderLength = 4;
    private static readonly byte[] OobHeader = { 0xFF, 0xFF, 0xFF, 0xFF };

    /// <summary>Cerca server sulla rete locale inviando un broadcast UDP "getinfo" sulla porta indicata.</summary>
    public async Task<IReadOnlyList<ServerInfo>> SearchLanAsync(
        int port = 27960,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromSeconds(2);
        var results = new Dictionary<string, ServerInfo>();

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.EnableBroadcast = true;
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));

        var challenge = Guid.NewGuid().ToString("N")[..8];
        var request = BuildOobPacket($"getinfo {challenge}");
        var broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, port);

        var sendTimestamp = DateTime.UtcNow;
        try
        {
            await socket.SendToAsync(request, SocketFlags.None, broadcastEndPoint, cancellationToken);
        }
        catch (SocketException)
        {
            return Array.Empty<ServerInfo>(); // rete non disponibile: nessun risultato, nessun crash
        }

        var deadline = DateTime.UtcNow + timeout.Value;
        var buffer = new byte[4096];

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) break;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(remaining);

            try
            {
                var receiveTask = socket.ReceiveFromAsync(
                    new ArraySegment<byte>(buffer), SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));

                var result = await receiveTask.WaitAsync(cts.Token);
                var from = (IPEndPoint)result.RemoteEndPoint;
                var info = TryParseInfoResponse(buffer.AsSpan(0, result.ReceivedBytes), from.Address.ToString(), from.Port);
                if (info is not null)
                {
                    info.IsLan = true;
                    info.PingMs = (long)(DateTime.UtcNow - sendTimestamp).TotalMilliseconds;
                    results[info.EndPointText] = info;
                }
            }
            catch (OperationCanceledException)
            {
                break; // timeout complessivo raggiunto
            }
            catch (SocketException)
            {
                break;
            }
        }

        return results.Values.ToList();
    }

    /// <summary>
    /// Interroga un master server pubblico per ottenere l'elenco dei server registrati, poi
    /// chiede i dettagli ("getinfo") a ciascuno. Best-effort: se il master non e' raggiungibile
    /// entro il timeout, restituisce una lista vuota invece di sollevare un'eccezione.
    /// </summary>
    public async Task<IReadOnlyList<ServerInfo>> SearchInternetAsync(
        string masterHost = "master.ioquake3.org",
        int masterPort = 27950,
        int protocolVersion = 68,
        TimeSpan? masterTimeout = null,
        TimeSpan? detailsTimeout = null,
        CancellationToken cancellationToken = default)
    {
        masterTimeout ??= TimeSpan.FromSeconds(3);
        detailsTimeout ??= TimeSpan.FromSeconds(3);

        List<IPEndPoint> serverEndPoints;
        try
        {
            serverEndPoints = await QueryMasterServerAsync(masterHost, masterPort, protocolVersion, masterTimeout.Value, cancellationToken);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested is false)
        {
            return Array.Empty<ServerInfo>();
        }

        if (serverEndPoints.Count == 0)
            return Array.Empty<ServerInfo>();

        var results = new Dictionary<string, ServerInfo>();
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));

        var challenge = Guid.NewGuid().ToString("N")[..8];
        var request = BuildOobPacket($"getinfo {challenge}");

        var sendTimestamp = DateTime.UtcNow;
        foreach (var endPoint in serverEndPoints)
        {
            try { await socket.SendToAsync(request, SocketFlags.None, endPoint, cancellationToken); }
            catch (SocketException) { /* singolo server irraggiungibile: si prosegue con gli altri */ }
        }

        // Il master server pubblico puo' restituire facilmente qualche centinaio di indirizzi: con
        // una finestra di ascolto fissa (es. 3-6 secondi) indipendente dal numero di server
        // interrogati, su reti piu' lente (es. Wi-Fi di un visore VR rispetto al cavo di un PC) le
        // risposte "getinfo" arrivate per ultime rischiano di non fare in tempo, facendo sembrare
        // la ricerca "filtrata" quando in realta' e' solo scaduta troppo presto. Allunghiamo quindi
        // la finestra in proporzione al numero di server trovati (limite di sicurezza a 12s per non
        // far restare l'utente in attesa all'infinito con un master particolarmente affollato).
        var scaledDetailsTimeout = TimeSpan.FromMilliseconds(
            Math.Min(12_000, Math.Max(detailsTimeout.Value.TotalMilliseconds, serverEndPoints.Count * 15)));

        var deadline = DateTime.UtcNow + scaledDetailsTimeout;
        var buffer = new byte[4096];

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) break;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(remaining);

            try
            {
                var receiveTask = socket.ReceiveFromAsync(
                    new ArraySegment<byte>(buffer), SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));
                var result = await receiveTask.WaitAsync(cts.Token);
                var from = (IPEndPoint)result.RemoteEndPoint;
                var info = TryParseInfoResponse(buffer.AsSpan(0, result.ReceivedBytes), from.Address.ToString(), from.Port);
                if (info is not null)
                {
                    info.IsLan = false;
                    info.PingMs = (long)(DateTime.UtcNow - sendTimestamp).TotalMilliseconds;
                    results[info.EndPointText] = info;
                }
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { break; }
        }

        return results.Values.ToList();
    }

    /// <summary>
    /// Interroga un singolo server con "getstatus" per ottenere l'elenco dei giocatori attualmente
    /// in partita (nome, frag, ping). A differenza di "getinfo" (usato per la lista server) questa
    /// richiesta va fatta server per server: si usa solo quando serve davvero sapere chi sta
    /// giocando (es. per cercare un giocatore conosciuto, o distinguere bot da umani), non per
    /// ogni server della lista. Best-effort: se il server non risponde entro il timeout,
    /// restituisce una lista vuota.
    /// </summary>
    public async Task<IReadOnlyList<ServerPlayerStatus>> GetPlayerStatusAsync(
        string address,
        int port,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromSeconds(2);

        if (!IPAddress.TryParse(address, out var ip))
        {
            var addresses = await Dns.GetHostAddressesAsync(address, cancellationToken);
            ip = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
            if (ip is null) return Array.Empty<ServerPlayerStatus>();
        }

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));

        var challenge = Guid.NewGuid().ToString("N")[..8];
        var request = BuildOobPacket($"getstatus {challenge}");
        var endPoint = new IPEndPoint(ip, port);

        try { await socket.SendToAsync(request, SocketFlags.None, endPoint, cancellationToken); }
        catch (SocketException) { return Array.Empty<ServerPlayerStatus>(); }

        var deadline = DateTime.UtcNow + timeout.Value;
        var buffer = new byte[8192];

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) break;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(remaining);

            try
            {
                var receiveTask = socket.ReceiveFromAsync(
                    new ArraySegment<byte>(buffer), SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));
                var result = await receiveTask.WaitAsync(cts.Token);
                var from = (IPEndPoint)result.RemoteEndPoint;
                if (!from.Address.Equals(ip)) continue; // risposta di un altro server: ignorata

                return ParseStatusResponsePlayers(buffer.AsSpan(0, result.ReceivedBytes));
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { break; }
        }

        return Array.Empty<ServerPlayerStatus>();
    }

    /// <summary>
    /// Formato "statusResponse\n\chiave\valore...\n&lt;frag&gt; &lt;ping&gt; "&lt;nome&gt;"\n...":
    /// dopo la riga con i campi del server (stesso formato di infoResponse), una riga per
    /// giocatore con frag, ping e nome tra virgolette. Il ping e' il secondo numero della riga: i
    /// bot rispondono sempre con ping 0 (nessuna connessione di rete reale dietro), i giocatori
    /// umani hanno quasi sempre un ping maggiore di 0 (stessa euristica usata da qualunque browser
    /// server Quake III per distinguerli, protocollo alla mano non c'e' un flag "e' un bot").
    /// </summary>
    private static IReadOnlyList<ServerPlayerStatus> ParseStatusResponsePlayers(ReadOnlySpan<byte> packet)
    {
        const string marker = "statusResponse";
        var text = Encoding.ASCII.GetString(packet);
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) return Array.Empty<ServerPlayerStatus>();

        var lines = text[(markerIndex + marker.Length)..].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var players = new List<ServerPlayerStatus>();

        // La prima riga e' sempre la stringa \chiave\valore\... del server: la saltiamo.
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim('\r', ' ');
            var firstQuote = line.IndexOf('"');
            var lastQuote = line.LastIndexOf('"');
            if (firstQuote < 0 || lastQuote <= firstQuote) continue;

            var name = line[(firstQuote + 1)..lastQuote];
            if (string.IsNullOrWhiteSpace(name)) continue;

            // Prima del nome tra virgolette: "<frag> <ping> ". Il ping e' il secondo token.
            var beforeQuote = line[..firstQuote].Trim();
            var tokens = beforeQuote.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ping = tokens.Length >= 2 && int.TryParse(tokens[1], out var p) ? p : 0;

            players.Add(new ServerPlayerStatus(name, ping));
        }

        return players;
    }

    private static async Task<List<IPEndPoint>> QueryMasterServerAsync(
        string masterHost, int masterPort, int protocolVersion, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(masterHost, cancellationToken);
        var masterAddress = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault();
        if (masterAddress is null)
            return new List<IPEndPoint>();

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));

        var request = BuildOobPacket($"getservers {protocolVersion} full empty");
        await socket.SendToAsync(request, SocketFlags.None, new IPEndPoint(masterAddress, masterPort), cancellationToken);

        var endPoints = new List<IPEndPoint>();
        var deadline = DateTime.UtcNow + timeout;
        var buffer = new byte[16384];

        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) break;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(remaining);

            try
            {
                var receiveTask = socket.ReceiveFromAsync(
                    new ArraySegment<byte>(buffer), SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));
                var result = await receiveTask.WaitAsync(cts.Token);
                ParseGetServersResponse(buffer.AsSpan(0, result.ReceivedBytes), endPoints);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { break; }
        }

        return endPoints;
    }

    /// <summary>
    /// Formato "getserversResponse": dopo l'intestazione, una sequenza di record separatore '\'
    /// + 4 byte IP + 2 byte porta (big-endian), fino al marcatore finale "EOT".
    /// </summary>
    private static void ParseGetServersResponse(ReadOnlySpan<byte> packet, List<IPEndPoint> into)
    {
        const string marker = "getserversResponse";
        var text = Encoding.ASCII.GetString(packet);
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) return;

        var offset = markerIndex + marker.Length;

        while (offset + 7 <= packet.Length)
        {
            if (packet[offset] != (byte)'\\')
            {
                offset++;
                continue;
            }

            // Marcatore di fine lista: "\EOT"
            if (offset + 4 <= packet.Length &&
                packet[offset + 1] == (byte)'E' && packet[offset + 2] == (byte)'O' && packet[offset + 3] == (byte)'T')
            {
                break;
            }

            var ip = new IPAddress(packet.Slice(offset + 1, 4));
            var port = (packet[offset + 5] << 8) | packet[offset + 6];

            if (port > 0)
                into.Add(new IPEndPoint(ip, port));

            offset += 7;
        }
    }

    private static byte[] BuildOobPacket(string command)
    {
        var commandBytes = Encoding.ASCII.GetBytes(command);
        var packet = new byte[OobHeaderLength + commandBytes.Length];
        OobHeader.CopyTo(packet, 0);
        commandBytes.CopyTo(packet, OobHeaderLength);
        return packet;
    }

    /// <summary>
    /// Formato "infoResponse\n\chiave\valore\chiave\valore...": estrae i campi piu' utili per
    /// mostrare il server nella lista (nome, mappa, giocatori, modalita', mod, password).
    /// </summary>
    private static ServerInfo? TryParseInfoResponse(ReadOnlySpan<byte> packet, string address, int port)
    {
        const string marker = "infoResponse";
        var text = Encoding.ASCII.GetString(packet);
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) return null;

        var payloadStart = text.IndexOf('\\', markerIndex);
        if (payloadStart < 0) return null;

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tokens = text[payloadStart..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i + 1 < tokens.Length; i += 2)
            fields[tokens[i]] = tokens[i + 1];

        var info = new ServerInfo { Address = address, Port = port };

        if (fields.TryGetValue("hostname", out var hostname)) info.HostName = Quake3TextUtils.StripColorCodes(hostname);
        if (fields.TryGetValue("mapname", out var map)) info.MapName = map;
        if (fields.TryGetValue("clients", out var clients) && int.TryParse(clients, out var c)) info.Players = c;
        if (fields.TryGetValue("sv_maxclients", out var max) && int.TryParse(max, out var m)) info.MaxPlayers = m;
        if (fields.TryGetValue("gametype", out var gt)) info.GameTypeRaw = gt;
        if (fields.TryGetValue("game", out var game)) info.ModName = game;
        if (fields.TryGetValue("needpass", out var needPass)) info.NeedsPassword = needPass == "1";

        return info;
    }

}
