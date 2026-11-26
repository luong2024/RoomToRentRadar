using System.Globalization;
using Fonts;
using RoomToRentRadar.Converters;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Pages.Controls.RoomDetail.Models;

/// <summary>
/// Pure presentation mapping from <see cref="RoomDetailDto"/> and <see cref="TravelTimesDto"/>
/// to display models for SCR-ROOM-01 (SRP/OCP extraction from <c>RoomDetailPageModel</c>).
/// </summary>
public static class RoomDetailSectionMapper
{
    private static readonly IReadOnlyDictionary<Amenity, (string ResourceKey, string IconGlyph)> AmenityMap =
        new Dictionary<Amenity, (string ResourceKey, string IconGlyph)>
        {
            [Amenity.AirConditioner] = ("Amenity_AirConditioner", FluentUI.weather_snowflake_24_regular),
            [Amenity.WaterHeater] = ("Amenity_WaterHeater", FluentUI.drop_24_regular),
            [Amenity.PrivateBathroom] = ("Amenity_PrivateBathroom", FluentUI.drop_24_regular),
            [Amenity.Wifi] = ("Amenity_Wifi", FluentUI.wifi_1_24_regular),
            [Amenity.Bed] = ("Amenity_Bed", FluentUI.bed_24_regular),
            [Amenity.Fridge] = ("Amenity_Fridge", FluentUI.food_24_regular),
            [Amenity.MotorbikeParking] = ("Amenity_MotorbikeParking", FluentUI.vehicle_bicycle_24_regular),
            [Amenity.Wardrobe] = ("Amenity_Wardrobe", FluentUI.desk_24_regular),
            [Amenity.Desk] = ("Amenity_Desk", FluentUI.desk_24_regular),
            [Amenity.Balcony] = ("Amenity_Balcony", FluentUI.window_24_regular),
            [Amenity.Window] = ("Amenity_Window", FluentUI.window_24_regular),
            [Amenity.WashingMachine] = ("Amenity_WashingMachine", FluentUI.drop_24_regular),
            [Amenity.Kitchen] = ("Amenity_Kitchen", FluentUI.food_24_regular),
            [Amenity.Elevator] = ("Amenity_Elevator", FluentUI.arrow_sort_up_24_regular),
            [Amenity.DryingArea] = ("Amenity_DryingArea", FluentUI.weather_sunny_high_24_regular)
        };

    private static readonly IReadOnlyList<(SafetyFeature Feature, string ResourceKey)> SafetyFeatureDefinitions =
    [
        (SafetyFeature.Cctv, "Safety_Cctv"),
        (SafetyFeature.SecureEntrance, "Safety_SecureEntrance"),
        (SafetyFeature.FireSafety, "Safety_FireExtinguisher"),
        (SafetyFeature.OwnerOrGuardOnSite, "Safety_OwnerLivesOnSite"),
        (SafetyFeature.WellLitAccess, "Safety_WellLitAccessPath")
    ];

    public static string FormatRoomType(RoomType roomType) => roomType switch
    {
        RoomType.BoardingRoom => AppResources.GetString("RoomType_BoardingRoom"),
        RoomType.MiniApartment => AppResources.GetString("RoomType_MiniApartment"),
        RoomType.WholeHouse => AppResources.GetString("RoomType_WholeHouse"),
        RoomType.Dormitory => AppResources.GetString("RoomType_Dormitory"),
        _ => roomType.ToString()
    };

    public static string ExtractInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "LL";
        }

        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        }

        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }

    public static IReadOnlyList<KeyFactItem> BuildKeyFacts(RoomDetailDto detail, string campusName, CultureInfo culture)
    {
        string occupantsValue = detail.MaxOccupants is > 0
            ? string.Format(AppResources.GetString("RoomDetail_PeopleCount"), detail.MaxOccupants)
            : AppResources.GetString("RoomDetail_OnePerson");

        string furnishingString = detail.Furnishing switch
        {
            Furnishing.Full => AppResources.GetString("Furnishing_Full"),
            Furnishing.Basic => AppResources.GetString("Furnishing_Partial"),
            _ => AppResources.GetString("Furnishing_None")
        };

        string availableFromText = detail.AvailableFrom.HasValue
            ? CultureFormatting.FormatDate(detail.AvailableFrom.Value, culture)
            : AppResources.GetString("RoomDetail_StatusAvailable");

        return
        [
            new KeyFactItem(
                FluentUI.ruler_24_regular,
                AppResources.GetString("RoomDetail_Area"),
                CultureFormatting.FormatArea(detail.AreaM2, culture)),
            new KeyFactItem(
                FluentUI.home_24_regular,
                AppResources.GetString("RoomDetail_RoomType"),
                FormatRoomType(detail.RoomType)),
            new KeyFactItem(
                FluentUI.people_24_regular,
                AppResources.GetString("RoomDetail_MaxOccupants"),
                occupantsValue),
            new KeyFactItem(
                FluentUI.bed_24_regular,
                AppResources.GetString("RoomDetail_Furnishing"),
                furnishingString),
            new KeyFactItem(
                FluentUI.calendar_ltr_24_regular,
                AppResources.GetString("RoomDetail_AvailableFrom"),
                availableFromText),
            new KeyFactItem(
                FluentUI.location_24_regular,
                string.Format(culture, AppResources.GetString("RoomDetail_ToCampus"), campusName),
                $"{detail.DistanceKm.ToString("0.0", culture)} km")
        ];
    }

    public static IReadOnlyList<KeyFactRowItem> GroupKeyFactRows(IReadOnlyList<KeyFactItem> facts)
    {
        var rows = new List<KeyFactRowItem>((facts.Count + 2) / 3);
        for (int i = 0; i < facts.Count; i += 3)
        {
            var first = facts[i];
            var second = i + 1 < facts.Count ? facts[i + 1] : null;
            var third = i + 2 < facts.Count ? facts[i + 2] : null;
            rows.Add(new KeyFactRowItem(first, second, third));
        }

        return rows;
    }

    public static IReadOnlyList<CostRowItem> BuildCostBreakdownRows(RoomDetailDto detail, CultureInfo culture)
    {
        var utils = detail.Utilities;
        string fullMoney(long amount) => CultureFormatting.FormatCurrency(amount, culture);

        string elecValue = utils.ElectricityPriceVndPerKwh.HasValue
            ? string.Format(AppResources.GetString("RoomDetail_PerKwh"), fullMoney(utils.ElectricityPriceVndPerKwh.Value))
            : AppResources.GetString("RoomDetail_Included");

        string waterValue;
        if (!utils.WaterPriceVnd.HasValue || utils.WaterPriceUnit == WaterPriceUnit.Included)
        {
            waterValue = AppResources.GetString("RoomDetail_Included");
        }
        else if (utils.WaterPriceUnit == WaterPriceUnit.PerPerson)
        {
            waterValue = string.Format(AppResources.GetString("RoomDetail_PerPerson"), fullMoney(utils.WaterPriceVnd.Value));
        }
        else
        {
            waterValue = string.Format(AppResources.GetString("RoomDetail_PerM3"), fullMoney(utils.WaterPriceVnd.Value));
        }

        string internetValue = utils.InternetFeeVnd.HasValue && utils.InternetFeeVnd.Value > 0
            ? fullMoney(utils.InternetFeeVnd.Value)
            : AppResources.GetString("RoomDetail_Included");

        string parkingValue = utils.ParkingFeeVnd.HasValue && utils.ParkingFeeVnd.Value > 0
            ? fullMoney(utils.ParkingFeeVnd.Value)
            : AppResources.GetString("RoomDetail_Free");

        return
        [
            new CostRowItem(AppResources.GetString("RoomDetail_Rent"), fullMoney(detail.MonthlyRentVnd)),
            new CostRowItem(AppResources.GetString("RoomDetail_Electricity"), elecValue),
            new CostRowItem(AppResources.GetString("RoomDetail_Water"), waterValue),
            new CostRowItem(AppResources.GetString("RoomDetail_Internet"), internetValue),
            new CostRowItem(AppResources.GetString("RoomDetail_MotorbikeParking"), parkingValue)
        ];
    }

    public static IReadOnlyList<AmenityDisplayItem> BuildAmenities(IReadOnlyList<Amenity> amenities) =>
        amenities.Select(CreateAmenityItem).ToList();

    public static IReadOnlyList<AmenityRowItem> GroupAmenityRows(IReadOnlyList<AmenityDisplayItem> items)
    {
        var rows = new List<AmenityRowItem>((items.Count + 1) / 2);
        for (int i = 0; i < items.Count; i += 2)
        {
            var left = items[i];
            var right = i + 1 < items.Count ? items[i + 1] : null;
            rows.Add(new AmenityRowItem(left, right));
        }

        return rows;
    }

    private static AmenityDisplayItem CreateAmenityItem(Amenity amenity) =>
        AmenityMap.TryGetValue(amenity, out var metadata)
            ? new AmenityDisplayItem(amenity, AppResources.GetString(metadata.ResourceKey), metadata.IconGlyph)
            : new AmenityDisplayItem(amenity, amenity.ToString(), FluentUI.home_24_regular);

    public static (string ScoreFormatted, IReadOnlyList<SafetyCheckItem> Items) BuildSafetyChecklist(SafetyDto safety)
    {
        int score = safety.Score <= 0
            ? 0
            : (safety.Score <= 5 ? safety.Score : Math.Clamp((int)Math.Round(safety.Score / 20.0), 0, 5));

        string scoreFormat = safety.IsVerified
            ? AppResources.GetString("RoomDetail_SafetyScoreVerified")
            : AppResources.GetString("RoomDetail_SafetyScoreUnverified");

        var presentFeatures = safety.Features.ToHashSet();
        var items = new List<SafetyCheckItem>(SafetyFeatureDefinitions.Count);
        foreach (var (feature, resourceKey) in SafetyFeatureDefinitions)
        {
            bool isPresent = presentFeatures.Contains(feature);
            items.Add(new SafetyCheckItem(
                feature,
                AppResources.GetString(resourceKey),
                isPresent,
                isPresent ? FluentUI.checkmark_16_regular : FluentUI.dismiss_16_regular));
        }

        return (string.Format(scoreFormat, score), items);
    }

    public static IReadOnlyList<HouseRuleItem> BuildHouseRules(HouseRulesDto? rules)
    {
        if (rules == null)
        {
            return Array.Empty<HouseRuleItem>();
        }

        var list = new List<HouseRuleItem>(8);

        if (!string.IsNullOrWhiteSpace(rules.QuietHoursStart) && !string.IsNullOrWhiteSpace(rules.QuietHoursEnd))
        {
            list.Add(new HouseRuleItem(
                FluentUI.weather_moon_20_regular,
                AppResources.GetString("HouseRule_QuietHours"),
                $"{rules.QuietHoursStart} – {rules.QuietHoursEnd}"));
        }

        if (!string.IsNullOrWhiteSpace(rules.CurfewTime))
        {
            list.Add(new HouseRuleItem(
                FluentUI.clock_20_regular,
                AppResources.GetString("HouseRule_Curfew"),
                string.Format(AppResources.GetString("HouseRule_CurfewGateCloses"), rules.CurfewTime)));
        }

        string guestDetail = rules.GuestPolicy switch
        {
            GuestPolicy.DaytimeOnly => AppResources.GetString("HouseRule_GuestsDaytimeOnly"),
            GuestPolicy.Allowed => AppResources.GetString("HouseRule_GuestsAllowed"),
            _ => AppResources.GetString("HouseRule_GuestsNotAllowed")
        };
        if (!string.IsNullOrWhiteSpace(rules.GuestPolicyNote))
        {
            guestDetail = $"{guestDetail} · {rules.GuestPolicyNote.Trim()}";
        }

        list.Add(new HouseRuleItem(
            FluentUI.people_20_regular,
            AppResources.GetString("HouseRule_Guests"),
            guestDetail));

        string cookingDetail = rules.CookingPolicy switch
        {
            CookingPolicy.SharedKitchenOnly => AppResources.GetString("HouseRule_CookingShared"),
            CookingPolicy.Allowed => AppResources.GetString("HouseRule_CookingInRoom"),
            _ => AppResources.GetString("HouseRule_CookingNotAllowed")
        };

        list.Add(new HouseRuleItem(
            FluentUI.food_20_regular,
            AppResources.GetString("HouseRule_Cooking"),
            cookingDetail));

        string parkingDetail = !rules.ParkingAvailable
            ? AppResources.GetString("HouseRule_ParkingUnavailable")
            : (!string.IsNullOrWhiteSpace(rules.ParkingRules)
                ? rules.ParkingRules.Trim()
                : AppResources.GetString("HouseRule_ParkingYard"));

        list.Add(new HouseRuleItem(
            FluentUI.vehicle_car_20_regular,
            AppResources.GetString("HouseRule_Parking"),
            parkingDetail));

        list.Add(new HouseRuleItem(
            FluentUI.animal_cat_20_regular,
            AppResources.GetString("HouseRule_Pets"),
            AppResources.GetString(rules.PetsAllowed ? "HouseRule_PetsAllowed" : "HouseRule_PetsNotAllowed")));

        list.Add(new HouseRuleItem(
            FluentUI.prohibited_20_regular,
            AppResources.GetString("HouseRule_Smoking"),
            AppResources.GetString(rules.SmokingAllowed ? "HouseRule_SmokingAllowed" : "HouseRule_SmokingNotAllowed")));

        if (!string.IsNullOrWhiteSpace(rules.OtherRules))
        {
            list.Add(new HouseRuleItem(
                FluentUI.document_endnote_20_regular,
                AppResources.GetString("HouseRule_OtherRules"),
                rules.OtherRules.Trim()));
        }

        return list;
    }

    public static IReadOnlyList<CommuteModeItem> BuildCommuteItems(IReadOnlyList<TravelTimeItemDto> items)
    {
        var result = new List<CommuteModeItem>(items.Count);
        foreach (var item in items)
        {
            int mins = (int)Math.Max(1, Math.Round(item.DurationSeconds / 60.0));
            string timeFormatted = item.IsEstimate
                ? string.Format(AppResources.GetString("RoomDetail_MinutesEst"), mins)
                : string.Format(AppResources.GetString("RoomDetail_Minutes"), mins);

            var commuteItem = item.Mode switch
            {
                TravelMode.Motorbike => new CommuteModeItem(item.Mode, FluentUI.vehicle_motorcycle_24_regular, timeFormatted, AppResources.GetString("TravelMode_Motorbike")),
                TravelMode.Bus => new CommuteModeItem(item.Mode, FluentUI.vehicle_bus_24_regular, timeFormatted, AppResources.GetString("TravelMode_Bus")),
                _ => new CommuteModeItem(item.Mode, FluentUI.person_walking_24_regular, timeFormatted, AppResources.GetString("TravelMode_Walking"))
            };
            result.Add(commuteItem);
        }

        return result;
    }
}
