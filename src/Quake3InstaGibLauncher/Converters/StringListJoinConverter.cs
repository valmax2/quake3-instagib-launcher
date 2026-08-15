using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace Quake3InstaGibLauncher.Converters;

/// <summary>Unisce una lista di stringhe in un unico testo separato da virgola, per mostrarla in un
/// TextBlock/Run (es. i nomi dei giocatori conosciuti trovati in un server).</summary>
public sealed class StringListJoinConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable enumerable and not string)
            return string.Join(", ", enumerable.Cast<object>().Select(o => o?.ToString() ?? ""));
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
