using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Entities;

public sealed class Room
{
    public string Id { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal MonthlyRent { get; private set; }
    public decimal DepositAmount { get; private set; }
    public double AreaSquareMeters { get; private set; }
    public string RoomType { get; private set; } = string.Empty;
    public IReadOnlyList<string> Amenities { get; private set; } = Array.Empty<string>();
    public string FullAddress { get; private set; } = string.Empty;
    public string LandlordName { get; private set; } = string.Empty;
    public string LandlordPhoneNumber { get; private set; } = string.Empty;
    public IReadOnlyList<string> PhotoUrls { get; private set; } = Array.Empty<string>();
    public string? Panorama360Url { get; private set; }
    public IReadOnlyList<string> HouseRules { get; private set; } = Array.Empty<string>();
    public RoomStatus Status { get; private set; } = RoomStatus.Available;
    public DateTime LastUpdatedAt { get; private set; } = DateTime.UtcNow;
    public bool IsVerified { get; private set; }
    public double DistanceToCampusKm { get; private set; }

    public bool IsAvailable => Status == RoomStatus.Available;

    private Room() { }

    public Room(
        string id,
        string title,
        string description,
        decimal monthlyRent,
        decimal depositAmount,
        double areaSquareMeters,
        string roomType,
        IReadOnlyList<string>? amenities = null,
        string? fullAddress = null,
        string? landlordName = null,
        string? landlordPhoneNumber = null,
        IReadOnlyList<string>? photoUrls = null,
        string? panorama360Url = null,
        IReadOnlyList<string>? houseRules = null,
        RoomStatus status = RoomStatus.Available,
        DateTime? lastUpdatedAt = null,
        bool isVerified = false,
        double distanceToCampusKm = 0.0)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DomainValidationException("Room ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainValidationException("Room title cannot be empty.");
        }

        if (monthlyRent < 0)
        {
            throw new DomainValidationException("Monthly rent cannot be negative.");
        }

        if (depositAmount < 0)
        {
            throw new DomainValidationException("Deposit amount cannot be negative.");
        }

        if (areaSquareMeters < 0)
        {
            throw new DomainValidationException("Area square meters cannot be negative.");
        }

        Id = id.Trim();
        Title = title.Trim();
        Description = description?.Trim() ?? string.Empty;
        MonthlyRent = monthlyRent;
        DepositAmount = depositAmount;
        AreaSquareMeters = areaSquareMeters;
        RoomType = roomType?.Trim() ?? string.Empty;
        Amenities = amenities ?? Array.Empty<string>();
        FullAddress = fullAddress?.Trim() ?? string.Empty;
        LandlordName = landlordName?.Trim() ?? string.Empty;
        LandlordPhoneNumber = landlordPhoneNumber?.Trim() ?? string.Empty;
        PhotoUrls = photoUrls ?? Array.Empty<string>();
        Panorama360Url = panorama360Url;
        HouseRules = houseRules ?? Array.Empty<string>();
        Status = status;
        LastUpdatedAt = lastUpdatedAt ?? DateTime.UtcNow;
        IsVerified = isVerified;
        DistanceToCampusKm = distanceToCampusKm;
    }
}
