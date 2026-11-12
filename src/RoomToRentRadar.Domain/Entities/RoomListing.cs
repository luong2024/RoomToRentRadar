namespace RoomToRentRadar.Domain.Entities;

using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;

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
    public RoomStatus Status { get; private set; }
    public VerificationStatus VerificationStatus { get; private set; }
    public string CampusId { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public DateTime CreatedAtUtc { get; }

    /// <summary>
    /// Business rule (AC4): Only listings with status Available and Verified are displayed to students.
    /// </summary>
    public bool IsDisplayable => Status == RoomStatus.Available && VerificationStatus == VerificationStatus.Verified;

    public RoomListing(
        string id,
        string title,
        string description,
        Money monthlyRent,
        double areaSquareMeters,
        string address,
        GeoLocation location,
        RoomStatus status,
        VerificationStatus verificationStatus,
        string campusId,
        string? thumbnailUrl = null,
        DateTime? createdAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(campusId);

        if (areaSquareMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(areaSquareMeters), "Area must be greater than zero.");
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
    }

    public void UpdateStatus(RoomStatus newStatus)
    {
        Status = newStatus;
    }

    public void UpdateVerificationStatus(VerificationStatus newStatus)
    {
        VerificationStatus = newStatus;
    }
}
