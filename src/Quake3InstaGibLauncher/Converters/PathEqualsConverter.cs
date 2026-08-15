using System.Globalization;
using System.Windows.Data;

namespace Quake3InstaGibLauncher.Converters;

/// <summary>Confronta due percorsi file (case-insensitive) per evidenziare, in una galleria di
/// anteprime, quale miniatura corrisponde all'immagine attualmente selezionata.</summary>
public sealed class PathEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string a || values[1] is not string b)
            return false;
        return !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
