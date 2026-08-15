using System.Globalization;
using System.Windows.Data;

namespace Quake3InstaGibLauncher.Converters;

/// <summary>Permette di legare un RadioButton a un valore specifico di un enum (ConverterParameter = nome del valore).</summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool b || !b || parameter is null) return Binding.DoNothing;
        return Enum.Parse(targetType, parameter.ToString()!);
    }
}
