using System.Globalization;

namespace RoomToRentRadar.Converters;

/// <summary>
/// Culture-aware room area display converter in square meters (OneWay).
/// Supports <see cref="IValueConverter"/> and <see cref="IMultiValueConverter"/> (<c>[areaM2, culture]</c>).
/// </summary>
public sealed class AreaConverter : IValueConverter, IMultiValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resolvedCulture = CultureFormatting.ResolveCulture(parameter, culture);
        return CultureFormatting.FormatArea(value, resolvedCulture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(AreaConverter)} supports OneWay display formatting only.");

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is null || values.Length == 0)
        {
            return CultureFormatting.FallbackValue;
        }

        var cultureInput = values.Length > 1 && values[1] is CultureInfo or string
            ? values[1]
            : parameter;
        var resolvedCulture = CultureFormatting.ResolveCulture(cultureInput, culture);
        return CultureFormatting.FormatArea(values[0], resolvedCulture);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(AreaConverter)} supports OneWay display formatting only.");
}
