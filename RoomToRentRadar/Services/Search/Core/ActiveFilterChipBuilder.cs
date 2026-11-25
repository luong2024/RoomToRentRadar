using System.Globalization;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Services.Search.Core;

/// <summary>
/// Builds the removable Explore chips (US04 AC3, UC-05 A1) from the active criteria.
/// All numbers follow <see cref="CultureInfo.CurrentCulture"/> (docs/ui-ux-spec.md §6); no hard-coded separators.
/// </summary>
public static class ActiveFilterChipBuilder
{
    public static IReadOnlyList<ActiveFilterChipModel> Build(RoomSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var chips = new List<ActiveFilterChipModel>();

        if (criteria.HasPriceFilter)
            chips.Add(Create(ActiveFilterChipKind.Price, FormatPrice(criteria.MinRentVnd, criteria.MaxRentVnd)));

        if (criteria.HasAreaFilter)
            chips.Add(Create(ActiveFilterChipKind.Area, FormatArea(criteria.MinAreaM2, criteria.MaxAreaM2)));

        foreach (var roomType in criteria.RoomTypes.OrderBy(t => t))
            chips.Add(Create(ActiveFilterChipKind.RoomType, AppResources.GetString($"RoomType_{roomType}"), roomType));

        if (criteria.MaxDistanceKm.HasValue)
        {
            var label = string.Format(
                CultureInfo.CurrentCulture,
                AppResources.GetString("Filters_Chip_MaxDistance"),
                FormatNumber(criteria.MaxDistanceKm.Value));
            chips.Add(Create(ActiveFilterChipKind.MaxDistance, label));
        }

        return chips;
    }

    private static ActiveFilterChipModel Create(ActiveFilterChipKind kind, string label, RoomType? roomType = null)
    {
        var removeLabel = string.Format(
            CultureInfo.CurrentCulture,
            AppResources.GetString("Explore_RemoveChipDescription"),
            label);
        return new ActiveFilterChipModel(kind, label, removeLabel, roomType);
    }

    private static string FormatPrice(long? minRent, long? maxRent)
    {
        if (minRent.HasValue && maxRent.HasValue)
        {
            var minUnit = UnitKey(minRent.Value);
            var maxUnit = UnitKey(maxRent.Value);
            var template = AppResources.GetString("Filters_Chip_RangeAmount");

            if (string.Equals(minUnit, maxUnit, StringComparison.Ordinal))
            {
                // "1.5M – 3M" (EN) / "1,5 – 3 tr" (VI) — the unit is placed by the template.
                return string.Format(
                    CultureInfo.CurrentCulture,
                    template,
                    FormatMoneyNumber(minRent.Value),
                    FormatMoneyNumber(maxRent.Value),
                    AppResources.GetString(minUnit));
            }

            // Mixed units (e.g. 950k – 1.5M): both bounds keep their own suffix.
            return string.Format(
                CultureInfo.CurrentCulture,
                template,
                FormatMoney(minRent.Value),
                FormatMoney(maxRent.Value),
                string.Empty).TrimEnd();
        }

        if (maxRent.HasValue)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                AppResources.GetString("Filters_Chip_UnderAmount"),
                FormatMoney(maxRent.Value));
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            AppResources.GetString("Filters_Chip_FromAmount"),
            FormatMoney(minRent!.Value));
    }

    private static string FormatArea(double? minArea, double? maxArea)
    {
        if (minArea.HasValue && maxArea.HasValue)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                AppResources.GetString("Filters_Chip_AreaRange"),
                FormatNumber(minArea.Value),
                FormatNumber(maxArea.Value));
        }

        if (maxArea.HasValue)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                AppResources.GetString("Filters_Chip_MaxArea"),
                FormatNumber(maxArea.Value));
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            AppResources.GetString("Filters_Chip_MinArea"),
            FormatNumber(minArea!.Value));
    }

    private static string FormatMoney(long amount)
    {
        var unitKey = UnitKey(amount);
        return string.Format(
            CultureInfo.CurrentCulture,
            AppResources.GetString(
                amount < 1_000_000 ? "Filters_PriceUnitThousand" : "Filters_PriceUnitMillion"),
            FormatMoneyNumber(amount));
    }

    private static string UnitKey(long amount) =>
        amount < 1_000_000 ? "Filters_UnitThousand" : "Filters_UnitMillion";

    private static string FormatMoneyNumber(long amount) =>
        (amount < 1_000_000 ? amount / 1000.0 : amount / 1_000_000.0)
            .ToString("0.#", CultureInfo.CurrentCulture);

    private static string FormatNumber(double value) =>
        value.ToString("0.#", CultureInfo.CurrentCulture);
}
