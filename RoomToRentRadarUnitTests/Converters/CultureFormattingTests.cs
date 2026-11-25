using System.Globalization;
using RoomToRentRadar.Converters;

namespace RoomToRentRadarUnitTests.Converters;

[TestFixture]
public class CultureFormattingTests
{
    private static readonly CultureInfo ViVn = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    // UT01-UT06: Standard values in vi-VN
    [Test]
    public void FormatCurrency_StandardValue_ViVn_ReturnsDotSeparatorWithDongSymbol()
    {
        Assert.That(CultureFormatting.FormatCurrency(2200000m, ViVn), Is.EqualTo("2.200.000 ₫"));
        Assert.That(CultureFormatting.FormatCurrency(2200000L, ViVn), Is.EqualTo("2.200.000 ₫"));
        Assert.That(CultureFormatting.FormatCurrency(2200000, ViVn), Is.EqualTo("2.200.000 ₫"));
    }

    [Test]
    public void FormatNumber_StandardValue_ViVn_ReturnsLocalizedSeparators()
    {
        Assert.That(CultureFormatting.FormatNumber(12345.67, ViVn), Is.EqualTo("12.345,67"));
        Assert.That(CultureFormatting.FormatNumber(12345.67m, ViVn), Is.EqualTo("12.345,67"));
        Assert.That(CultureFormatting.FormatNumber(-12345.67, ViVn), Is.EqualTo("-12.345,67"));
        Assert.That(CultureFormatting.FormatNumber(12345.67, ViVn, "N0"), Is.EqualTo("12.346"));
    }

    [Test]
    public void FormatDate_StandardValue_ViVn_ReturnsDayMonthYear()
    {
        var date = new DateOnly(2026, 11, 25);

        Assert.That(CultureFormatting.FormatDate(date, ViVn), Is.EqualTo("25/11/2026"));
        Assert.That(CultureFormatting.FormatDate(new DateTime(2026, 11, 25, 16, 30, 0), ViVn), Is.EqualTo("25/11/2026"));
    }

    [Test]
    public void FormatTime_StandardValue_ViVn_Returns24HourClock()
    {
        var time = new TimeOnly(16, 30);

        Assert.That(CultureFormatting.FormatTime(time, ViVn), Is.EqualTo("16:30"));
        Assert.That(CultureFormatting.FormatTime(new DateTime(2026, 11, 25, 16, 30, 0), ViVn), Is.EqualTo("16:30"));
        Assert.That(CultureFormatting.FormatTime(new TimeSpan(16, 30, 0), ViVn), Is.EqualTo("16:30"));
    }

    [Test]
    public void FormatArea_StandardValue_ViVn_ReturnsCommaDecimalWithUnit()
    {
        Assert.That(CultureFormatting.FormatArea(25.5, ViVn), Is.EqualTo("25,5 m²"));
        Assert.That(CultureFormatting.FormatArea(25.5m, ViVn), Is.EqualTo("25,5 m²"));
        Assert.That(CultureFormatting.FormatArea(25.0, ViVn), Is.EqualTo("25 m²"));
    }

    [Test]
    public void FormatDistance_StandardValue_ViVn_ReturnsKilometersWithComma()
    {
        Assert.That(CultureFormatting.FormatDistance(1500, ViVn), Is.EqualTo("1,5 km"));
        Assert.That(CultureFormatting.FormatDistance(1500m, ViVn), Is.EqualTo("1,5 km"));
    }

    // UT07-UT13: Standard values in en-US (currency stays VND, never USD/$)
    [Test]
    public void FormatCurrency_StandardValue_EnUs_StaysVndWithoutUsdSymbol()
    {
        var formatted = CultureFormatting.FormatCurrency(2200000m, EnUs);

        Assert.That(formatted, Is.EqualTo("2,200,000 VND"));
        Assert.That(formatted, Does.Not.Contain("$"));
        Assert.That(formatted, Does.Not.Contain("USD"));
    }

    [Test]
    public void FormatNumber_StandardValue_EnUs_ReturnsLocalizedSeparators()
    {
        Assert.That(CultureFormatting.FormatNumber(12345.67, EnUs), Is.EqualTo("12,345.67"));
        Assert.That(CultureFormatting.FormatNumber(-12345.67, EnUs), Is.EqualTo("-12,345.67"));
        Assert.That(CultureFormatting.FormatNumber(12345.67, EnUs, "N1"), Is.EqualTo("12,345.7"));
    }

    [Test]
    public void FormatDate_StandardValue_EnUs_ReturnsMonthDayYear()
    {
        Assert.That(CultureFormatting.FormatDate(new DateOnly(2026, 11, 25), EnUs), Is.EqualTo("11/25/2026"));
        Assert.That(CultureFormatting.FormatDate(new DateTime(2026, 11, 25, 16, 30, 0), EnUs), Is.EqualTo("11/25/2026"));
    }

    [Test]
    public void FormatTime_StandardValue_EnUs_Returns12HourClockWithMeridiem()
    {
        Assert.That(CultureFormatting.FormatTime(new TimeOnly(16, 30), EnUs), Is.EqualTo("4:30 PM"));
        Assert.That(CultureFormatting.FormatTime(new DateTime(2026, 11, 25, 16, 30, 0), EnUs), Is.EqualTo("4:30 PM"));
        Assert.That(CultureFormatting.FormatTime(new TimeSpan(16, 30, 0), EnUs), Is.EqualTo("4:30 PM"));
    }

    [Test]
    public void FormatArea_StandardValue_EnUs_ReturnsDotDecimalWithUnit()
    {
        Assert.That(CultureFormatting.FormatArea(25.5, EnUs), Is.EqualTo("25.5 m²"));
        Assert.That(CultureFormatting.FormatArea(25.0, EnUs), Is.EqualTo("25 m²"));
    }

    [Test]
    public void FormatDistance_StandardValue_EnUs_ReturnsKilometersWithDot()
    {
        Assert.That(CultureFormatting.FormatDistance(1500, EnUs), Is.EqualTo("1.5 km"));
    }

    // UT14: Zero is valid and must not become fallback em-dash
    [Test]
    public void Formatters_ZeroValue_ReturnsFormattedZeroNotFallback()
    {
        Assert.That(CultureFormatting.FormatCurrency(0m, ViVn), Is.EqualTo("0 ₫"));
        Assert.That(CultureFormatting.FormatCurrency(0, EnUs), Is.EqualTo("0 VND"));
        Assert.That(CultureFormatting.FormatCurrency(-0.0, EnUs), Is.EqualTo("0 VND"));
        Assert.That(CultureFormatting.FormatNumber(0, ViVn), Is.EqualTo("0,00"));
        Assert.That(CultureFormatting.FormatNumber(-0.0, EnUs), Is.EqualTo("0.00"));
        Assert.That(CultureFormatting.FormatArea(0, ViVn), Is.EqualTo("0 m²"));
        Assert.That(CultureFormatting.FormatArea(-0.0, EnUs), Is.EqualTo("0 m²"));
        Assert.That(CultureFormatting.FormatDistance(0, ViVn), Is.EqualTo("0 m"));
        Assert.That(CultureFormatting.FormatDistance(-0.0, EnUs), Is.EqualTo("0 m"));
    }

    // UT15: Null and invalid types safely return "—" without throwing
    [TestCase(null)]
    [TestCase("2200000")]
    [TestCase("12,345.67")]
    [TestCase(true)]
    public void NumericFormatters_NullOrInvalidType_ReturnFallback(object? invalidValue)
    {
        Assert.That(CultureFormatting.FormatCurrency(invalidValue, ViVn), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatNumber(invalidValue, EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatArea(invalidValue, ViVn), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatDistance(invalidValue, EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
    }

    [Test]
    public void DateAndTimeFormatters_NullOrInvalidType_ReturnFallback()
    {
        Assert.That(CultureFormatting.FormatDate(null, ViVn), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatDate("2026-11-25", ViVn), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatDate(new TimeOnly(16, 30), ViVn), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatDate(12345, EnUs), Is.EqualTo(CultureFormatting.FallbackValue));

        Assert.That(CultureFormatting.FormatTime(null, EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatTime("16:30", EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatTime(new DateOnly(2026, 11, 25), EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatTime(TimeSpan.FromHours(-1), EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatTime(TimeSpan.FromHours(24), EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
    }

    [TestCase("INVALID_FMT_999")]
    [TestCase("Z")]
    [TestCase("C2")]
    [TestCase("0..#")]
    public void FormatNumber_InvalidFormatSpecifier_ReturnsFallback(string invalidFormat)
    {
        Assert.That(CultureFormatting.FormatNumber(123.45, EnUs, invalidFormat), Is.EqualTo(CultureFormatting.FallbackValue));
    }

    [Test]
    public void FormatNumber_SupportedCustomFormat_FormatsWithCulture()
    {
        Assert.That(CultureFormatting.FormatNumber(12345.67, ViVn, "#,##0.#"), Is.EqualTo("12.345,7"));
        Assert.That(CultureFormatting.FormatNumber(12345.67, EnUs, "0.#"), Is.EqualTo("12345.7"));
    }

    // UT16: Negative currency/area/distance and non-finite floating-point inputs return "—"
    [TestCase(-1d)]
    [TestCase(-0.1d)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void NonNegativeFormatters_NegativeOrNonFiniteDouble_ReturnFallback(double invalidValue)
    {
        Assert.That(CultureFormatting.FormatCurrency(invalidValue, ViVn), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatArea(invalidValue, ViVn), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatDistance(invalidValue, EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void AllNumericFormatters_NonFiniteFloat_ReturnFallback(float invalidValue)
    {
        Assert.That(CultureFormatting.FormatCurrency(invalidValue, ViVn), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatNumber(invalidValue, EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatArea(invalidValue, ViVn), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatDistance(invalidValue, EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
    }

    [Test]
    public void NonNegativeFormatters_NegativeDecimal_ReturnFallback()
    {
        Assert.That(CultureFormatting.FormatCurrency(-100000m, EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatArea(-12.5m, ViVn), Is.EqualTo(CultureFormatting.FallbackValue));
        Assert.That(CultureFormatting.FormatDistance(-50m, EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
    }

    [Test]
    public void FormatCurrency_OverflowDouble_ReturnsFallback()
    {
        Assert.That(CultureFormatting.FormatCurrency(1e30d, EnUs), Is.EqualTo(CultureFormatting.FallbackValue));
    }

    // UT17: Distance threshold & rounding near 1000m
    [TestCase(0d, "vi-VN", "0 m")]
    [TestCase(350d, "vi-VN", "350 m")]
    [TestCase(350d, "en-US", "350 m")]
    [TestCase(999d, "vi-VN", "999 m")]
    [TestCase(999d, "en-US", "999 m")]
    [TestCase(999.4d, "vi-VN", "999,4 m")]
    [TestCase(999.4d, "en-US", "999.4 m")]
    [TestCase(999.9d, "vi-VN", "999,9 m")]
    [TestCase(999.9d, "en-US", "999.9 m")]
    [TestCase(999.95d, "vi-VN", "1 km")]
    [TestCase(999.95d, "en-US", "1 km")]
    [TestCase(999.96d, "vi-VN", "1 km")]
    [TestCase(999.96d, "en-US", "1 km")]
    [TestCase(1000d, "vi-VN", "1 km")]
    [TestCase(1000d, "en-US", "1 km")]
    [TestCase(1500d, "vi-VN", "1,5 km")]
    [TestCase(1500d, "en-US", "1.5 km")]
    public void FormatDistance_ThresholdCases_ReturnsExpectedUnitAndSeparator(
        double meters,
        string cultureCode,
        string expected)
    {
        var culture = CultureInfo.GetCultureInfo(cultureCode);

        var actual = CultureFormatting.FormatDistance(meters, culture);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void FormatDistance_DecimalBoundaryNear1000_PromotesToKilometers()
    {
        Assert.That(CultureFormatting.FormatDistance(999.96m, ViVn), Is.EqualTo("1 km"));
        Assert.That(CultureFormatting.FormatDistance(999.9m, ViVn), Is.EqualTo("999,9 m"));
    }

    // UT18: Switching culture changes output while raw value stays unchanged
    [Test]
    public void SwitchingCulture_SameInputValue_ChangesOutputWithoutMutatingValue()
    {
        const decimal rent = 2200000m;
        const double area = 25.5;
        const double distance = 1500;

        var viRent = CultureFormatting.FormatCurrency(rent, ViVn);
        var enRent = CultureFormatting.FormatCurrency(rent, EnUs);
        var viArea = CultureFormatting.FormatArea(area, ViVn);
        var enArea = CultureFormatting.FormatArea(area, EnUs);
        var viDistance = CultureFormatting.FormatDistance(distance, ViVn);
        var enDistance = CultureFormatting.FormatDistance(distance, EnUs);

        Assert.That(viRent, Is.EqualTo("2.200.000 ₫"));
        Assert.That(enRent, Is.EqualTo("2,200,000 VND"));
        Assert.That(viArea, Is.EqualTo("25,5 m²"));
        Assert.That(enArea, Is.EqualTo("25.5 m²"));
        Assert.That(viDistance, Is.EqualTo("1,5 km"));
        Assert.That(enDistance, Is.EqualTo("1.5 km"));
        Assert.That(rent, Is.EqualTo(2200000m));
        Assert.That(area, Is.EqualTo(25.5));
        Assert.That(distance, Is.EqualTo(1500));
    }

    // UT19: Explicit culture takes precedence over Thread.CurrentThread.CurrentCulture
    [Test]
    [NonParallelizable]
    public void ExplicitCulture_DifferentFromThreadCulture_TakesPrecedence()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        var originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = ViVn;
            Thread.CurrentThread.CurrentUICulture = ViVn;

            Assert.That(CultureFormatting.FormatCurrency(2200000m, EnUs), Is.EqualTo("2,200,000 VND"));
            Assert.That(CultureFormatting.FormatNumber(12345.67, EnUs), Is.EqualTo("12,345.67"));
            Assert.That(CultureFormatting.FormatDate(new DateOnly(2026, 11, 25), EnUs), Is.EqualTo("11/25/2026"));
            Assert.That(CultureFormatting.FormatTime(new TimeOnly(16, 30), EnUs), Is.EqualTo("4:30 PM"));
            Assert.That(CultureFormatting.FormatArea(25.5, EnUs), Is.EqualTo("25.5 m²"));
            Assert.That(CultureFormatting.FormatDistance(1500, EnUs), Is.EqualTo("1.5 km"));

            // Null culture falls back to current thread culture (vi-VN)
            Assert.That(CultureFormatting.FormatCurrency(2200000m, null), Is.EqualTo("2.200.000 ₫"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    [Test]
    public void ResolveCulture_HandlesCultureInfoStringAndInvalidFallback()
    {
        Assert.That(CultureFormatting.ResolveCulture(ViVn, EnUs).Name, Is.EqualTo("vi-VN"));
        Assert.That(CultureFormatting.ResolveCulture("vi-VN", EnUs).Name, Is.EqualTo("vi-VN"));
        Assert.That(CultureFormatting.ResolveCulture("en-US", ViVn).Name, Is.EqualTo("en-US"));
        Assert.That(CultureFormatting.ResolveCulture("not-a-valid-culture-xyz", EnUs).Name, Is.EqualTo("en-US"));
        Assert.That(CultureFormatting.ResolveCulture(null, ViVn).Name, Is.EqualTo("vi-VN"));
    }

    // UT20: Date/time boundary values (00:00, 12:00, 23:59, UTC & offset) keep date/time without timezone shift
    [Test]
    public void FormatDateAndTime_BoundaryValuesAndTimeZones_DoNotShiftDateOrClock()
    {
        var midnightUtc = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
        var noonUtc = new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        var endOfDayOffset = new DateTimeOffset(2026, 11, 25, 23, 59, 0, TimeSpan.FromHours(-5));

        Assert.That(CultureFormatting.FormatDate(midnightUtc, ViVn), Is.EqualTo("25/11/2026"));
        Assert.That(CultureFormatting.FormatDate(midnightUtc, EnUs), Is.EqualTo("11/25/2026"));
        Assert.That(CultureFormatting.FormatDate(endOfDayOffset, ViVn), Is.EqualTo("25/11/2026"));
        Assert.That(CultureFormatting.FormatDate(endOfDayOffset, EnUs), Is.EqualTo("11/25/2026"));

        Assert.That(CultureFormatting.FormatTime(new TimeOnly(0, 0), ViVn), Is.EqualTo("00:00"));
        Assert.That(CultureFormatting.FormatTime(new TimeOnly(0, 0), EnUs), Is.EqualTo("12:00 AM"));
        Assert.That(CultureFormatting.FormatTime(noonUtc, ViVn), Is.EqualTo("12:00"));
        Assert.That(CultureFormatting.FormatTime(noonUtc, EnUs), Is.EqualTo("12:00 PM"));
        Assert.That(CultureFormatting.FormatTime(endOfDayOffset, ViVn), Is.EqualTo("23:59"));
        Assert.That(CultureFormatting.FormatTime(endOfDayOffset, EnUs), Is.EqualTo("11:59 PM"));
    }
}
