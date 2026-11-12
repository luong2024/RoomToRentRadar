using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Aggregates;

/// <summary>
/// Authoritative Room aggregate per docs/erd.md §4.4, ADR-011, and docs/business-analysis.md BR-LST-04..10.
/// Single aggregate root shared across search, detail, and lifecycle moderation.
/// </summary>
public sealed class Room
{
    public string Id { get; private set; } = string.Empty;
    public string LandlordId { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public RoomType RoomType { get; private set; } = RoomType.BoardingRoom;
    public decimal MonthlyRentVnd { get; private set; }
    public decimal DepositVnd { get; private set; }
    public double AreaM2 { get; private set; }
    public int? MaxOccupants { get; private set; }
    public string? Furnishing { get; private set; }
    public string? AvailableFrom { get; private set; }
    public string AddressLine { get; private set; } = string.Empty;
    public string Ward { get; private set; } = string.Empty;
    public string District { get; private set; } = "TP. Thái Nguyên";
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public decimal? ElectricityPriceVndPerKwh { get; private set; }
    public decimal? WaterPriceVnd { get; private set; }
    public string? WaterPriceUnit { get; private set; }
    public decimal? InternetFeeVnd { get; private set; }
    public decimal? ParkingFeeVnd { get; private set; }
    public string? OtherFeesNote { get; private set; }
    public ListingStatus Status { get; private set; } = ListingStatus.PendingReview;
    public string? StatusReason { get; private set; }
    public bool NeedsReReview { get; private set; }
    public bool IsSafetyVerified { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public string? VerifiedByUserId { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime LastUpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public int Version { get; private set; } = 1;
    public string ConcurrencyToken { get; private set; } = "1";

    private readonly string? _customRoomType;
    public string RoomTypeDisplay => !string.IsNullOrWhiteSpace(_customRoomType) ? _customRoomType : RoomType.ToString();

    // Compatibility & Convenience Accessors
    public decimal Price => MonthlyRentVnd;
    public decimal MonthlyRent => MonthlyRentVnd;
    public decimal DepositAmount => DepositVnd;
    public double Area => AreaM2;
    public double AreaSquareMeters => AreaM2;
    public string Address => AddressLine;
    public DateTime LastUpdatedAt => LastUpdatedAtUtc;
    public string FullAddress => string.IsNullOrWhiteSpace(Ward)
        ? AddressLine
        : $"{AddressLine}, {Ward}, {District}".Trim(',', ' ');

    public bool IsAvailable => Status == ListingStatus.Available;
    public bool IsVerified => IsSafetyVerified || VerifiedAtUtc.HasValue;
    public bool IsPendingVerification => Status == ListingStatus.PendingReview;

    /// <summary>
    /// BR-LST-04a: Available, Rented, and Unpublished listings are viewable on detail page.
    /// PendingReview, Rejected, and Suspended return 404 to public users.
    /// </summary>
    public bool IsPubliclyVisible =>
        Status == ListingStatus.Available ||
        Status == ListingStatus.Rented ||
        Status == ListingStatus.Unpublished;

    // Phase 1 Transitional properties for detail views until Phase 2/3 extracts them to respective read models
    public IReadOnlyList<string> Amenities { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> PhotoUrls { get; private set; } = Array.Empty<string>();
    public string? Panorama360Url { get; private set; }
    public IReadOnlyList<string> HouseRules { get; private set; } = Array.Empty<string>();
    public string LandlordName { get; private set; } = string.Empty;
    public string LandlordPhoneNumber { get; private set; } = string.Empty;
    public double DistanceToCampusKm { get; private set; }

    private Room() { }

    /// <summary>
    /// Parameterized constructor supporting ERD §4.4 and existing detail API fixtures.
    /// </summary>
    public Room(
        string id,
        string title,
        string description,
        decimal monthlyRent,
        decimal depositAmount,
        double areaSquareMeters,
        string? roomType = null,
        IReadOnlyList<string>? amenities = null,
        string? fullAddress = null,
        string? landlordName = null,
        string? landlordPhoneNumber = null,
        IReadOnlyList<string>? photoUrls = null,
        string? panorama360Url = null,
        IReadOnlyList<string>? houseRules = null,
        ListingStatus status = ListingStatus.Available,
        DateTime? lastUpdatedAt = null,
        bool isVerified = false,
        double distanceToCampusKm = 0.0,
        string? concurrencyToken = null,
        string? landlordId = null,
        double latitude = 21.5875,
        double longitude = 105.8080,
        RoomType? roomTypeEnum = null,
        string ward = "",
        string district = "TP. Thái Nguyên",
        int version = 1)
    {
        Id = id?.Trim() ?? string.Empty;
        Title = title?.Trim() ?? string.Empty;
        Description = description?.Trim() ?? string.Empty;
        MonthlyRentVnd = monthlyRent;
        DepositVnd = depositAmount;
        AreaM2 = areaSquareMeters;
        AddressLine = fullAddress?.Trim() ?? string.Empty;
        Ward = ward?.Trim() ?? string.Empty;
        District = string.IsNullOrWhiteSpace(district) ? "TP. Thái Nguyên" : district.Trim();
        LandlordId = string.IsNullOrWhiteSpace(landlordId) ? "landlord-default" : landlordId.Trim();
        Latitude = latitude;
        Longitude = longitude;
        Status = status;
        IsSafetyVerified = isVerified;
        VerifiedAtUtc = isVerified ? (lastUpdatedAt ?? DateTime.UtcNow) : null;
        PublishedAtUtc = status == ListingStatus.Available ? (lastUpdatedAt ?? DateTime.UtcNow) : null;
        LastUpdatedAtUtc = lastUpdatedAt ?? DateTime.UtcNow;
        CreatedAtUtc = lastUpdatedAt ?? DateTime.UtcNow;
        Version = version > 0 ? version : 1;
        ConcurrencyToken = string.IsNullOrWhiteSpace(concurrencyToken) ? Version.ToString() : concurrencyToken.Trim();
        _customRoomType = roomType;

        if (roomTypeEnum.HasValue)
        {
            RoomType = roomTypeEnum.Value;
        }
        else if (!string.IsNullOrWhiteSpace(roomType) && Enum.TryParse<RoomType>(roomType.Trim(), true, out var parsedType))
        {
            RoomType = parsedType;
        }
        else
        {
            RoomType = RoomType.BoardingRoom;
        }

        Amenities = amenities != null ? amenities.ToArray() : Array.Empty<string>();
        PhotoUrls = photoUrls != null ? photoUrls.ToArray() : Array.Empty<string>();
        Panorama360Url = panorama360Url;
        HouseRules = houseRules != null ? houseRules.ToArray() : Array.Empty<string>();
        LandlordName = landlordName?.Trim() ?? string.Empty;
        LandlordPhoneNumber = landlordPhoneNumber?.Trim() ?? string.Empty;
        DistanceToCampusKm = distanceToCampusKm;

        EnsureInvariants();
    }

    /// <summary>
    /// Static factory aligning with PR #16 foundation.
    /// </summary>
    public static Room Create(
        string title,
        string description,
        decimal price,
        double area,
        string address,
        double latitude,
        double longitude,
        string landlordId,
        RoomType roomType = RoomType.BoardingRoom,
        decimal depositVnd = 0,
        string ward = "",
        string district = "TP. Thái Nguyên",
        ListingStatus initialStatus = ListingStatus.Available)
    {
        var now = DateTime.UtcNow;
        var room = new Room(
            id: Guid.NewGuid().ToString("N"),
            title: title,
            description: description,
            monthlyRent: price,
            depositAmount: depositVnd,
            areaSquareMeters: area,
            roomType: roomType.ToString(),
            amenities: null,
            fullAddress: address,
            landlordName: null,
            landlordPhoneNumber: null,
            photoUrls: null,
            panorama360Url: null,
            houseRules: null,
            status: initialStatus,
            lastUpdatedAt: now,
            isVerified: initialStatus == ListingStatus.Available,
            distanceToCampusKm: 0.0,
            concurrencyToken: "1",
            landlordId: landlordId,
            latitude: latitude,
            longitude: longitude,
            roomTypeEnum: roomType,
            ward: ward,
            district: district,
            version: 1);

        return room;
    }

    /// <summary>
    /// Static reconstitute method aligning with PR #16 and repository hydration.
    /// </summary>
    public static Room Reconstitute(
        string id,
        string title,
        string description,
        decimal price,
        double area,
        string address,
        double latitude,
        double longitude,
        string landlordId,
        bool isAvailable,
        bool isVerified,
        DateTime createdAtUtc,
        ListingStatus? status = null,
        RoomType roomType = RoomType.BoardingRoom,
        decimal depositVnd = 0,
        string ward = "",
        string district = "TP. Thái Nguyên",
        int version = 1,
        DateTime? lastUpdatedAtUtc = null,
        DateTime? publishedAtUtc = null,
        string? concurrencyToken = null)
    {
        var resolvedStatus = status ?? (isAvailable ? ListingStatus.Available : ListingStatus.Rented);

        var room = new Room(
            id: id,
            title: title,
            description: description,
            monthlyRent: price,
            depositAmount: depositVnd,
            areaSquareMeters: area,
            roomType: roomType.ToString(),
            amenities: null,
            fullAddress: address,
            landlordName: null,
            landlordPhoneNumber: null,
            photoUrls: null,
            panorama360Url: null,
            houseRules: null,
            status: resolvedStatus,
            lastUpdatedAt: lastUpdatedAtUtc ?? createdAtUtc,
            isVerified: isVerified,
            distanceToCampusKm: 0.0,
            concurrencyToken: concurrencyToken ?? version.ToString(),
            landlordId: landlordId,
            latitude: latitude,
            longitude: longitude,
            roomTypeEnum: roomType,
            ward: ward,
            district: district,
            version: version);

        room.CreatedAtUtc = createdAtUtc;
        room.PublishedAtUtc = publishedAtUtc ?? (isAvailable ? createdAtUtc : null);
        return room;
    }

    public void SetConcurrencyToken(string? token)
    {
        ConcurrencyToken = string.IsNullOrWhiteSpace(token) ? Version.ToString() : token.Trim();
    }

    // ADR-011 & BR-LST-04 State Machine Transitions

    public void Approve(string adminUserId)
    {
        if (Status != ListingStatus.PendingReview)
        {
            throw new InvalidStateTransitionException($"Cannot approve room in '{Status}' state. Only PendingReview listings can be approved.");
        }

        Status = ListingStatus.Available;
        StatusReason = null;
        VerifiedByUserId = adminUserId;
        VerifiedAtUtc = DateTime.UtcNow;
        PublishedAtUtc ??= DateTime.UtcNow;
        LastUpdatedAtUtc = DateTime.UtcNow;
        Version++;
        ConcurrencyToken = Version.ToString();
    }

    public void Reject(string reason)
    {
        if (Status != ListingStatus.PendingReview)
        {
            throw new InvalidStateTransitionException($"Cannot reject room in '{Status}' state. Only PendingReview listings can be rejected.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("Rejection reason is required.");
        }

        Status = ListingStatus.Rejected;
        StatusReason = reason.Trim();
        LastUpdatedAtUtc = DateTime.UtcNow;
        Version++;
        ConcurrencyToken = Version.ToString();
    }

    public void Resubmit()
    {
        if (Status != ListingStatus.Rejected && Status != ListingStatus.Suspended)
        {
            throw new InvalidStateTransitionException($"Cannot resubmit room in '{Status}' state. Only Rejected or Suspended listings can be resubmitted.");
        }

        Status = ListingStatus.PendingReview;
        StatusReason = null;
        LastUpdatedAtUtc = DateTime.UtcNow;
        Version++;
        ConcurrencyToken = Version.ToString();
    }

    public void MarkRented()
    {
        if (Status != ListingStatus.Available)
        {
            throw new InvalidStateTransitionException($"Cannot mark room as rented in '{Status}' state. Only Available listings can be marked as rented.");
        }

        Status = ListingStatus.Rented;
        LastUpdatedAtUtc = DateTime.UtcNow;
        Version++;
        ConcurrencyToken = Version.ToString();
    }

    public void MarkAvailable()
    {
        if (Status != ListingStatus.Rented)
        {
            throw new InvalidStateTransitionException($"Cannot mark room as available in '{Status}' state. Only Rented listings can be marked as available.");
        }

        Status = ListingStatus.Available;
        LastUpdatedAtUtc = DateTime.UtcNow;
        Version++;
        ConcurrencyToken = Version.ToString();
    }

    public void Unpublish()
    {
        if (Status != ListingStatus.Available && Status != ListingStatus.Rented)
        {
            throw new InvalidStateTransitionException($"Cannot unpublish room in '{Status}' state. Only Available or Rented listings can be unpublished.");
        }

        Status = ListingStatus.Unpublished;
        LastUpdatedAtUtc = DateTime.UtcNow;
        Version++;
        ConcurrencyToken = Version.ToString();
    }

    public void Republish()
    {
        if (Status != ListingStatus.Unpublished)
        {
            throw new InvalidStateTransitionException($"Cannot republish room in '{Status}' state. Only Unpublished listings can be republished.");
        }

        Status = ListingStatus.Available;
        LastUpdatedAtUtc = DateTime.UtcNow;
        Version++;
        ConcurrencyToken = Version.ToString();
    }

    public void Suspend(string reason)
    {
        if (Status != ListingStatus.Available && Status != ListingStatus.Rented && Status != ListingStatus.Unpublished)
        {
            throw new InvalidStateTransitionException($"Cannot suspend room in '{Status}' state. Only active/published listings can be suspended.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("Suspension reason is required.");
        }

        Status = ListingStatus.Suspended;
        StatusReason = reason.Trim();
        LastUpdatedAtUtc = DateTime.UtcNow;
        Version++;
        ConcurrencyToken = Version.ToString();
    }

    private void EnsureInvariants()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new DomainValidationException("Room ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new DomainValidationException("Room title cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(LandlordId))
        {
            throw new DomainValidationException("Room landlord is required.");
        }

        if (MonthlyRentVnd <= 0m)
        {
            throw new DomainValidationException("Monthly rent must be greater than zero.");
        }

        if (DepositVnd < 0m)
        {
            throw new DomainValidationException("Deposit amount cannot be negative.");
        }

        if (AreaM2 <= 0)
        {
            throw new DomainValidationException("Area square meters must be greater than zero.");
        }

        if (double.IsNaN(Latitude) || Latitude < -90 || Latitude > 90)
        {
            throw new DomainValidationException("Room latitude must be between -90 and 90.");
        }

        if (double.IsNaN(Longitude) || Longitude < -180 || Longitude > 180)
        {
            throw new DomainValidationException("Room longitude must be between -180 and 180.");
        }

        if (!Enum.IsDefined(typeof(ListingStatus), Status))
        {
            throw new DomainValidationException($"Invalid listing status: {Status}.");
        }

        if (!Enum.IsDefined(typeof(RoomType), RoomType))
        {
            throw new DomainValidationException($"Invalid room type: {RoomType}.");
        }
    }
}
