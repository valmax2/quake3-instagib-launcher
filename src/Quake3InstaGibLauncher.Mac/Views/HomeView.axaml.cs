using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Quake3InstaGibLauncher.Mac.Models;
using Quake3InstaGibLauncher.Mac.ViewModels;
using SkiaSharp;

namespace Quake3InstaGibLauncher.Mac.Views;

public partial class HomeView : UserControl
{
    private readonly ScaleTransform _emblemFlip = new() { ScaleX = 1, ScaleY = 1 };
    private readonly RotateTransform _emblemSpin = new() { Angle = 0 };
    private readonly ScaleTransform _logoFlip = new() { ScaleX = 1, ScaleY = 1 };
    private readonly RotateTransform _logoSpin = new() { Angle = 0 };

    private DispatcherTimer? _timer;
    private DateTime _animationStart;
    private LogoRotationAxis _activeAxis;
    private double _periodSeconds = 90;

    private DispatcherTimer? _gifTimer;
    private List<Bitmap>? _gifFrames;
    private int _gifFrameIndex;
    private string? _gifFramesSourcePath; // evita di ridecodificare la stessa GIF ad ogni ApplyLogoSettings()

    public HomeView()
    {
        InitializeComponent();

        // I transform non sono referenziabili con x:Name in Avalonia (non fanno parte della
        // NameScope visuale): li creiamo qui e li assegniamo esplicitamente al Canvas.
        EmblemCanvas.RenderTransform = new TransformGroup
        {
            Children = { _emblemFlip, _emblemSpin },
        };
        CustomLogoImage.RenderTransform = new TransformGroup
        {
            Children = { _logoFlip, _logoSpin },
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible)
            ApplyLogoSettings();
    }

    /// <summary>
    /// Applica posizione, dimensione, asse e velocita' di rotazione dell'emblema, equivalente
    /// macOS di HomeView.xaml.cs (Windows). Avalonia non ha un timer di animazione dichiarativo
    /// comodo quanto WPF per questo caso d'uso, quindi si usa un DispatcherTimer che ricalcola
    /// l'angolo/il ribaltamento a ogni tick in base al tempo trascorso: stesso identico effetto
    /// visivo (rotazione Z piatta, "flip" 2D su ScaleX/ScaleY per simulare gli assi Y/X), risultato
    /// deterministico e semplice da verificare per correttezza anche senza un Mac a disposizione.
    /// </summary>
    private void ApplyLogoSettings()
    {
        _timer?.Stop();
        _timer = null;
        _gifTimer?.Stop();
        _gifTimer = null;

        if (DataContext is not MainViewModel vm) return;
        var theme = vm.Settings.Theme;

        foreach (var (spin, flip) in new[] { (_emblemSpin, _emblemFlip), (_logoSpin, _logoFlip) })
        {
            spin.Angle = 0;
            flip.ScaleX = 1;
            flip.ScaleY = 1;
        }

        switch (theme.LogoPos)
        {
            case LogoPosition.Hidden:
                EmblemContainer.IsVisible = false;
                return;
            case LogoPosition.BehindTitle:
                EmblemContainer.IsVisible = true;
                EmblemContainer.Margin = new Thickness(0, -40, 0, -70);
                break;
            default:
                EmblemContainer.IsVisible = true;
                EmblemContainer.Margin = new Thickness(0, 0, 0, 10);
                break;
        }

        var size = Math.Clamp(theme.LogoSize, 80, 600);
        EmblemContainer.Width = size;
        EmblemContainer.Height = size;

        // Logo personalizzato (immagine importata o quello VStudio Apps di default) al posto
        // dell'emblema vettoriale: stessa logica della versione Windows. A differenza di WPF,
        // qui si mostra solo il fotogramma statico anche per una GIF (vedi commento su
        // UiThemeSettings.LogoImagePath: Avalonia non ha un decoder GIF multi-fotogramma pronto
        // equivalente a BitmapDecoder.Frames di WPF).
        var useCustomLogo = !string.IsNullOrWhiteSpace(theme.LogoImagePath) && File.Exists(theme.LogoImagePath);
        VectorEmblemViewbox.IsVisible = !useCustomLogo;
        CustomLogoImage.IsVisible = useCustomLogo;

        RotateTransform activeSpin;
        ScaleTransform activeFlip;

        if (useCustomLogo)
        {
            activeSpin = _logoSpin;
            activeFlip = _logoFlip;
            try
            {
                if (theme.LogoImageIsAnimatedGif)
                    StartGifAnimation(theme.LogoImagePath!);
                else
                {
                    using var stream = File.OpenRead(theme.LogoImagePath!);
                    CustomLogoImage.Source = new Bitmap(stream);
                }
            }
            catch
            {
                // File non leggibile/corrotto: si torna all'emblema vettoriale invece di crashare.
                VectorEmblemViewbox.IsVisible = true;
                CustomLogoImage.IsVisible = false;
                activeSpin = _emblemSpin;
                activeFlip = _emblemFlip;
            }
        }
        else
        {
            activeSpin = _emblemSpin;
            activeFlip = _emblemFlip;
        }

        if (!theme.LogoRotationEnabled) return;

        _activeAxis = theme.LogoAxis;
        _periodSeconds = Math.Clamp(theme.LogoRotationSeconds, 5, 600);
        _animationStart = DateTime.UtcNow;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => OnAnimationTick(activeSpin, activeFlip);
        _timer.Start();
    }

    /// <summary>
    /// Avalonia non anima i GIF multi-fotogramma nativamente (a differenza di WPF, che li anima
    /// via BitmapDecoder.Frames + un timer): qui si ottiene lo stesso risultato decodificando ogni
    /// fotogramma con SkiaSharp (gia' dipendenza del progetto, usata anche da
    /// MacImageConversionService per le anteprime mappe) tramite l'API SKCodec, che supporta la
    /// decodifica per indice di fotogramma dei GIF animati. I fotogrammi decodificati vengono
    /// tenuti in cache per percorso file (vedi _gifFramesSourcePath) cosi' da non ridecodificare
    /// l'intera GIF ogni volta che la Home torna visibile, solo alla primissima volta o se cambia
    /// il file. Intervallo fisso (100ms, stessa scelta della versione Windows) invece di leggere il
    /// ritardo specifico di ogni fotogramma dai metadati GIF: piu' semplice e robusto.
    /// </summary>
    private void StartGifAnimation(string path)
    {
        if (_gifFrames is not null && _gifFramesSourcePath == path)
        {
            CustomLogoImage.Source = _gifFrames[0];
            _gifFrameIndex = 0;
            if (_gifFrames.Count > 1) StartGifTimer();
            return;
        }

        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream);
        if (codec is null) return;

        var frameCount = Math.Max(1, codec.FrameCount);
        var info = codec.Info;
        var frames = new List<Bitmap>(frameCount);

        for (var i = 0; i < frameCount; i++)
        {
            using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            var options = new SKCodecOptions(i);
            var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels(), options);
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput) continue;

            using var image = SKImage.FromBitmap(bitmap);
            using var pngData = image.Encode(SKEncodedImageFormat.Png, quality: 100);
            using var pngStream = new MemoryStream(pngData.ToArray());
            frames.Add(new Bitmap(pngStream));
        }

        if (frames.Count == 0) return;

        _gifFrames = frames;
        _gifFramesSourcePath = path;
        _gifFrameIndex = 0;
        CustomLogoImage.Source = frames[0];

        if (frames.Count > 1) StartGifTimer();
    }

    private void StartGifTimer()
    {
        _gifTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _gifTimer.Tick += (_, _) =>
        {
            if (_gifFrames is null || _gifFrames.Count == 0) return;
            _gifFrameIndex = (_gifFrameIndex + 1) % _gifFrames.Count;
            CustomLogoImage.Source = _gifFrames[_gifFrameIndex];
        };
        _gifTimer.Start();
    }

    private void OnAnimationTick(RotateTransform spin, ScaleTransform flip)
    {
        var elapsed = (DateTime.UtcNow - _animationStart).TotalSeconds;
        var phase = elapsed % _periodSeconds / _periodSeconds; // 0..1

        if (_activeAxis == LogoRotationAxis.Z)
        {
            spin.Angle = phase * 360;
            return;
        }

        // Onda triangolare 1 -> -1 -> 1 per simulare un ribaltamento continuo.
        var triangular = phase < 0.5 ? 1 - 4 * phase : -3 + 4 * phase;

        if (_activeAxis == LogoRotationAxis.Y)
            flip.ScaleX = triangular;
        else
            flip.ScaleY = triangular;
    }
}
