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
            return string.Format(
                CultureInfo.CurrentCulture,
                AppResources.GetString("Filters_Chip_RangeAmount"),
                FormatMoney(minRent.Value),
                FormatMoney(maxRent.Value));
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
        if (amount < 1_000_000)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                AppResources.GetString("Filters_PriceUnitThousand"),
                (amount / 1000.0).ToString("0.#", CultureInfo.CurrentCulture));
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            AppResources.GetString("Filters_PriceUnitMillion"),
            (amount / 1_000_000.0).ToString("0.#", CultureInfo.CurrentCulture));
    }

    private static string FormatNumber(double value) =>
        value.ToString("0.#", CultureInfo.CurrentCulture);
}
