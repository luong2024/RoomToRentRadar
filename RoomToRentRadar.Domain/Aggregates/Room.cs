using System.Globalization;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Aggregates;

/// <summary>
/// Aggregate root of a room listing (<c>docs/erd.md</c> §4.4, ADR-011).
/// Immutable for callers: state changes only through the lifecycle methods below —
/// there is no public setter for <see cref="Status"/> or any other field.
/// </summary>
public sealed class Room
{
    public const int TitleMinLength = 10;
    public const int TitleMaxLength = 100;
    public const long MonthlyRentMinVnd = 100_000;
    public const long MonthlyRentMaxVnd = 50_000_000;
    public const int DepositMonthsMultiplier = 3;
    public const double AreaMinM2 = 5.0;
    public const double AreaMaxM2 = 200.0;

    public string Id { get; private set; }
    public string LandlordId { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public RoomType RoomType { get; private set; }
    public long MonthlyRentVnd { get; private set; }
    public long DepositVnd { get; private set; }
    public double AreaM2 { get; private set; }
    public int? MaxOccupants { get; private set; }
    public string? Furnishing { get; private set; }
    public DateOnly? AvailableFrom { get; private set; }
    public string AddressLine { get; private set; }
    public string Ward { get; private set; }
    public string District { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public long? ElectricityPriceVndPerKwh { get; private set; }
    public long? WaterPriceVnd { get; private set; }
    public string? WaterPriceUnit { get; private set; }
    public long? InternetFeeVnd { get; private set; }
    public long? ParkingFeeVnd { get; private set; }
    public string? OtherFeesNote { get; private set; }
    public ListingStatus Status { get; private set; }
    public string? StatusReason { get; private set; }
    public bool NeedsReReview { get; private set; }
    public bool IsSafetyVerified { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public string? VerifiedByUserId { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime LastUpdatedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int Version { get; private set; }
    public string ConcurrencyToken { get; private set; }

    private Room(
        string id,
        string landlordId,
        string title,
        string? description,
        RoomType roomType,
        long monthlyRentVnd,
        long depositVnd,
        double areaM2,
        int? maxOccupants,
        string? furnishing,
        DateOnly? availableFrom,
        string addressLine,
        string ward,
        string district,
        double latitude,
        double longitude,
        long? electricityPriceVndPerKwh,
        long? waterPriceVnd,
        string? waterPriceUnit,
        long? internetFeeVnd,
        long? parkingFeeVnd,
        string? otherFeesNote,
        ListingStatus status,
        string? statusReason,
        bool needsReReview,
        bool isSafetyVerified,
        DateTime? verifiedAtUtc,
        string? verifiedByUserId,
        DateTime? publishedAtUtc,
        DateTime lastUpdatedAtUtc,
        DateTime createdAtUtc,
        int version,
        string concurrencyToken)
    {
        Id = id;
        LandlordId = landlordId;
        Title = title.Trim();
        Description = description;
        RoomType = roomType;
        MonthlyRentVnd = monthlyRentVnd;
        DepositVnd = depositVnd;
        AreaM2 = areaM2;
        MaxOccupants = maxOccupants;
        Furnishing = furnishing;
        AvailableFrom = availableFrom;
        AddressLine = addressLine;
        Ward = ward;
        District = district;
        Latitude = latitude;
        Longitude = longitude;
        ElectricityPriceVndPerKwh = electricityPriceVndPerKwh;
        WaterPriceVnd = waterPriceVnd;
        WaterPriceUnit = waterPriceUnit;
        InternetFeeVnd = internetFeeVnd;
        ParkingFeeVnd = parkingFeeVnd;
        OtherFeesNote = otherFeesNote;
        Status = status;
        StatusReason = statusReason;
        NeedsReReview = needsReReview;
        IsSafetyVerified = isSafetyVerified;
        VerifiedAtUtc = AsUtc(verifiedAtUtc);
        VerifiedByUserId = verifiedByUserId;
        PublishedAtUtc = AsUtc(publishedAtUtc);
        LastUpdatedAtUtc = AsUtc(lastUpdatedAtUtc)!.Value;
        CreatedAtUtc = AsUtc(createdAtUtc)!.Value;
        Version = version;
        ConcurrencyToken = concurrencyToken;
    }

    /// <summary>ERD §1: every date-time column is stored and exposed as UTC.</summary>
    private static DateTime? AsUtc(DateTime? value)
    {
        if (value is not { } instant)
        {
            return null;
        }

        return instant.Kind switch
        {
            DateTimeKind.Utc => instant,
            DateTimeKind.Local => instant.ToUniversalTime(),
            _ => DateTime.SpecifyKind(instant, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// BR-LST-04/06: a new listing always starts in <see cref="ListingStatus.PendingReview"/> and is
    /// never verified or published before an Admin approves it.
    /// </summary>
    public static Room Create(
        string landlordId,
        string title,
        RoomType roomType,
        long monthlyRentVnd,
        long depositVnd,
        double areaM2,
        string addressLine,
        string ward,
        string district,
        double latitude,
        double longitude,
        string? description = null,
        int? maxOccupants = null,
        string? furnishing = null,
        DateOnly? availableFrom = null,
        long? electricityPriceVndPerKwh = null,
        long? waterPriceVnd = null,
        string? waterPriceUnit = null,
        long? internetFeeVnd = null,
        long? parkingFeeVnd = null,
        string? otherFeesNote = null,
        DateTime? utcNow = null)
    {
        var now = AsUtc(utcNow ?? DateTime.UtcNow)!.Value;

        var room = new Room(
            id: Guid.NewGuid().ToString("N"),
            landlordId: landlordId,
            title: title,
            description: description,
            roomType: roomType,
            monthlyRentVnd: monthlyRentVnd,
            depositVnd: depositVnd,
            areaM2: areaM2,
            maxOccupants: maxOccupants,
            furnishing: furnishing,
            availableFrom: availableFrom,
            addressLine: addressLine,
            ward: ward,
            district: district,
            latitude: latitude,
            longitude: longitude,
            electricityPriceVndPerKwh: electricityPriceVndPerKwh,
            waterPriceVnd: waterPriceVnd,
            waterPriceUnit: waterPriceUnit,
            internetFeeVnd: internetFeeVnd,
            parkingFeeVnd: parkingFeeVnd,
            otherFeesNote: otherFeesNote,
            status: ListingStatus.PendingReview,
            statusReason: null,
            needsReReview: false,
            isSafetyVerified: false,
            verifiedAtUtc: null,
            verifiedByUserId: null,
            publishedAtUtc: null,
            lastUpdatedAtUtc: now,
            createdAtUtc: now,
            version: 1,
            concurrencyToken: ConcurrencyTokenFor(1));

        room.EnsureInvariants();
        return room;
    }

    /// <summary>Rebuilds a Room from persistence or a test fixture; every column of ERD §4.4 is accepted.</summary>
    public static Room Reconstitute(
        string id,
        string landlordId,
        string title,
        RoomType roomType,
        long monthlyRentVnd,
        long depositVnd,
        double areaM2,
        string addressLine,
        string ward,
        string district,
        double latitude,
        double longitude,
        ListingStatus status,
        DateTime createdAtUtc,
        DateTime lastUpdatedAtUtc,
        int version,
        string? concurrencyToken = null,
        string? description = null,
        int? maxOccupants = null,
        string? furnishing = null,
        DateOnly? availableFrom = null,
        long? electricityPriceVndPerKwh = null,
        long? waterPriceVnd = null,
        string? waterPriceUnit = null,
        long? internetFeeVnd = null,
        long? parkingFeeVnd = null,
        string? otherFeesNote = null,
        string? statusReason = null,
        bool needsReReview = false,
        bool isSafetyVerified = false,
        DateTime? verifiedAtUtc = null,
        string? verifiedByUserId = null,
        DateTime? publishedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DomainValidationException("Room Id is required.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new DomainValidationException($"Listing status '{(int)status}' is not a recognized status.");
        }

        if (version < 1)
        {
            throw new DomainValidationException("Room version must be at least 1.");
        }

        var room = new Room(
            id: id,
            landlordId: landlordId,
            title: title,
            description: description,
            roomType: roomType,
            monthlyRentVnd: monthlyRentVnd,
            depositVnd: depositVnd,
            areaM2: areaM2,
            maxOccupants: maxOccupants,
            furnishing: furnishing,
            availableFrom: availableFrom,
            addressLine: addressLine,
            ward: ward,
            district: district,
            latitude: latitude,
            longitude: longitude,
            electricityPriceVndPerKwh: electricityPriceVndPerKwh,
            waterPriceVnd: waterPriceVnd,
            waterPriceUnit: waterPriceUnit,
            internetFeeVnd: internetFeeVnd,
            parkingFeeVnd: parkingFeeVnd,
            otherFeesNote: otherFeesNote,
            status: status,
            statusReason: statusReason,
            needsReReview: needsReReview,
            isSafetyVerified: isSafetyVerified,
            verifiedAtUtc: verifiedAtUtc,
            verifiedByUserId: verifiedByUserId,
            publishedAtUtc: publishedAtUtc,
            lastUpdatedAtUtc: lastUpdatedAtUtc,
            createdAtUtc: createdAtUtc,
            version: version,
            concurrencyToken: string.IsNullOrWhiteSpace(concurrencyToken)
                ? ConcurrencyTokenFor(version)
                : concurrencyToken);

        room.EnsureInvariants();
        return room;
    }

    public bool IsPubliclyVisible => Status == ListingStatus.Available;

    // ----------------------------------------------------------------- ADR-011 state machine

    public void Approve(DateTime verifiedAtUtc)
    {
        EnsureTransition(ListingStatus.PendingReview, nameof(Approve), ListingStatus.Available);

        var verifiedAt = AsUtc(verifiedAtUtc)!.Value;
        Status = ListingStatus.Available;
        VerifiedAtUtc = verifiedAt;
        PublishedAtUtc ??= verifiedAt;
        StatusReason = null;
        Touch();
    }

    public void Reject(string reason)
    {
        EnsureTransition(ListingStatus.PendingReview, nameof(Reject), ListingStatus.Rejected);
        EnsureReason(reason, nameof(Reject));

        Status = ListingStatus.Rejected;
        StatusReason = reason.Trim();
        Touch();
    }

    public void Resubmit()
    {
        if (Status != ListingStatus.Rejected && Status != ListingStatus.Suspended)
        {
            throw InvalidTransition(
                nameof(Resubmit),
                Status,
                $"{ListingStatus.Rejected}, {ListingStatus.Suspended} -> {ListingStatus.PendingReview}");
        }

        Status = ListingStatus.PendingReview;
        StatusReason = null;
        Touch();
    }

    public void MarkRented()
    {
        EnsureTransition(ListingStatus.Available, nameof(MarkRented), ListingStatus.Rented);

        Status = ListingStatus.Rented;
        Touch();
    }

    public void MarkAvailable()
    {
        EnsureTransition(ListingStatus.Rented, nameof(MarkAvailable), ListingStatus.Available);

        Status = ListingStatus.Available;
        Touch();
    }

    public void Unpublish()
    {
        if (Status != ListingStatus.Available && Status != ListingStatus.Rented)
        {
            throw InvalidTransition(
                nameof(Unpublish),
                Status,
                $"{ListingStatus.Available}, {ListingStatus.Rented} -> {ListingStatus.Unpublished}");
        }

        Status = ListingStatus.Unpublished;
        Touch();
    }

    public void Republish()
    {
        EnsureTransition(ListingStatus.Unpublished, nameof(Republish), ListingStatus.Available);

        Status = ListingStatus.Available;
        Touch();
    }

    public void Suspend(string reason)
    {
        if (Status != ListingStatus.Available &&
            Status != ListingStatus.Rented &&
            Status != ListingStatus.Unpublished)
        {
            throw InvalidTransition(
                nameof(Suspend),
                Status,
                $"{ListingStatus.Available}, {ListingStatus.Rented}, {ListingStatus.Unpublished} -> {ListingStatus.Suspended}");
        }

        EnsureReason(reason, nameof(Suspend));

        Status = ListingStatus.Suspended;
        StatusReason = reason.Trim();
        Touch();
    }

    // ----------------------------------------------------------------- helpers

    private void Touch()
    {
        LastUpdatedAtUtc = DateTime.UtcNow;
        Version++;
        // ERD §1: the token is derived from Version, never a second source of truth.
        ConcurrencyToken = ConcurrencyTokenFor(Version);
    }

    private static string ConcurrencyTokenFor(int version) =>
        version.ToString(CultureInfo.InvariantCulture);

    private void EnsureTransition(ListingStatus required, string operation, ListingStatus target)
    {
        if (Status != required)
        {
            throw InvalidTransition(operation, Status, $"{required} -> {target}");
        }
    }

    private static InvalidStateTransitionException InvalidTransition(
        string operation,
        ListingStatus current,
        string expected) =>
        new($"Cannot {operation}: listing is '{current}' but the transition requires '{expected}'.");

    private static void EnsureReason(string reason, string operation)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException($"{operation} requires a reason.");
        }
    }

    private void EnsureInvariants()
    {
        if (string.IsNullOrWhiteSpace(LandlordId))
        {
            throw new DomainValidationException("Room landlord is required.");
        }

        if (string.IsNullOrWhiteSpace(Title) || Title.Length < TitleMinLength || Title.Length > TitleMaxLength)
        {
            throw new DomainValidationException(
                $"Room title must be between {TitleMinLength} and {TitleMaxLength} characters.", // BR-LST-01
                "INVALID_ROOM_TITLE");
        }

        if (!Enum.IsDefined(RoomType))
        {
            throw new DomainValidationException($"Room type '{(int)RoomType}' is not a recognized room type.");
        }

        if (MonthlyRentVnd < MonthlyRentMinVnd || MonthlyRentVnd > MonthlyRentMaxVnd)
        {
            throw new DomainValidationException(
                $"Monthly rent must be between {MonthlyRentMinVnd:N0} and {MonthlyRentMaxVnd:N0} VND.", // BR-LST-01
                "INVALID_RENT");
        }

        if (DepositVnd < 0 || DepositVnd > MonthlyRentVnd * DepositMonthsMultiplier)
        {
            throw new DomainValidationException(
                "Deposit must be between 0 and 3 times the monthly rent.", // BR-LST-01
                "INVALID_DEPOSIT");
        }

        if (!double.IsFinite(AreaM2) || AreaM2 < AreaMinM2 || AreaM2 > AreaMaxM2)
        {
            throw new DomainValidationException(
                $"Room area must be finite and between {AreaMinM2} and {AreaMaxM2} square metres.", // BR-LST-01
                "INVALID_AREA");
        }

        if (MaxOccupants.HasValue && (MaxOccupants < 1 || MaxOccupants > 10))
        {
            throw new DomainValidationException("Max occupants must be between 1 and 10.");
        }

        if (string.IsNullOrWhiteSpace(AddressLine))
        {
            throw new DomainValidationException("Room address line is required.");
        }

        if (string.IsNullOrWhiteSpace(Ward))
        {
            throw new DomainValidationException("Room ward is required.");
        }

        if (string.IsNullOrWhiteSpace(District))
        {
            throw new DomainValidationException("Room district is required.");
        }

        if (!double.IsFinite(Latitude) || Latitude < -90 || Latitude > 90)
        {
            throw new DomainValidationException("Room latitude must be finite and between -90 and 90.");
        }

        if (!double.IsFinite(Longitude) || Longitude < -180 || Longitude > 180)
        {
            throw new DomainValidationException("Room longitude must be finite and between -180 and 180.");
        }

        if (ElectricityPriceVndPerKwh is < 0 or > 10_000)
        {
            throw new DomainValidationException("Electricity price must be between 0 and 10,000 VND per kWh."); // BR-LST-03
        }
    }
}
