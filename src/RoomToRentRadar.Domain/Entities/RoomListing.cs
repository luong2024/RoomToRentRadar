namespace RoomToRentRadar.Domain.Entities;

using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;

/// <summary>
/// Represents a rental room listing in the system.
/// </summary>
public sealed class RoomListing
{
    public string Id { get; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public Money MonthlyRent { get; private set; }
    public double AreaSquareMeters { get; private set; }
    public string Address { get; private set; }
    public GeoLocation Location { get; private set; }
    public ListingRoomStatus Status { get; private set; }
    public VerificationStatus VerificationStatus { get; private set; }
    public string CampusId { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public DateTime CreatedAtUtc { get; }
    public string RoomType { get; private set; }
    public IReadOnlyList<string> Amenities { get; private set; }

    /// <summary>
    /// Business rule (AC4): Only listings with status Available and Verified are displayed to students.
    /// </summary>
    public bool IsDisplayable => Status == ListingRoomStatus.Available && VerificationStatus == VerificationStatus.Verified;

    public RoomListing(
        string id,
        string title,
        string description,
        Money monthlyRent,
        double areaSquareMeters,
        string address,
        GeoLocation location,
        ListingRoomStatus status,
        VerificationStatus verificationStatus,
        string campusId,
        string? thumbnailUrl = null,
        DateTime? createdAtUtc = null,
        string? roomType = null,
        IEnumerable<string>? amenities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(campusId);

        if (!double.IsFinite(areaSquareMeters) || areaSquareMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(areaSquareMeters), "Area must be a finite number greater than zero.");
        }

        Id = id;
        Title = title;
        Description = description ?? string.Empty;
        MonthlyRent = monthlyRent;
        AreaSquareMeters = areaSquareMeters;
        Address = address;
        Location = location;
        Status = status;
        VerificationStatus = verificationStatus;
        CampusId = campusId;
        ThumbnailUrl = thumbnailUrl;
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
        RoomType = string.IsNullOrWhiteSpace(roomType) ? "private" : roomType.Trim();
        Amenities = amenities?
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim())
            .ToArray() ?? [];
    }

    public void UpdateStatus(ListingRoomStatus newStatus)
    {
        Status = newStatus;
    }

    public void UpdateVerificationStatus(VerificationStatus newStatus)
    {
        VerificationStatus = newStatus;
    }
}
