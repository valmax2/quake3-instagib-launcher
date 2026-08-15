using System.Globalization;
using System.Windows.Data;

namespace Quake3InstaGibLauncher.Converters;

/// <summary>True se i due valori passati (tipicamente l'elemento di una riga e la selezione
/// corrente del ViewModel) sono lo stesso riferimento. Usato per evidenziare "questo e' l'elemento
/// selezionato" in liste dove il modello dati (es. ServerInfo) non ha una proprieta' IsSelected
/// propria.</summary>
public sealed class ObjectEqualsConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;
        return ReferenceEquals(values[0], values[1]) && values[0] is not null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
