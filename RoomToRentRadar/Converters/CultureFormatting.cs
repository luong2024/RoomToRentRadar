using System.Globalization;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Converters;

/// <summary>
/// Pure C# culture-aware display formatting helpers for <c>vi-VN</c> and <c>en-US</c> (BR-I18N-03).
/// Kept free of MAUI dependencies so formatting behavior is directly unit-testable.
/// </summary>
public static class CultureFormatting
{
    public const string FallbackValue = "—";
    public const string DefaultNumberFormat = "N2";

    private const long Thousand = 1_000;
    private const long Million = 1_000_000;

    public static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
    public static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>
    /// Resolves an explicit culture input (<see cref="CultureInfo"/> or culture code string),
    /// falling back to <paramref name="fallbackCulture"/> and then <see cref="CultureInfo.CurrentCulture"/>.
    /// </summary>
    public static CultureInfo ResolveCulture(object? cultureInput, CultureInfo? fallbackCulture = null)
    {
        if (cultureInput is CultureInfo explicitCulture)
        {
            return explicitCulture;
        }

        if (cultureInput is string cultureCode && !string.IsNullOrWhiteSpace(cultureCode))
        {
            try
            {
                return CultureInfo.GetCultureInfo(cultureCode.Trim(), predefinedOnly: true);
            }
            catch (CultureNotFoundException)
            {
                // Fall through to fallback culture safely.
            }
        }

        return fallbackCulture ?? CultureInfo.CurrentCulture;
    }

    public static bool IsVietnamese(CultureInfo? culture)
    {
        var effective = culture ?? CultureInfo.CurrentCulture;
        return effective.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Formats a non-negative VND amount using the provided culture without currency conversion.
    /// <c>vi-VN</c>: <c>2.200.000 ₫</c> · <c>en-US</c>: <c>2,200,000 ₫</c>.
    /// </summary>
    public static string FormatCurrency(object? value, CultureInfo? culture = null)
    {
        if (!TryExtractDecimal(value, out var amount) || amount < 0m)
        {
            return FallbackValue;
        }

        if (amount == 0m)
        {
            amount = 0m;
        }

        var effective = culture ?? CultureInfo.CurrentCulture;
        var formatted = amount.ToString("N0", effective);
        return $"{formatted} ₫";
    }

    /// <summary>
    /// Short VND amount shared by the room card, room detail and compare card: <c>2.5M</c>, <c>2,5 tr</c>,
    /// <c>950k</c> (ui-ux-spec §6). The short form only drops zeros: an amount that is not a whole number of
    /// thousands is written in full, so a price is never rounded.
    /// </summary>
    public static string FormatCompactMoney(long amountVnd, CultureInfo? culture = null)
    {
        var effective = culture ?? CultureInfo.CurrentCulture;
        if (!HasShortForm(amountVnd))
        {
            return FormatCurrency(amountVnd, effective);
        }

        return amountVnd >= Million
            ? Localized("RoomDetail_ShortMillionFormat", effective, (amountVnd / (decimal)Million).ToString("0.###", effective))
            : Localized("RoomDetail_ShortThousandFormat", effective, (amountVnd / Thousand).ToString(effective));
    }

    /// <summary><see cref="FormatCompactMoney"/> with the period: <c>2.5M/mo</c>, <c>2,5 tr/tháng</c>.</summary>
    public static string FormatMonthlyPrice(long amountVnd, CultureInfo? culture = null)
    {
        var effective = culture ?? CultureInfo.CurrentCulture;
        return Localized("RoomDetail_ShortPerMonthFormat", effective, FormatCompactMoney(amountVnd, effective));
    }

    /// <summary>
    /// Deposit shown under the price. English names the currency (<c>2.4M VND</c>); the Vietnamese "tr" and
    /// "k" read as đồng already, and a full amount carries <c>₫</c>.
    /// </summary>
    public static string FormatDepositMoney(long amountVnd, CultureInfo? culture = null)
    {
        if (amountVnd < 0)
        {
            return FallbackValue;
        }

        var effective = culture ?? CultureInfo.CurrentCulture;
        return HasShortForm(amountVnd)
            ? Localized("Price_ShortWithUnit", effective, FormatCompactMoney(amountVnd, effective))
            : Localized("Price_FullWithUnit", effective, amountVnd.ToString("N0", effective));
    }

    private static bool HasShortForm(long amountVnd) => amountVnd >= Thousand && amountVnd % Thousand == 0;

    private static string Localized(string key, CultureInfo culture, string value) =>
        string.Format(culture, AppResources.GetString(key, culture), value);

    /// <summary>
    /// Formats a general number using the provided culture (default <c>N2</c>).
    /// Does not parse numeric strings from unknown cultures.
    /// </summary>
    public static string FormatNumber(object? value, CultureInfo? culture = null, string? format = null)
    {
        var effectiveFormat = string.IsNullOrWhiteSpace(format) ? DefaultNumberFormat : format.Trim();
        if (!IsSupportedNumberFormat(effectiveFormat))
        {
            return FallbackValue;
        }

        var effective = culture ?? CultureInfo.CurrentCulture;

        try
        {
            if (value is decimal dec)
            {
                return NormalizeZero(dec).ToString(effectiveFormat, effective);
            }

            if (TryExtractDouble(value, out var number))
            {
                return NormalizeZero(number).ToString(effectiveFormat, effective);
            }
        }
        catch (FormatException)
        {
            return FallbackValue;
        }

        return FallbackValue;
    }

    /// <summary>
    /// Formats today and the previous seven calendar days relatively, and other dates absolutely.
    /// Preserves the calendar date supplied by the caller (BR-I18N-03).
    /// </summary>
    public static string FormatDate(object? value, CultureInfo? culture = null) =>
        FormatDate(value, culture, DateOnly.FromDateTime(DateTime.Today));

    internal static string FormatDate(object? value, CultureInfo? culture, DateOnly today)
    {
        if (!TryExtractDate(value, out var date))
        {
            return FallbackValue;
        }

        var effective = culture ?? CultureInfo.CurrentCulture;
        var daysAgo = today.DayNumber - date.DayNumber;
        if (daysAgo == 0)
            return AppResources.GetString("Date_Today", effective);
        if (daysAgo == 1)
            return AppResources.GetString("Date_Yesterday", effective);
        if (daysAgo is >= 2 and <= 7)
            return string.Format(effective, AppResources.GetString("Date_DaysAgo", effective), daysAgo);

        var pattern = IsVietnamese(effective) ? "dd'/'MM'/'yyyy" : "d MMM yyyy";
        return date.ToString(pattern, effective);
    }

    /// <summary>
    /// Formats a wall-clock time without timezone conversion:
    /// <c>vi-VN</c>: <c>HH:mm</c> · <c>en-US</c>: <c>h:mm tt</c>.
    /// </summary>
    public static string FormatTime(object? value, CultureInfo? culture = null)
    {
        if (!TryExtractTime(value, out var time))
        {
            return FallbackValue;
        }

        var effective = culture ?? CultureInfo.CurrentCulture;
        if (IsVietnamese(effective))
        {
            return time.ToString("HH':'mm", effective);
        }

        var designatorCulture = string.IsNullOrEmpty(effective.DateTimeFormat.AMDesignator) ||
                                string.IsNullOrEmpty(effective.DateTimeFormat.PMDesignator)
            ? EnglishCulture
            : effective;

        return time.ToString("h':'mm tt", designatorCulture);
    }

    /// <summary>
    /// Formats a non-negative area in square meters using <c>0.#</c> and <c>m²</c>.
    /// </summary>
    public static string FormatArea(object? value, CultureInfo? culture = null)
    {
        var effective = culture ?? CultureInfo.CurrentCulture;

        if (value is decimal dec)
        {
            if (dec < 0m)
            {
                return FallbackValue;
            }

            return $"{NormalizeZero(dec).ToString("0.#", effective)} m²";
        }

        if (!TryExtractDouble(value, out var area) || area < 0d)
        {
            return FallbackValue;
        }

        return $"{NormalizeZero(area).ToString("0.#", effective)} m²";
    }

    /// <summary>
    /// Formats a non-negative distance given in meters:
    /// below 1000 m uses <c>0.# m</c>; at or above 1000 m (including rounding to 1000 at 1 decimal) uses <c>0.# km</c>.
    /// </summary>
    public static string FormatDistance(object? value, CultureInfo? culture = null)
    {
        var effective = culture ?? CultureInfo.CurrentCulture;

        if (value is decimal dec)
        {
            if (dec < 0m)
            {
                return FallbackValue;
            }

            var normalized = NormalizeZero(dec);
            if (Math.Round(normalized, 1, MidpointRounding.AwayFromZero) >= 1000m)
            {
                var km = normalized / 1000m;
                return $"{km.ToString("0.#", effective)} km";
            }

            return $"{normalized.ToString("0.#", effective)} m";
        }

        if (!TryExtractDouble(value, out var meters) || meters < 0d)
        {
            return FallbackValue;
        }

        var normalizedMeters = NormalizeZero(meters);
        if (Math.Round(normalizedMeters, 1, MidpointRounding.AwayFromZero) >= 1000d)
        {
            var km = normalizedMeters / 1000d;
            return $"{km.ToString("0.#", effective)} km";
        }

        return $"{normalizedMeters.ToString("0.#", effective)} m";
    }

    private static bool TryExtractDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case decimal d:
                result = d;
                return true;
            case byte b:
                result = b;
                return true;
            case sbyte sb:
                result = sb;
                return true;
            case short s:
                result = s;
                return true;
            case ushort us:
                result = us;
                return true;
            case int i:
                result = i;
                return true;
            case uint ui:
                result = ui;
                return true;
            case long l:
                result = l;
                return true;
            case ulong ul:
                result = ul;
                return true;
            case double dbl when double.IsFinite(dbl)
                                 && dbl >= (double)decimal.MinValue
                                 && dbl <= (double)decimal.MaxValue:
                try
                {
                    result = Convert.ToDecimal(dbl);
                    return true;
                }
                catch (OverflowException)
                {
                    result = 0m;
                    return false;
                }
            case float flt when float.IsFinite(flt)
                                && flt >= (float)decimal.MinValue
                                && flt <= (float)decimal.MaxValue:
                try
                {
                    result = Convert.ToDecimal(flt);
                    return true;
                }
                catch (OverflowException)
                {
                    result = 0m;
                    return false;
                }
            default:
                result = 0m;
                return false;
        }
    }

    private static bool TryExtractDouble(object? value, out double result)
    {
        switch (value)
        {
            case double d when double.IsFinite(d):
                result = d;
                return true;
            case float f when float.IsFinite(f):
                result = f;
                return true;
            case decimal dec:
                result = (double)dec;
                return double.IsFinite(result);
            case byte b:
                result = b;
                return true;
            case sbyte sb:
                result = sb;
                return true;
            case short s:
                result = s;
                return true;
            case ushort us:
                result = us;
                return true;
            case int i:
                result = i;
                return true;
            case uint ui:
                result = ui;
                return true;
            case long l:
                result = l;
                return true;
            case ulong ul:
                result = ul;
                return true;
            default:
                result = 0d;
                return false;
        }
    }

    private static bool TryExtractDate(object? value, out DateOnly result)
    {
        switch (value)
        {
            case DateOnly dateOnly:
                result = dateOnly;
                return true;
            case DateTime dateTime:
                result = DateOnly.FromDateTime(dateTime);
                return true;
            case DateTimeOffset dateTimeOffset:
                result = DateOnly.FromDateTime(dateTimeOffset.DateTime);
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static bool TryExtractTime(object? value, out TimeOnly result)
    {
        switch (value)
        {
            case TimeOnly timeOnly:
                result = timeOnly;
                return true;
            case DateTime dateTime:
                result = TimeOnly.FromDateTime(dateTime);
                return true;
            case DateTimeOffset dateTimeOffset:
                result = TimeOnly.FromDateTime(dateTimeOffset.DateTime);
                return true;
            case TimeSpan timeSpan when timeSpan >= TimeSpan.Zero && timeSpan < TimeSpan.FromDays(1):
                result = TimeOnly.FromTimeSpan(timeSpan);
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static bool IsSupportedNumberFormat(string format)
    {
        if (format.Length is 1 or 2)
        {
            var specifier = char.ToUpperInvariant(format[0]);
            if (specifier is 'N' or 'F' or 'G' or 'P')
            {
                return format.Length == 1 || char.IsAsciiDigit(format[1]);
            }
        }

        var hasDigitPlaceholder = false;
        var hasDecimalPoint = false;
        foreach (var ch in format)
        {
            if (ch is '0' or '#')
            {
                hasDigitPlaceholder = true;
            }
            else if (ch == '.')
            {
                if (hasDecimalPoint)
                {
                    return false;
                }

                hasDecimalPoint = true;
            }
            else if (ch != ',')
            {
                return false;
            }
        }

        return hasDigitPlaceholder;
    }

    private static decimal NormalizeZero(decimal value) => value == 0m ? 0m : value;

    private static double NormalizeZero(double value) => value == 0d ? 0d : value;
}
