using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;
using RoomToRentRadar.Api.Services.Mapping;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services;

public sealed class ListingDetailService(IListingRepository repository) : IListingDetailService
{
    public const string PhoneMaskSuffix = " (Sign in to view)";
    public const string UnavailableMessage =
        "Phòng này hiện không còn trống. Dưới đây là một số phòng tương tự đang Available.";

    /// <summary>Số phòng tương tự gợi ý khi phòng không còn trống.</summary>
    public const int SimilarRoomsCount = 3;

    public async Task<ListingDetailDto?> GetByIdAsync(
        string id,
        bool isAuthenticated,
        CancellationToken cancellationToken = default)
    {
        var listing = await repository.GetByIdAsync(id, cancellationToken);
        if (listing is null || !IsPubliclyVisible(listing))
        {
            return null;
        }

        if (listing.IsAvailable())
        {
            return MapDetail(listing, isAuthenticated, isAvailable: true, unavailableMessage: null, similarRooms: []);
        }

        var candidates = await repository.GetSimilarCandidatesAsync(listing.CampusId, listing.Id, cancellationToken);
        var similarRooms = PickTopSimilar(listing, candidates, SimilarRoomsCount);
        return MapDetail(listing, isAuthenticated, isAvailable: false, UnavailableMessage, similarRooms);
    }

    /// <summary>
    /// US08 AC2: tin chưa được duyệt (PendingReview / Rejected) hoặc đã xóa mềm không hiển thị công khai;
    /// chủ trọ xem tin của mình qua GET /api/v1/landlord/listings/{id}.
    /// </summary>
    private static bool IsPubliclyVisible(ListingDocument listing) =>
        string.Equals(listing.ModerationStatus, ListingModerationStatus.Approved, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(listing.Status, ListingAvailabilityStatus.Archived, StringComparison.OrdinalIgnoreCase);

    internal static IReadOnlyList<ListingCardDto> PickTopSimilar(
        ListingDocument source,
        IReadOnlyList<ListingDocument> candidates,
        int take)
    {
        return candidates
            .OrderBy(c => string.Equals(c.RoomType, source.RoomType, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(c => Math.Abs(c.Rent - source.Rent))
            .ThenBy(c => Math.Abs(c.Area - source.Area))
            .Take(take)
            .Select(ListingCardMapper.ToCard)
            .ToList();
    }

    public static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "***" + PhoneMaskSuffix;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 3)
        {
            return "***" + PhoneMaskSuffix;
        }

        return digits[..3] + "***" + PhoneMaskSuffix;
    }

    private static ListingDetailDto MapDetail(
        ListingDocument listing,
        bool isAuthenticated,
        bool isAvailable,
        string? unavailableMessage,
        IReadOnlyList<ListingCardDto> similarRooms)
    {
        var phone = isAuthenticated
            ? listing.Landlord.Phone
            : MaskPhone(listing.Landlord.Phone);

        return new ListingDetailDto(
            listing.Id,
            listing.CampusId,
            listing.Title,
            listing.Description,
            listing.Status,
            listing.IsVerified,
            isAvailable,
            unavailableMessage,
            listing.Rent,
            listing.Deposit,
            listing.Area,
            listing.RoomType,
            listing.Address,
            new GeoJsonPointDto(listing.Location.Type, listing.Location.Coordinates),
            listing.Amenities,
            listing.SecurityScore,
            listing.Photos
                .OrderBy(p => p.Order)
                .Select(p => new PhotoDto(p.Url, p.Is360, p.Order))
                .ToList(),
            new HouseRulesDto(
                listing.HouseRules.Version,
                listing.HouseRules.UpdatedAt,
                listing.HouseRules.QuietHours,
                listing.HouseRules.GuestPolicy,
                listing.HouseRules.CookingRules,
                listing.HouseRules.ParkingRules),
            new LandlordDto(listing.Landlord.Id, listing.Landlord.Name, phone),
            listing.UpdatedAt,
            similarRooms);
    }
}
