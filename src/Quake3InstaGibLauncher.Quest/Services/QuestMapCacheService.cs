using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Quake3InstaGibLauncher.Core.Models;

namespace Quake3InstaGibLauncher.Quest.Services;

/// <summary>
/// Salva l'elenco mappe scansionato (con il percorso delle anteprime gia' decodificate) nella
/// cartella privata dell'app (Context.FilesDir), cosi' l'app non deve riaprire tutti i .pk3 -
/// alcuni pesano centinaia di MB - ad ogni singolo avvio. La cache si invalida da sola (fingerprint
/// basata su nome/dimensione/data di modifica di ogni .pk3 nelle cartelle scansionate): se l'utente
/// aggiunge o toglie un .pk3, la prossima volta lo si nota e si riscansiona in automatico, senza
/// bisogno di un tasto "cancella cache" manuale.
/// </summary>
public sealed class QuestMapCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private string CacheFilePath => Path.Combine(
        global::Android.App.Application.Context.FilesDir!.AbsolutePath, "maps_cache.json");

    private sealed class CacheFile
    {
        public string Fingerprint { get; set; } = "";
        public List<MapInfo> Maps { get; set; } = new();
    }

    /// <summary>Calcola un'impronta stabile delle cartelle scansionate (nome+dimensione+data di
    /// ogni .pk3): cambia solo se i file .pk3 presenti cambiano davvero, non ad ogni avvio.</summary>
    public static string ComputeFingerprint(IEnumerable<string> directoriesToScan)
    {
        var sb = new StringBuilder();
        foreach (var dir in directoriesToScan.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.GetFiles(dir, "*.pk3").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var info = new FileInfo(file);
                sb.Append(info.Name).Append(':').Append(info.Length).Append(':').Append(info.LastWriteTimeUtc.Ticks).Append(';');
            }
        }

        var hash = MD5.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return System.Convert.ToHexString(hash);
    }

    /// <summary>Restituisce le mappe in cache SOLO se l'impronta corrisponde ancora (nessun .pk3
    /// aggiunto/tolto/modificato dall'ultima scansione); altrimenti null, per far scattare una
    /// scansione completa nel chiamante.</summary>
    public List<MapInfo>? TryLoad(string currentFingerprint)
    {
        try
        {
            if (!File.Exists(CacheFilePath)) return null;

            var cached = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(CacheFilePath), JsonOptions);
            if (cached is null || cached.Fingerprint != currentFingerprint) return null;

            return cached.Maps;
        }
        catch
        {
            return null; // cache corrotta/illeggibile: si riscansiona, non si blocca l'app
        }
    }

    public void Save(string fingerprint, IReadOnlyList<MapInfo> maps)
    {
        try
        {
            var cache = new CacheFile { Fingerprint = fingerprint, Maps = maps.ToList() };
            File.WriteAllText(CacheFilePath, JsonSerializer.Serialize(cache, JsonOptions));
        }
        catch
        {
            // storage pieno o non scrivibile: la prossima volta si riscansiona semplicemente di nuovo
        }
    }
}
