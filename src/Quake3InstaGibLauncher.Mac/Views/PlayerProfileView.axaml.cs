using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Quake3InstaGibLauncher.Mac.Services;

namespace Quake3InstaGibLauncher.Mac.Views;

/// <summary>
/// Code-behind per la palette colori cliccabile e l'anteprima dal vivo del nome: la posizione del
/// cursore in un TextBox non e' bindabile in modo pulito da MVVM puro, quindi qui (come nella
/// versione Windows) si gestisce direttamente.
/// </summary>
public partial class PlayerProfileView : UserControl
{
    public PlayerProfileView()
    {
        InitializeComponent();
        BuildPalette();

        NameTextBox.TextChanged += (_, _) => UpdatePreview();
        this.AttachedToVisualTree += (_, _) => UpdatePreview();
    }

    private void BuildPalette()
    {
        for (var i = 0; i < Quake3ColorTextRenderer.Palette.Length; i++)
        {
            var code = i;
            var swatch = new Button
            {
                Width = 30,
                Height = 30,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Quake3ColorTextRenderer.Palette[i]),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
            };
            swatch.Click += (_, _) => InsertColorCode(code);
            PalettePanel.Children.Add(swatch);
        }
    }

    private void InsertColorCode(int code)
    {
        var caret = NameTextBox.CaretIndex;
        var text = NameTextBox.Text ?? string.Empty;
        var insert = $"^{code}";
        NameTextBox.Text = text.Insert(Math.Clamp(caret, 0, text.Length), insert);
        NameTextBox.CaretIndex = caret + insert.Length;
        NameTextBox.Focus();
    }

    private void UpdatePreview()
    {
        PreviewText.Inlines?.Clear();
        var runs = Quake3ColorTextRenderer.BuildRuns(NameTextBox.Text ?? string.Empty);
        foreach (var run in runs)
            PreviewText.Inlines?.Add(run);
    }
}
