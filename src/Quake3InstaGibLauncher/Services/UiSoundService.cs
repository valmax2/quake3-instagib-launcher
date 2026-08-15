using System.IO;
using System.Media;
using System.Text;

namespace Quake3InstaGibLauncher.Services;

/// <summary>
/// Suoni dell'interfaccia (passaggio del mouse e clic sui pulsanti), in stile sci-fi da sparatutto
/// arena. IMPORTANTE: sono generati matematicamente a runtime (toni sinusoidali sintetici) — non
/// sono e non contengono i suoni originali di Quake III, che sono materiale protetto da copyright
/// di id Software/Bethesda e non possono essere estratti o ridistribuiti con questa app.
/// </summary>
public static class UiSoundService
{
    /// <summary>Attivato/disattivato da UiThemeSettings.UiSoundsEnabled (vedi ThemeService.Apply,
    /// che e' il punto centrale gia' chiamato ad ogni caricamento/modifica delle preferenze).</summary>
    public static bool Enabled { get; set; } = true;

    private static readonly Lazy<SoundPlayer> HoverPlayer = new(() => BuildPlayer(BuildBlip(1600, 2200, 40, 0.16)));
    private static readonly Lazy<SoundPlayer> ClickPlayer = new(() => BuildPlayer(BuildBlip(760, 340, 75, 0.32)));

    public static void PlayHover()
    {
        if (Enabled) TryPlay(HoverPlayer.Value);
    }

    public static void PlayClick()
    {
        if (Enabled) TryPlay(ClickPlayer.Value);
    }

    private static void TryPlay(SoundPlayer player)
    {
        try { player.Play(); }
        catch { /* nessun dispositivo audio disponibile o simile: non deve mai bloccare l'interfaccia */ }
    }

    private static SoundPlayer BuildPlayer(byte[] wavBytes)
    {
        var player = new SoundPlayer(new MemoryStream(wavBytes));
        try { player.Load(); } catch { /* si ritentera' comunque al momento del Play() */ }
        return player;
    }

    /// <summary>Genera un breve "blip" sintetico: un tono sinusoidale la cui frequenza scivola da
    /// frequencyStartHz a frequencyEndHz in durationMs millisecondi, con inviluppo a dissolvenza in
    /// uscita (parte forte, si spegne dolcemente) per evitare click/scoppiettii, incapsulato in un
    /// file WAV PCM 16 bit mono standard.</summary>
    private static byte[] BuildBlip(double frequencyStartHz, double frequencyEndHz, int durationMs, double volume)
    {
        const int sampleRate = 44100;
        var sampleCount = sampleRate * durationMs / 1000;
        var samples = new short[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var t = (double)i / sampleCount;
            var frequency = frequencyStartHz + (frequencyEndHz - frequencyStartHz) * t;
            var phase = 2 * Math.PI * frequency * i / sampleRate;
            var envelope = Math.Pow(1 - t, 1.5);
            var sample = Math.Sin(phase) * volume * envelope;
            samples[i] = (short)Math.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue);
        }

        return WrapAsWav(samples, sampleRate);
    }

    private static byte[] WrapAsWav(short[] samples, int sampleRate)
    {
        const int bitsPerSample = 16;
        const int channels = 1;
        var dataLength = samples.Length * 2;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write((short)bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        foreach (var sample in samples)
            writer.Write(sample);

        return ms.ToArray();
    }
}
