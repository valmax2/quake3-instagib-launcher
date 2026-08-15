using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Quake3InstaGibLauncher.Services;

namespace Quake3InstaGibLauncher.Views;

public partial class PlayerProfileView : UserControl
{
    public PlayerProfileView()
    {
        InitializeComponent();
        BuildPalette();
        NameTextBox.TextChanged += (_, _) => RefreshPreview();
        IsVisibleChanged += (_, _) => { if (IsVisible) RefreshPreview(); };
    }

    /// <summary>Costruisce gli 8 pulsanti colore della palette Quake III a codice, cosi' i colori
    /// restano perfettamente in sincrono con quelli usati per l'anteprima (Quake3ColorTextRenderer).</summary>
    private void BuildPalette()
    {
        for (var i = 0; i < Quake3ColorTextRenderer.Palette.Length; i++)
        {
            var code = i;
            var swatch = new Button
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(0, 0, 6, 0),
                Background = new SolidColorBrush(Quake3ColorTextRenderer.Palette[i]),
                BorderBrush = System.Windows.Application.Current.Resources["Brush.PanelBorder"] as Brush,
                BorderThickness = new Thickness(1),
                ToolTip = $"^{code}",
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            swatch.Click += (_, _) => InsertColorCode(code);
            PalettePanel.Children.Add(swatch);
        }
    }

    /// <summary>Inserisce "^N" nella posizione attuale del cursore nel TextBox del nome (non e'
    /// un semplice append: l'utente puo' posizionarsi in mezzo a una parola gia' scritta).</summary>
    private void InsertColorCode(int code)
    {
        var caret = NameTextBox.SelectionStart;
        NameTextBox.Text = NameTextBox.Text.Insert(caret, $"^{code}");
        NameTextBox.SelectionStart = caret + 2;
        NameTextBox.Focus();
    }

    private void RefreshPreview()
    {
        PreviewText.Inlines.Clear();
        foreach (var run in Quake3ColorTextRenderer.BuildRuns(NameTextBox.Text))
            PreviewText.Inlines.Add(run);
    }
}
