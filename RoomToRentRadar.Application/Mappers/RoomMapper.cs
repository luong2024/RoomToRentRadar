using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadar.Application.Mappers;

public static class RoomMapper
{
    public static RoomCardDto ToCardDto(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        return new RoomCardDto(
            Id: room.Id,
            Title: room.Title,
            MonthlyRentVnd: (long)room.MonthlyRentVnd,
            AreaM2: room.AreaM2,
            RoomType: room.RoomType,
            DistanceKm: room.DistanceToCampusKm,
            IsVerified: room.IsVerified,
            Status: room.Status,
            Latitude: room.Latitude,
            Longitude: room.Longitude,
            CoverThumbUrl: room.PhotoUrls?.FirstOrDefault(),
            IsFavourite: false,
            PublishedAtUtc: room.PublishedAtUtc
        );
    }


    public static RoomDetailDto ToDetailDto(
        Room room,
        string? landlordPhoneNumber,
        double? distanceKm = null,
        string? unavailableMessage = null,
        IReadOnlyList<RoomCardDto>? similarRooms = null)
    {
        ArgumentNullException.ThrowIfNull(room);

        // BR-LST-03: estimated monthly total = rent + 100 kWh × electricity price + water (4 m³ × price, or 1 × per-person price, or 0 if included) + internet + parking
        long totalEstimate = (long)room.MonthlyRentVnd;
        if (room.ElectricityPriceVndPerKwh.HasValue)
        {
            totalEstimate += 100L * room.ElectricityPriceVndPerKwh.Value;
        }

        if (room.WaterPriceVnd.HasValue)
        {
            var unit = room.WaterPriceUnit?.Trim();
            if (string.Equals(unit, "PerPerson", StringComparison.OrdinalIgnoreCase))
            {
                totalEstimate += 1L * room.WaterPriceVnd.Value;
            }
            else if (string.Equals(unit, "Included", StringComparison.OrdinalIgnoreCase))
            {
                totalEstimate += 0L;
            }
            else // PerCubicMeter or default per BR-LST-03: 4 m³
            {
                totalEstimate += 4L * room.WaterPriceVnd.Value;
            }
        }

        if (room.InternetFeeVnd.HasValue)
        {
            totalEstimate += room.InternetFeeVnd.Value;
        }

        if (room.ParkingFeeVnd.HasValue)
        {
            totalEstimate += room.ParkingFeeVnd.Value;
        }

        var utilities = new UtilitiesView(
            ElectricityPriceVndPerKwh: room.ElectricityPriceVndPerKwh,
            WaterPriceVnd: room.WaterPriceVnd,
            WaterPriceUnit: room.WaterPriceUnit,
            InternetFeeVnd: room.InternetFeeVnd,
            ParkingFeeVnd: room.ParkingFeeVnd,
            OtherFeesNote: room.OtherFeesNote,
            EstimatedMonthlyTotalVnd: totalEstimate
        );

        var amenities = (room.Amenities ?? Array.Empty<string>())
            .Select(a => Enum.TryParse<Amenity>(a, true, out var parsed) ? (Amenity?)parsed : null)
            .Where(a => a.HasValue)
            .Select(a => a!.Value)
            .ToList();

        var photos = (room.PhotoUrls ?? Array.Empty<string>())
            .Select((url, idx) => new PhotoDto(
                Id: $"photo-{idx + 1}",
                Kind: "Standard",
                ThumbUrl: url,
                MediumUrl: url,
                OriginalUrl: url,
                SortOrder: idx
            ))
            .ToList();

        if (!string.IsNullOrWhiteSpace(room.Panorama360Url))
        {
            photos.Add(new PhotoDto(
                Id: "photo-panorama",
                Kind: "Panorama",
                ThumbUrl: room.Panorama360Url,
                MediumUrl: room.Panorama360Url,
                OriginalUrl: room.Panorama360Url,
                SortOrder: photos.Count
            ));
        }

        var landlord = new LandlordContactDto(
            Id: !string.IsNullOrWhiteSpace(room.LandlordId) ? room.LandlordId : "landlord-default",
            DisplayName: !string.IsNullOrWhiteSpace(room.LandlordName) ? room.LandlordName : room.LandlordId,
            IsVerified: room.LandlordIsVerified,
            MemberSinceUtc: room.LandlordMemberSinceUtc ?? room.CreatedAtUtc,
            PhoneNumber: landlordPhoneNumber,
            ZaloUrl: !string.IsNullOrWhiteSpace(landlordPhoneNumber) ? $"https://zalo.me/{landlordPhoneNumber}" : null
        );

        var address = new AddressDto(
            AddressLine: !string.IsNullOrWhiteSpace(room.AddressLine) ? room.AddressLine : room.FullAddress,
            Ward: room.Ward,
            District: !string.IsNullOrWhiteSpace(room.District) ? room.District : "TP. Thái Nguyên"
        );

        var location = new GeoPointDto(
            Latitude: room.Latitude,
            Longitude: room.Longitude
        );

        // BR-LST-08: Safety score computed from verified criteria; empty array if none confirmed
        var safetyFeatures = Array.Empty<string>();
        var safety = new SafetyDto(
            Score: safetyFeatures.Length,
            IsVerified: room.IsSafetyVerified,
            Features: safetyFeatures
        );

        HouseRulesDto? houseRules = null;
        if (room.HouseRules != null && room.HouseRules.Count > 0)
        {
            houseRules = new HouseRulesDto(
                VersionNumber: 1,
                UpdatedAtUtc: room.LastUpdatedAtUtc,
                GuestPolicy: "Allowed",
                CookingPolicy: "Allowed",
                ParkingAvailable: true,
                GuestPolicyNote: string.Join("; ", room.HouseRules)
            );
        }

        return new RoomDetailDto(
            Id: room.Id,
            Title: room.Title,
            Description: room.Description,
            RoomType: room.RoomType,
            Status: room.Status,
            IsVerified: room.IsVerified,
            MonthlyRentVnd: (long)room.MonthlyRentVnd,
            DepositVnd: (long)room.DepositVnd,
            AreaM2: room.AreaM2,
            Address: address,
            Location: location,
            DistanceKm: distanceKm ?? room.DistanceToCampusKm,
            Utilities: utilities,
            Amenities: amenities,
            Safety: safety,
            Photos: photos,
            Landlord: landlord,
            LastUpdatedAtUtc: room.LastUpdatedAtUtc,
            HouseRules: houseRules
        );
    }

    public static string MapRoomStatus(ListingStatus status) => status switch
    {
        ListingStatus.Available => "Available",
        ListingStatus.Rented => "Rented",
        ListingStatus.Unpublished => "Unavailable",
        ListingStatus.PendingReview => "PendingVerification",
        ListingStatus.Rejected => "Unavailable",
        ListingStatus.Suspended => "Unavailable",
        _ => "Unavailable"
    };
}
