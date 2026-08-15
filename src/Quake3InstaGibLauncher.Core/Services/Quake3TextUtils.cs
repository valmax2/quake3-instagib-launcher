using System.Text;

namespace Quake3InstaGibLauncher.Core.Services;

/// <summary>Utilita' condivise per il testo del protocollo Quake III (nomi server/giocatori).</summary>
public static class Quake3TextUtils
{
    /// <summary>Rimuove i codici colore Quake III ("^N", N = cifra 0-9) da un nome/stringa,
    /// per confronti e visualizzazione. Es. "^1Ross^7i" -> "Rossi".</summary>
    public static string StripColorCodes(string text)
    {
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '^' && i + 1 < text.Length && char.IsDigit(text[i + 1]))
            {
                i++;
                continue;
            }
            result.Append(text[i]);
        }
        return result.ToString();
    }
}
