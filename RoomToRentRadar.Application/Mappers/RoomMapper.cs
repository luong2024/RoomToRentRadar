namespace RoomToRentRadar.Application.Mappers;

using System.Globalization;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Ports.Outbound.Repositories;

/// <summary>
/// Pure mapping functions between Domain aggregates/candidates and Response DTOs.
/// Adheres strictly to BR-LST-03 for utility estimates and BR-SRCH-03 for distance precision.
/// </summary>
public static class RoomMapper
{
    public static RoomDetailDto ToDetailDto(
        Room room,
        User? landlord,
        Campus campus,
        bool isPhoneVisible)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(campus);

        var distanceKm = HaversineDistanceCalculator.CalculateDistanceKm(
            room.Latitude, room.Longitude, campus.Latitude, campus.Longitude);

        // BR-LST-03: estimated monthly total = rent + 100 kWh × electricity price + water (4 m³ × price, or 1 × per-person price, or 0 if included) + internet + parking
        long totalEstimate = room.MonthlyRentVnd;
        if (room.ElectricityPriceVndPerKwh.HasValue)
        {
            totalEstimate += 100L * room.ElectricityPriceVndPerKwh.Value;
        }

        if (room.WaterPriceVnd.HasValue)
        {
            var unit = room.WaterPriceUnit?.Trim();
            if (string.Equals(unit, "PerPerson", StringComparison.OrdinalIgnoreCase))
            {
                var occupants = room.MaxOccupants.GetValueOrDefault(1);
                totalEstimate += occupants * room.WaterPriceVnd.Value;
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
            EstimatedMonthlyTotalVnd: totalEstimate);

        var phone = isPhoneVisible ? landlord?.PhoneNumber : null;
        var zaloUrl = isPhoneVisible && !string.IsNullOrWhiteSpace(landlord?.PhoneNumber)
            ? $"https://zalo.me/{landlord.PhoneNumber}"
            : null;

        var landlordContact = new LandlordContactDto(
            Id: landlord?.Id ?? room.LandlordId,
            DisplayName: landlord?.FullName ?? "Chủ nhà",
            IsVerified: landlord?.IsVerified ?? false,
            MemberSinceUtc: landlord?.CreatedAtUtc ?? room.CreatedAtUtc,
            PhoneNumber: phone,
            ZaloUrl: zaloUrl);

        var address = new AddressDto(
            AddressLine: room.AddressLine,
            Ward: room.Ward,
            District: string.IsNullOrWhiteSpace(room.District) ? "TP. Thái Nguyên" : room.District);

        var location = new GeoPointDto(
            Latitude: room.Latitude,
            Longitude: room.Longitude);

        var safety = new SafetyDto(
            Score: 0,
            IsVerified: room.IsSafetyVerified,
            Features: Array.Empty<string>());

        return new RoomDetailDto(
            Id: room.Id,
            Title: room.Title,
            Description: room.Description,
            RoomType: room.RoomType,
            Status: room.Status,
            IsVerified: room.VerifiedAtUtc.HasValue,
            MonthlyRentVnd: room.MonthlyRentVnd,
            DepositVnd: room.DepositVnd,
            AreaM2: room.AreaM2,
            Address: address,
            Location: location,
            DistanceKm: distanceKm,
            Utilities: utilities,
            Amenities: Array.Empty<Amenity>(),
            Safety: safety,
            Photos: Array.Empty<PhotoDto>(),
            Landlord: landlordContact,
            LastUpdatedAtUtc: room.LastUpdatedAtUtc,
            HouseRules: null,
            MaxOccupants: room.MaxOccupants,
            Furnishing: room.Furnishing,
            AvailableFrom: room.AvailableFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IsFavourite: false,
            PublishedAtUtc: room.PublishedAtUtc);
    }

    public static RoomCardDto ToCardDto(
        RoomSearchCandidate candidate,
        Campus campus)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(campus);

        var distanceKm = HaversineDistanceCalculator.CalculateDistanceKm(
            candidate.Latitude, candidate.Longitude, campus.Latitude, campus.Longitude);

        return new RoomCardDto(
            Id: candidate.Id,
            Title: candidate.Title,
            MonthlyRentVnd: candidate.MonthlyRentVnd,
            AreaM2: candidate.AreaM2,
            RoomType: candidate.RoomType,
            DistanceKm: distanceKm,
            IsVerified: candidate.IsVerified,
            Status: candidate.Status,
            CoverThumbUrl: candidate.CoverThumbUrl,
            Latitude: candidate.Latitude,
            Longitude: candidate.Longitude,
            PublishedAtUtc: candidate.PublishedAtUtc,
            IsFavourite: false);
    }
}
