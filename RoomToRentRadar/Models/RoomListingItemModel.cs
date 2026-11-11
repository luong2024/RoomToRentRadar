using System.Globalization;
using RoomToRentRadar.Models.Contracts;

namespace RoomToRentRadar.Models;

/// <summary>
/// Item Model bind trực tiếp lên RoomListPage (Compiled Bindings).
/// </summary>
public sealed class RoomListingItemModel
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    public required string Id { get; init; }
    public string CampusId { get; init; } = "ictu-main";
    public required string Title { get; init; }
    public decimal Rent { get; init; }
    public double Area { get; init; }
    public double DistanceMeters { get; init; }
    public string RoomType { get; init; } = "Phòng trọ";
    public string Address { get; init; } = string.Empty;
    public string ThumbnailUrl { get; init; } = string.Empty;
    public string Status { get; init; } = "Available";
    public bool IsVerified { get; init; }

    // Thuộc tính định dạng sẵn cho UI & AutomationId cho Appium (US01 AC2)
    public string FormattedRent => string.Format(ViCulture, "{0:N0} đ/tháng", Rent);
    public string FormattedArea => $"{Area:0.#} m²";
    public string FormattedDistance => DistanceMeters < 1000
        ? $"Cách ICTU {DistanceMeters:0} m"
        : $"Cách ICTU {DistanceMeters / 1000.0:0.1} km";

    public string CardAutomationId => $"RoomCard_{Id}";
    public string VerifiedBadgeAutomationId => $"VerifiedBadge_{Id}";

    public static RoomListingItemModel FromDto(ListingCardDto dto) => new()
    {
        Id = dto.Id,
        CampusId = dto.CampusId,
        Title = dto.Title,
        Rent = dto.Rent,
        Area = dto.Area,
        DistanceMeters = dto.DistanceMeters,
        RoomType = dto.RoomType,
        Address = dto.Address,
        ThumbnailUrl = dto.ThumbnailUrl,
        Status = dto.Status,
        IsVerified = dto.IsVerified
    };
}