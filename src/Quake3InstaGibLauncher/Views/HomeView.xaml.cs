using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Quake3InstaGibLauncher.Models;
using Quake3InstaGibLauncher.ViewModels;

namespace Quake3InstaGibLauncher.Views;

public partial class HomeView : UserControl
{
    private DispatcherTimer? _gifTimer;
    private BitmapFrame[]? _gifFrames;
    private int _gifFrameIndex;

    public HomeView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) => { if (IsVisible) ApplyLogoSettings(); };
    }

    /// <summary>
    /// Applica logo (vettoriale o immagine/GIF importata), posizione, dimensione, asse e
    /// velocita' di rotazione in base alle preferenze correnti (Personalizza interfaccia).
    /// Richiamato ogni volta che la Home torna visibile.
    ///
    /// L'asse Z e' una vera rotazione nel piano (RotateTransform). Gli assi X/Y, non piu'
    /// disponibili come pseudo-3D nativo in .NET (Core) WPF (UIElement.Projection e' stato
    /// rimosso rispetto a .NET Framework), sono simulati con un ribaltamento 2D (ScaleTransform
    /// da 1 a -1 e ritorno), un effetto "flip" equivalente dal punto di vista visivo.
    /// </summary>
    private void ApplyLogoSettings()
    {
        StopGifAnimation();

        if (DataContext is not MainViewModel vm) return;
        var theme = vm.Settings.Theme;

        // Reset di entrambe le coppie di transform (vettoriale + immagine custom).
        foreach (var (spin, flip) in new[] { (EmblemSpin, EmblemFlip), (CustomLogoSpin, CustomLogoFlip) })
        {
            spin.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
            flip.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
            flip.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
            spin.Angle = 0;
            flip.ScaleX = 1;
            flip.ScaleY = 1;
        }

        switch (theme.LogoPos)
        {
            case LogoPosition.Hidden:
                EmblemContainer.Visibility = Visibility.Collapsed;
                return;
            case LogoPosition.BehindTitle:
                EmblemContainer.Visibility = Visibility.Visible;
                EmblemContainer.Margin = new Thickness(0, -60, 0, -90);
                break;
            default: // AboveTitle
                EmblemContainer.Visibility = Visibility.Visible;
                EmblemContainer.Margin = new Thickness(0, 0, 0, 10);
                break;
        }

        var size = Math.Clamp(theme.LogoSize, 80, 600);
        EmblemContainer.Width = size;
        EmblemContainer.Height = size;

        var useCustomLogo = !string.IsNullOrWhiteSpace(theme.LogoImagePath) && File.Exists(theme.LogoImagePath);
        VectorEmblemViewbox.Visibility = useCustomLogo ? Visibility.Collapsed : Visibility.Visible;
        CustomLogoImage.Visibility = useCustomLogo ? Visibility.Visible : Visibility.Collapsed;

        System.Windows.Media.RotateTransform activeSpin;
        System.Windows.Media.ScaleTransform activeFlip;

        if (useCustomLogo)
        {
            activeSpin = CustomLogoSpin;
            activeFlip = CustomLogoFlip;

            if (theme.LogoImageIsAnimatedGif)
                StartGifAnimation(theme.LogoImagePath!);
            else
                CustomLogoImage.Source = Services.ThemeService.LoadBitmapNoCache(theme.LogoImagePath!);
        }
        else
        {
            activeSpin = EmblemSpin;
            activeFlip = EmblemFlip;
        }

        if (!theme.LogoRotationEnabled)
            return;

        var seconds = Math.Clamp(theme.LogoRotationSeconds, 5, 600);

        if (theme.LogoAxis == LogoRotationAxis.Z)
        {
            var spinAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(seconds)),
                RepeatBehavior = RepeatBehavior.Forever,
            };
            activeSpin.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, spinAnimation);
            return;
        }

        // Ribaltamento continuo 1 -> -1 -> 1 su ScaleX (asse Y, verticale) o ScaleY (asse X, orizzontale).
        var flipAnimation = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        var half = TimeSpan.FromSeconds(seconds / 2.0);
        flipAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.Zero));
        flipAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(-1, half));
        flipAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromSeconds(seconds)));

        var property = theme.LogoAxis == LogoRotationAxis.Y
            ? System.Windows.Media.ScaleTransform.ScaleXProperty
            : System.Windows.Media.ScaleTransform.ScaleYProperty;

        activeFlip.BeginAnimation(property, flipAnimation);
    }

    /// <summary>
    /// WPF non anima i GIF quando assegnati direttamente a Image.Source (mostra solo il primo
    /// fotogramma): qui decodifichiamo tutti i fotogrammi e li facciamo scorrere con un timer.
    /// Usa un intervallo fisso (100ms) invece di leggere il ritardo specifico di ogni fotogramma
    /// dai metadati GIF, per semplicita' e robustezza: il risultato resta un'animazione fluida
    /// per la stragrande maggioranza dei GIF usati come logo/icona.
    /// </summary>
    private void StartGifAnimation(string path)
    {
        try
        {
            // IgnoreImageCache: senza questo flag, re-importare una nuova GIF con lo stesso nome
            // file (vedi ThemeImageService.Import) mostrerebbe ancora l'animazione precedente,
            // gia' in cache per quello stesso URI, finche' l'app non viene riavviata.
            var decoder = BitmapDecoder.Create(new Uri(path, UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return;

            _gifFrames = decoder.Frames.ToArray();
            _gifFrameIndex = 0;
            CustomLogoImage.Source = _gifFrames[0];

            if (_gifFrames.Length == 1) return; // GIF statico (un solo fotogramma): niente da animare

            _gifTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _gifTimer.Tick += (_, _) =>
            {
                _gifFrameIndex = (_gifFrameIndex + 1) % _gifFrames.Length;
                CustomLogoImage.Source = _gifFrames[_gifFrameIndex];
            };
            _gifTimer.Start();
        }
        catch
        {
            // GIF non leggibile/corrotto: mostriamo semplicemente nessuna immagine invece di crashare.
        }
    }

    private void StopGifAnimation()
    {
        _gifTimer?.Stop();
        _gifTimer = null;
        _gifFrames = null;
    }
}
