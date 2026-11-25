using System.Globalization;

namespace RoomToRentRadar.Converters;

/// <summary>
/// Culture-aware number display converter (OneWay, default <c>N2</c>).
/// Supports <see cref="IValueConverter"/> and <see cref="IMultiValueConverter"/> (<c>[number, culture]</c>).
/// </summary>
public sealed class NumberConverter : IValueConverter, IMultiValueConverter
{
    public string Format { get; set; } = CultureFormatting.DefaultNumberFormat;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resolvedCulture = parameter is CultureInfo explicitCulture
            ? explicitCulture
            : CultureFormatting.ResolveCulture(null, culture);
        var format = parameter is string paramFormat && !string.IsNullOrWhiteSpace(paramFormat)
            ? paramFormat
            : Format;

        return CultureFormatting.FormatNumber(value, resolvedCulture, format);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(NumberConverter)} supports OneWay display formatting only.");

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is null || values.Length == 0)
        {
            return CultureFormatting.FallbackValue;
        }

        var cultureInput = values.Length > 1 ? values[1] : null;
        var resolvedCulture = CultureFormatting.ResolveCulture(cultureInput, culture);
        var format = parameter is string paramFormat && !string.IsNullOrWhiteSpace(paramFormat)
            ? paramFormat
            : Format;

        return CultureFormatting.FormatNumber(values[0], resolvedCulture, format);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(NumberConverter)} supports OneWay display formatting only.");
}
