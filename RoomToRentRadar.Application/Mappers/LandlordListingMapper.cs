using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Photos;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.Mappers;

/// <summary>
/// Maps the <see cref="Room"/> aggregate, its photos and its current house rules to the landlord
/// contracts (openapi <c>MyListingDetailDto</c> / <c>MyListingSummaryDto</c>). Photos are ordered by
/// <c>SortOrder</c> (0 = cover, BR-MED-05) and every storage key is expanded to a public URL through
/// <see cref="IPhotoStoragePort"/> so the API never leaks the on-disk layout.
/// </summary>
public static class LandlordListingMapper
{
    public static MyListingDetailDto ToDetail(
        Room room,
        IReadOnlyList<RoomPhoto> photos,
        IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(photos);
        ArgumentNullException.ThrowIfNull(storage);

        var orderedPhotos = OrderPhotos(photos);

        return new MyListingDetailDto(
            Id: room.Id,
            Status: room.Status,
            StatusReason: room.StatusReason,
            NeedsReReview: room.NeedsReReview,
            IsVerified: room.VerifiedAtUtc.HasValue,
            IsSafetyVerified: room.IsSafetyVerified,
            Version: room.Version,
            Listing: ToUpsertListing(room, orderedPhotos),
            Photos: orderedPhotos.Select(photo => ToPhotoDto(photo, storage)).ToArray(),
            HouseRules: ToHouseRulesDto(room.CurrentHouseRules),
            PublishedAtUtc: room.PublishedAtUtc,
            LastUpdatedAtUtc: room.LastUpdatedAtUtc,
            CreatedAtUtc: room.CreatedAtUtc);
    }

    public static MyListingSummaryDto ToSummary(OwnedRoomListing listing, IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(listing);
        ArgumentNullException.ThrowIfNull(storage);

        var room = listing.Room;
        return new MyListingSummaryDto(
            Id: room.Id,
            Title: room.Title,
            Status: room.Status,
            StatusReason: room.StatusReason,
            NeedsReReview: room.NeedsReReview,
            IsVerified: room.VerifiedAtUtc.HasValue,
            CoverThumbUrl: listing.CoverThumbPath is null ? null : storage.GetPublicUrl(listing.CoverThumbPath),
            MonthlyRentVnd: room.MonthlyRentVnd,
            LastUpdatedAtUtc: room.LastUpdatedAtUtc,
            CreatedAtUtc: room.CreatedAtUtc);
    }

    public static PhotoDto ToPhotoDto(RoomPhoto photo, IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(photo);
        ArgumentNullException.ThrowIfNull(storage);

        return new PhotoDto(
            Id: photo.Id,
            Kind: photo.Kind,
            ThumbUrl: storage.GetPublicUrl(photo.ThumbPath),
            MediumUrl: storage.GetPublicUrl(photo.MediumPath),
            OriginalUrl: storage.GetPublicUrl(photo.OriginalPath),
            Width: photo.Width,
            Height: photo.Height,
            SortOrder: photo.SortOrder);
    }

    /// <summary>Mirrors the <c>listing</c> property of <c>MyListingDetailDto</c> (openapi <c>UpsertListingRequest</c>).</summary>
    public static UpsertListingRequest ToUpsertListing(Room room, IReadOnlyList<RoomPhoto> orderedPhotos)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(orderedPhotos);

        return new UpsertListingRequest(
            Title: room.Title,
            Description: room.Description,
            RoomType: room.RoomType,
            MonthlyRentVnd: room.MonthlyRentVnd,
            DepositVnd: room.DepositVnd,
            AreaM2: room.AreaM2,
            MaxOccupants: room.MaxOccupants,
            Furnishing: ParseEnumName<Furnishing>(room.Furnishing),
            AvailableFrom: room.AvailableFrom,
            Address: new AddressDto(room.AddressLine, room.Ward, room.District),
            Location: new GeoPointDto(room.Latitude, room.Longitude),
            Utilities: new UtilitiesDto(
                room.ElectricityPriceVndPerKwh,
                room.WaterPriceVnd,
                ParseEnumName<WaterPriceUnit>(room.WaterPriceUnit),
                room.InternetFeeVnd,
                room.ParkingFeeVnd,
                room.OtherFeesNote),
            Amenities: room.Amenities,
            SafetyFeatures: room.SafetyFeatures.Select(entry => entry.Feature).ToArray(),
            PhotoIds: orderedPhotos.Select(photo => photo.Id).ToArray(),
            HouseRules: ToHouseRulesFields(room.CurrentHouseRules));
    }

    public static HouseRulesDto? ToHouseRulesDto(HouseRuleVersion? rules) =>
        rules is null
            ? null
            : new HouseRulesDto(
                QuietHoursStart: rules.QuietHoursStart,
                QuietHoursEnd: rules.QuietHoursEnd,
                CurfewTime: rules.CurfewTime,
                GuestPolicy: rules.GuestPolicy,
                GuestPolicyNote: rules.GuestPolicyNote,
                CookingPolicy: rules.CookingPolicy,
                ParkingAvailable: rules.ParkingAvailable,
                ParkingRules: rules.ParkingRules,
                PetsAllowed: rules.PetsAllowed,
                SmokingAllowed: rules.SmokingAllowed,
                OtherRules: rules.OtherRules,
                VersionNumber: rules.VersionNumber,
                UpdatedAtUtc: rules.CreatedAtUtc);

    public static HouseRulesFieldsDto? ToHouseRulesFields(HouseRuleVersion? rules) =>
        rules is null
            ? null
            : new HouseRulesFieldsDto(
                QuietHoursStart: rules.QuietHoursStart,
                QuietHoursEnd: rules.QuietHoursEnd,
                CurfewTime: rules.CurfewTime,
                GuestPolicy: rules.GuestPolicy,
                GuestPolicyNote: rules.GuestPolicyNote,
                CookingPolicy: rules.CookingPolicy,
                ParkingAvailable: rules.ParkingAvailable,
                ParkingRules: rules.ParkingRules,
                PetsAllowed: rules.PetsAllowed,
                SmokingAllowed: rules.SmokingAllowed,
                OtherRules: rules.OtherRules);

    private static IReadOnlyList<RoomPhoto> OrderPhotos(IReadOnlyList<RoomPhoto> photos) =>
        photos
            .OrderBy(photo => photo.SortOrder)
            .ThenBy(photo => photo.Id, StringComparer.Ordinal)
            .ToList();

    /// <summary>GUARDRAILS 1.5.1: only exact enum names are accepted, numbers are rejected.</summary>
    private static TEnum? ParseEnumName<TEnum>(string? value)
        where TEnum : struct, Enum =>
        value is not null
        && Enum.GetNames<TEnum>().Contains(value, StringComparer.Ordinal)
        && Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed)
            ? parsed
            : null;
}
