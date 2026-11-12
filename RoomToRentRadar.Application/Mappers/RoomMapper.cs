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
            Description: room.Description,
            Price: room.Price,
            Area: room.Area,
            Address: room.FullAddress,
            Latitude: room.Latitude,
            Longitude: room.Longitude,
            DistanceKm: room.DistanceToCampusKm,
            IsAvailable: room.IsAvailable,
            IsVerified: room.IsVerified,
            CreatedAtUtc: room.CreatedAtUtc
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

        var elec = (room.ElectricityPriceVndPerKwh ?? 3500m) * 100m;
        var water = (room.WaterPriceVnd ?? 25000m) * 4m;
        var internet = room.InternetFeeVnd ?? 100000m;
        var parking = room.ParkingFeeVnd ?? 0m;
        var estimatedMonthlyTotalVnd = (long)(room.MonthlyRentVnd + elec + water + internet + parking);

        var utilities = new UtilitiesView(
            ElectricityPriceVndPerKwh: room.ElectricityPriceVndPerKwh,
            WaterPriceVnd: room.WaterPriceVnd,
            WaterPriceUnit: room.WaterPriceUnit ?? "PerCubicMeter",
            InternetFeeVnd: room.InternetFeeVnd,
            ParkingFeeVnd: room.ParkingFeeVnd,
            OtherFeesNote: room.OtherFeesNote,
            EstimatedMonthlyTotalVnd: estimatedMonthlyTotalVnd
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
            DisplayName: !string.IsNullOrWhiteSpace(room.LandlordName) ? room.LandlordName : "Chủ phòng trọ",
            IsVerified: room.IsVerified,
            MemberSinceUtc: room.CreatedAtUtc,
            PhoneNumber: landlordPhoneNumber,
            ZaloUrl: !string.IsNullOrWhiteSpace(landlordPhoneNumber) ? $"https://zalo.me/{landlordPhoneNumber}" : null
        );

        var address = new AddressDto(
            AddressLine: !string.IsNullOrWhiteSpace(room.AddressLine) ? room.AddressLine : room.FullAddress,
            Ward: !string.IsNullOrWhiteSpace(room.Ward) ? room.Ward : "Quyết Thắng",
            District: !string.IsNullOrWhiteSpace(room.District) ? room.District : "TP. Thái Nguyên"
        );

        var location = new GeoPointDto(
            Latitude: room.Latitude,
            Longitude: room.Longitude
        );

        var safety = new SafetyDto(
            Score: room.IsSafetyVerified ? 4 : 2,
            IsVerified: room.IsSafetyVerified,
            Features: new[] { "Cctv", "SecureEntrance" }
        );

        var houseRules = new HouseRulesDto(
            VersionNumber: 1,
            UpdatedAtUtc: room.LastUpdatedAtUtc,
            GuestPolicy: "Allowed",
            CookingPolicy: "Allowed",
            ParkingAvailable: true
        );

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

    [Obsolete("Use ListingStatus overload")]
    public static string MapRoomStatus(RoomStatus status) => status switch
    {
        RoomStatus.Available => "Available",
        RoomStatus.Unavailable => "Unavailable",
        RoomStatus.Rented => "Rented",
        RoomStatus.PendingVerification => "PendingVerification",
        _ => "Unavailable"
    };
}
