using System.Globalization;
using BindableProperty = Microsoft.Maui.Controls.BindableProperty;
using RoomToRentRadar.Converters;

namespace RoomToRentRadarUnitTests.Converters;

[TestFixture]
public class FormattingConverterTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private static IEnumerable<TestCaseData> ConverterCases()
    {
        yield return new(new CurrencyConverter(), 2500000, "2,500,000 ₫", "2.500.000 ₫");
        yield return new(new AreaConverter(), 25.5m, "25.5 m²", "25,5 m²");
        yield return new(new DistanceConverter(), 1500, "1.5 km", "1,5 km");
        yield return new(new NumberConverter(), 1234.5m, "1,234.50", "1.234,50");
        yield return new(new DateConverter(), new DateOnly(2000, 11, 15), "15 Nov 2000", "15/11/2000");
        yield return new(new TimeConverter(), new TimeOnly(16, 30), "4:30 PM", "16:30");
    }

    [TestCaseSource(nameof(ConverterCases))]
    public void Convert_BindingCultureAndFallback_ReturnLocalizedValues(
        IValueConverter converter, object input, string english, string vietnamese)
    {
        var multi = (IMultiValueConverter)converter;
        Assert.That(converter.Convert(input, typeof(string), null, English), Is.EqualTo(english));
        Assert.That(converter.Convert(input, typeof(string), Vietnamese, English), Is.EqualTo(vietnamese));
        Assert.That(multi.Convert([input, "vi-VN"], typeof(string), null!, English), Is.EqualTo(vietnamese));
        // The number converter reserves its parameter for a format string.
        var fallback = converter is NumberConverter ? english : vietnamese;
        Assert.That(multi.Convert([input, BindableProperty.UnsetValue], typeof(string), Vietnamese, English), Is.EqualTo(fallback));
        Assert.That(multi.Convert([input], typeof(string), Vietnamese, English), Is.EqualTo(fallback));
    }

    [TestCaseSource(nameof(ConverterCases))]
    public void Convert_MissingValuesAndReverseBinding_RespectOneWayContract(
        IValueConverter converter, object input, string english, string vietnamese)
    {
        var multi = (IMultiValueConverter)converter;
        Assert.That(converter.Convert(null, typeof(string), null, English), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(multi.Convert(null!, typeof(string), null!, English), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(multi.Convert([], typeof(string), null!, English), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(multi.Convert([BindableProperty.UnsetValue], typeof(string), null!, English), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(input, typeof(object), null, English));
        Assert.Throws<NotSupportedException>(() => multi.ConvertBack(input, [typeof(object)], null!, English));
    }

    [Test]
    public void NumberConverter_FormatParameter_OverridesConfiguredFormatInBothBindingModes()
    {
        var converter = new NumberConverter { Format = "N1" };
        Assert.That(((IValueConverter)converter).Convert(1234.26m, typeof(string), null, English), Is.EqualTo("1,234.3"));
        Assert.That(((IValueConverter)converter).Convert(1234.26m, typeof(string), "N0", English), Is.EqualTo("1,234"));
        Assert.That(((IMultiValueConverter)converter).Convert([1234.26m, Vietnamese], typeof(string), "N2", English),
            Is.EqualTo("1.234,26"));
    }
}
