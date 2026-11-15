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

    /// <summary>
    /// Version read from storage when this instance was loaded (GUARDRAILS §1.5.2). <c>UpdateAsync</c>
    /// must match THIS value, not <c>Version - 1</c>: several transitions may happen between load and save.
    /// </summary>
    public int LoadedVersion { get; private set; }

    // Phase 1 Transitional properties for detail views until Phase 2/3 extracts them to respective read models
    public IReadOnlyList<string> Amenities { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> PhotoUrls { get; private set; } = Array.Empty<string>();
    public string? Panorama360Url { get; private set; }
    public IReadOnlyList<string> HouseRules { get; private set; } = Array.Empty<string>();
    public string LandlordName { get; private set; } = string.Empty;
    public string LandlordPhoneNumber { get; private set; } = string.Empty;
    public bool LandlordIsVerified { get; private set; }
    public DateTime? LandlordMemberSinceUtc { get; private set; }
    public double DistanceToCampusKm { get; private set; }

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
        LastUpdatedAtUtc = AsUtc(lastUpdatedAtUtc) ?? DateTime.UtcNow;
        CreatedAtUtc = AsUtc(createdAtUtc) ?? DateTime.UtcNow;
        Version = version;
        ConcurrencyToken = concurrencyToken;
        LoadedVersion = version;
    }

    /// <summary>
    /// Parameterized constructor supporting fixtures and existing detail API tests.
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
        string ward = "Tân Thịnh",
        string district = "TP. Thái Nguyên",
        int version = 1)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DomainValidationException("Room ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainValidationException("Room title cannot be empty.");
        }

        if (monthlyRent <= 0)
        {
            throw new DomainValidationException("Monthly rent must be greater than zero.");
        }

        if (depositAmount < 0)
        {
            throw new DomainValidationException("Deposit amount cannot be negative.");
        }

        if (areaSquareMeters <= 0)
        {
            throw new DomainValidationException("Area square meters must be greater than zero.");
        }

        if (latitude < -90.0 || latitude > 90.0)
        {
            throw new DomainValidationException("Latitude must be between -90 and 90.");
        }

        if (longitude < -180.0 || longitude > 180.0)
        {
            throw new DomainValidationException("Longitude must be between -180 and 180.");
        }

        if (!Enum.IsDefined(typeof(ListingStatus), status))
        {
            throw new DomainValidationException($"Invalid listing status: {(int)status}.");
        }

        Id = id.Trim();
        Title = title?.Trim() ?? string.Empty;
        Description = description?.Trim() ?? string.Empty;
        MonthlyRentVnd = (long)monthlyRent;
        DepositVnd = (long)depositAmount;
        AreaM2 = areaSquareMeters;
        AddressLine = string.IsNullOrWhiteSpace(fullAddress) ? "Đường Z115" : fullAddress.Trim();
        Ward = string.IsNullOrWhiteSpace(ward) ? "Tân Thịnh" : ward.Trim();
        District = string.IsNullOrWhiteSpace(district) ? "TP. Thái Nguyên" : district.Trim();
        LandlordId = string.IsNullOrWhiteSpace(landlordId)
            ? (!string.IsNullOrWhiteSpace(landlordName) || !string.IsNullOrWhiteSpace(landlordPhoneNumber) ? $"landlord-{id.Trim()}" : "seed-landlord")
            : landlordId.Trim();
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
        LoadedVersion = Version;

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
    }

    /// <summary>
    /// Factory for creating a new room listing in the initial <see cref="ListingStatus.PendingReview"/> state.
    /// Invariants are enforced before the aggregate is returned (GUARDRAILS §1.5.3).
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
        var now = AsUtc(utcNow) ?? DateTime.UtcNow;

        var id = Guid.NewGuid().ToString("N");
        var version = 1;
        var token = ConcurrencyTokenFor(version);

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
            status: ListingStatus.PendingReview,
            statusReason: null,
            needsReReview: false,
            isSafetyVerified: false,
            verifiedAtUtc: null,
            verifiedByUserId: null,
            publishedAtUtc: null,
            lastUpdatedAtUtc: now,
            createdAtUtc: now,
            version: version,
            concurrencyToken: token);

        room.EnsureInvariants();
        return room;
    }

    /// <summary>
    /// Convenience factory for tests using legacy parameter names.
    /// </summary>
    public static Room Create(
        string title,
        string description,
        decimal price,
        double area,
        string address,
        double latitude,
        double longitude,
        string landlordId = "seed-landlord",
        RoomType roomType = RoomType.BoardingRoom,
        string ward = "Tân Thịnh",
        string district = "TP. Thái Nguyên",
        ListingStatus? initialStatus = null)
    {
        var now = DateTime.UtcNow;
        var rentLong = (long)price;
        var depositLong = (long)price;
        var room = Create(
            landlordId: landlordId,
            title: title,
            roomType: roomType,
            monthlyRentVnd: rentLong,
            depositVnd: depositLong,
            areaM2: area,
            addressLine: address,
            ward: ward,
            district: district,
            latitude: latitude,
            longitude: longitude,
            description: description,
            utcNow: now);

        if (initialStatus == null || initialStatus == ListingStatus.Available)
        {
            room.Approve(now);
        }
        return room;
    }

    /// <summary>
    /// Reconstitutes a room from persistence. Skips creation invariants so historical records
    /// that do not satisfy modern validation rules can still be loaded (GUARDRAILS §1.5.3).
    /// </summary>
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
        DateTime? publishedAtUtc = null,
        string? landlordName = null,
        string? landlordPhoneNumber = null)
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

        if (!string.IsNullOrWhiteSpace(landlordName))
        {
            room.LandlordName = landlordName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(landlordPhoneNumber))
        {
            room.LandlordPhoneNumber = landlordPhoneNumber.Trim();
        }

        room.EnsureInvariants();
        return room;
    }

    public void SetLandlordContact(string? name, string? phoneNumber, bool isVerified = false, DateTime? memberSinceUtc = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            LandlordName = name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            LandlordPhoneNumber = phoneNumber.Trim();
        }

        LandlordIsVerified = isVerified;
        if (memberSinceUtc.HasValue)
        {
            LandlordMemberSinceUtc = memberSinceUtc.Value;
        }
    }

    /// <summary>
    /// Legacy Reconstitute overload for fixture backward compatibility.
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
        ListingStatus status = ListingStatus.Available)
    {
        return new Room(
            id: id,
            title: title,
            description: description,
            monthlyRent: price,
            depositAmount: price,
            areaSquareMeters: area,
            fullAddress: address,
            latitude: latitude,
            longitude: longitude,
            landlordId: landlordId,
            isVerified: isVerified,
            status: status,
            lastUpdatedAt: createdAtUtc
        );
    }

    // ----------------------------------------------------------------- ADR-011 state machine

    public void Approve(DateTime verifiedAtUtc, string? verifiedByUserId = null)
    {
        EnsureTransition(ListingStatus.PendingReview, nameof(Approve), ListingStatus.Available);

        var verifiedAt = AsUtc(verifiedAtUtc)!.Value;
        Status = ListingStatus.Available;
        VerifiedAtUtc = verifiedAt;
        PublishedAtUtc ??= verifiedAt;
        if (!string.IsNullOrWhiteSpace(verifiedByUserId))
        {
            VerifiedByUserId = verifiedByUserId.Trim();
        }
        StatusReason = null;
        Touch();
    }

    public void Approve(string verifiedByUserId) =>
        Approve(DateTime.UtcNow, verifiedByUserId);

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
                $"{ListingStatus.Rejected} or {ListingStatus.Suspended} -> {ListingStatus.PendingReview}");
        }

        Status = ListingStatus.PendingReview;
        StatusReason = null;
        NeedsReReview = false;
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

    public void SetConcurrencyToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new DomainValidationException("Concurrency token cannot be empty.");
        }
        ConcurrencyToken = token.Trim();
    }

    // ----------------------------------------------------------------- helpers

    /// <summary>
    /// Called by the repository after a successful INSERT/UPDATE so the next save compares against
    /// the version that is now in storage.
    /// </summary>
    public void MarkPersisted()
    {
        LoadedVersion = Version;
    }

    private void Touch()
    {
        LastUpdatedAtUtc = DateTime.UtcNow;
        Version++;
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

    private static DateTime? AsUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
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
