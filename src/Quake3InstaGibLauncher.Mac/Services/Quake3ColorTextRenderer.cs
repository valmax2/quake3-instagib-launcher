using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Quake3InstaGibLauncher.Mac.Services;

/// <summary>Converte un testo con codici colore Quake III ("^0".."^7") in una sequenza di Run
/// Avalonia colorati, per l'anteprima dal vivo del nome giocatore in Personaggio.</summary>
public static class Quake3ColorTextRenderer
{
    /// <summary>Palette standard Quake III: 0 nero, 1 rosso, 2 verde, 3 giallo, 4 blu, 5 ciano,
    /// 6 magenta, 7 bianco (stesso ordine mostrato dalla palette cliccabile nella UI).</summary>
    public static readonly Color[] Palette =
    {
        Colors.Black,
        Color.FromRgb(0xE0, 0x30, 0x30),
        Color.FromRgb(0x30, 0xC0, 0x40),
        Color.FromRgb(0xE0, 0xD0, 0x30),
        Color.FromRgb(0x40, 0x70, 0xE0),
        Color.FromRgb(0x30, 0xC0, 0xC0),
        Color.FromRgb(0xE0, 0x40, 0xE0),
        Colors.White,
    };

    public static List<Run> BuildRuns(string coloredText)
    {
        var currentColor = Palette[7]; // bianco finche' non compare un codice colore
        var buffer = new System.Text.StringBuilder();
        var runs = new List<Run>();

        void Flush()
        {
            if (buffer.Length == 0) return;
            runs.Add(new Run(buffer.ToString()) { Foreground = new SolidColorBrush(currentColor) });
            buffer.Clear();
        }

        for (var i = 0; i < coloredText.Length; i++)
        {
            if (coloredText[i] == '^' && i + 1 < coloredText.Length && char.IsDigit(coloredText[i + 1]))
            {
                Flush();
                currentColor = Palette[(coloredText[i + 1] - '0') % 8];
                i++; // salta la cifra del codice colore
                continue;
            }
            buffer.Append(coloredText[i]);
        }
        Flush();
        return runs;
    }
}
