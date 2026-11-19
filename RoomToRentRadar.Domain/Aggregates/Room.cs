using System.Globalization;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

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

    /// <summary>BR-LST-01: the pin must stay inside Thái Nguyên province.</summary>
    public const double ThaiNguyenMinLat = 21.30;
    public const double ThaiNguyenMaxLat = 22.05;
    public const double ThaiNguyenMinLng = 105.45;
    public const double ThaiNguyenMaxLng = 106.25;

    public const int DescriptionMaxLength = 2000;
    public const int AddressLineMaxLength = 200;
    public const int WardMaxLength = 100;
    public const int DistrictMaxLength = 100;
    public const int OtherFeesNoteMaxLength = 200;

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
    public IReadOnlyList<Amenity> Amenities { get; private set; } = Array.Empty<Amenity>();
    public IReadOnlyList<RoomSafetyFeatureEntry> SafetyFeatures { get; private set; } = Array.Empty<RoomSafetyFeatureEntry>();
    public HouseRuleVersion? CurrentHouseRules { get; private set; }
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

    private readonly List<HouseRuleVersion> _houseRuleHistory = [];
    public IReadOnlyList<HouseRuleVersion> HouseRuleHistory => _houseRuleHistory;

    /// <summary>
    /// Derived score (docs/erd.md §4.4, BR-LST-08):
    /// When IsSafetyVerified is true, score = count of confirmed safety features.
    /// Otherwise, score = count of declared safety features.
    /// </summary>
    public int SafetyScore => IsSafetyVerified
        ? SafetyFeatures.Count(f => f.IsConfirmed)
        : SafetyFeatures.Count;

    /// <summary>
    /// Version read from storage when this instance was loaded (GUARDRAILS §1.5.2). <c>UpdateAsync</c>
    /// must match THIS value, not <c>Version - 1</c>: several transitions may happen between load and save.
    /// </summary>
    public int LoadedVersion { get; private set; }

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
        LoadedVersion = version;
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
        DateTime? utcNow = null,
        IEnumerable<Amenity>? amenities = null,
        IEnumerable<SafetyFeature>? safetyFeatures = null)
    {
        var now = AsUtc(utcNow ?? DateTime.UtcNow)!.Value;
        var initialSafetyFeatures = safetyFeatures?
            .Distinct()
            .Select(f => new RoomSafetyFeatureEntry(f, IsConfirmed: false))
            .ToList();

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

        room.SetChildren(amenities, initialSafetyFeatures, null);
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
        DateTime lastUpdatedAtUtc,
        DateTime createdAtUtc,
        int version,
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
        IEnumerable<Amenity>? amenities = null,
        IEnumerable<RoomSafetyFeatureEntry>? safetyFeatures = null,
        HouseRuleVersion? currentHouseRules = null,
        IEnumerable<HouseRuleVersion>? houseRuleHistory = null,
        string? concurrencyToken = null)
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
            verifiedAtUtc: AsUtc(verifiedAtUtc),
            verifiedByUserId: verifiedByUserId,
            publishedAtUtc: AsUtc(publishedAtUtc),
            lastUpdatedAtUtc: AsUtc(lastUpdatedAtUtc)!.Value,
            createdAtUtc: AsUtc(createdAtUtc)!.Value,
            version: version,
            concurrencyToken: string.IsNullOrWhiteSpace(concurrencyToken)
                ? ConcurrencyTokenFor(version)
                : concurrencyToken);

        if (houseRuleHistory != null)
        {
            room._houseRuleHistory.AddRange(houseRuleHistory);
        }

        var latestRule = currentHouseRules ?? room._houseRuleHistory.OrderByDescending(h => h.VersionNumber).FirstOrDefault();
        room.SetChildren(amenities, safetyFeatures, latestRule);
        room.EnsureInvariants();
        return room;
    }

    public bool IsPubliclyVisible => Status == ListingStatus.Available;

    // ----------------------------------------------------------------- children & landlord edits

    public void SetAmenities(IEnumerable<Amenity> amenities)
    {
        ArgumentNullException.ThrowIfNull(amenities);
        Amenities = NormalizeEnumList(amenities, "Amenity", "INVALID_AMENITY");
        Touch();
    }

    public void DeclareSafetyFeatures(IEnumerable<SafetyFeature> features)
    {
        ArgumentNullException.ThrowIfNull(features);
        var requested = NormalizeEnumList(features, "Safety feature", "INVALID_SAFETY_FEATURE");
        var existingConfirmed = SafetyFeatures.Where(f => f.IsConfirmed).Select(f => f.Feature).ToHashSet();
        var addedAnyNew = requested.Any(f => !SafetyFeatures.Any(existing => existing.Feature == f));

        SafetyFeatures = requested
            .Select(f => new RoomSafetyFeatureEntry(f, existingConfirmed.Contains(f)))
            .ToList();

        if (addedAnyNew && (Status == ListingStatus.Available || IsSafetyVerified))
        {
            NeedsReReview = true;
        }

        Touch();
    }

    /// <summary>
    /// Admin confirms safety features during moderation (BR-LST-08).
    /// Marks confirmed features, sets <see cref="IsSafetyVerified"/> to true, and records the admin auditor.
    /// Idempotent: deselecting a previously confirmed feature resets it to false.
    /// </summary>
    public void ConfirmSafetyFeatures(IEnumerable<SafetyFeature> confirmedFeatures, string adminUserId, DateTime? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(confirmedFeatures);
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new DomainValidationException("Admin user ID is required to confirm safety features.");
        }

        var confirmedSet = confirmedFeatures.ToHashSet();
        var updated = new List<RoomSafetyFeatureEntry>();
        foreach (var current in SafetyFeatures)
        {
            // Idempotent: set true if present in confirmedSet, reset to false otherwise (Fix Medium Finding)
            updated.Add(new RoomSafetyFeatureEntry(current.Feature, confirmedSet.Contains(current.Feature)));
        }

        SafetyFeatures = updated;
        IsSafetyVerified = true;
        VerifiedByUserId = adminUserId.Trim();
        VerifiedAtUtc = AsUtc(utcNow ?? DateTime.UtcNow);
        Touch();
    }

    public HouseRuleVersion AddHouseRuleVersion(
        GuestPolicy guestPolicy,
        CookingPolicy cookingPolicy,
        bool parkingAvailable,
        bool petsAllowed,
        bool smokingAllowed,
        string createdByUserId,
        string? quietHoursStart = null,
        string? quietHoursEnd = null,
        string? curfewTime = null,
        string? guestPolicyNote = null,
        string? parkingRules = null,
        string? otherRules = null,
        DateTime? utcNow = null)
    {
        var nextVersion = (CurrentHouseRules?.VersionNumber ?? 0) + 1;
        var version = HouseRuleVersion.Create(
            roomId: Id,
            versionNumber: nextVersion,
            guestPolicy: guestPolicy,
            cookingPolicy: cookingPolicy,
            parkingAvailable: parkingAvailable,
            petsAllowed: petsAllowed,
            smokingAllowed: smokingAllowed,
            createdByUserId: createdByUserId,
            utcNow: utcNow,
            quietHoursStart: quietHoursStart,
            quietHoursEnd: quietHoursEnd,
            curfewTime: curfewTime,
            guestPolicyNote: guestPolicyNote,
            parkingRules: parkingRules,
            otherRules: otherRules);

        _houseRuleHistory.Add(version);
        CurrentHouseRules = version;
        Touch();
        return version;
    }

    /// <summary>
    /// Assigns the child collections loaded from storage (or supplied when creating a listing).
    /// Validates every enum, removes duplicates keeping the first occurrence and a stable order, and
    /// deliberately does not <see cref="Touch"/>: creating a room already starts at version 1.
    /// </summary>
    public void SetChildren(
        IEnumerable<Amenity>? amenities,
        IEnumerable<RoomSafetyFeatureEntry>? safetyFeatures,
        HouseRuleVersion? currentHouseRules = null)
    {
        Amenities = NormalizeEnumList(amenities, "Amenity", "INVALID_AMENITY"); // GUARDRAILS 1.5.1 whitelist
        SafetyFeatures = NormalizeSafetyFeatureEntries(safetyFeatures);
        CurrentHouseRules = currentHouseRules;
        if (currentHouseRules != null && !_houseRuleHistory.Any(h => h.Id == currentHouseRules.Id))
        {
            _houseRuleHistory.Add(currentHouseRules);
        }
    }

    /// <summary>
    /// Applies a landlord edit (BR-LST-05, BR-LST-09, ERD §4.6). Status rules:
    /// <list type="bullet">
    /// <item><c>Rejected</c> → back to <c>PendingReview</c> with the reason cleared.</item>
    /// <item><c>Available</c>/<c>Rented</c>/<c>Unpublished</c> → keep the status; a material change
    /// (address, pin, room type, newly added photos or safety feature) raises <see cref="NeedsReReview"/>.</item>
    /// <item><c>PendingReview</c> → keep the status, no re-review needed.</item>
    /// <item><c>Suspended</c> → untouched until the landlord calls <see cref="Resubmit"/>.</item>
    /// </list>
    /// Confirmed safety features stay confirmed; a newly declared feature starts unconfirmed.
    /// </summary>
    public void UpdateDetails(RoomDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var requestedAmenities = NormalizeEnumList(details.Amenities, "Amenity", "INVALID_AMENITY");
        var requestedSafetyFeatures = NormalizeEnumList(details.SafetyFeatures, "Safety feature", "INVALID_SAFETY_FEATURE"); // BR-LST-08

        var confirmedByFeature = SafetyFeatures.ToDictionary(entry => entry.Feature, entry => entry.IsConfirmed);
        var isMaterialChange =
            !string.Equals(AddressLine, details.AddressLine, StringComparison.Ordinal) || // BR-LST-05
            !string.Equals(Ward, details.Ward, StringComparison.Ordinal) ||
            !string.Equals(District, details.District, StringComparison.Ordinal) ||
            RoomType != details.RoomType ||
            !Latitude.Equals(details.Latitude) ||
            !Longitude.Equals(details.Longitude) ||
            details.HasAddedNewPhotos ||
            requestedSafetyFeatures.Any(feature => !confirmedByFeature.ContainsKey(feature));

        Title = (details.Title ?? string.Empty).Trim();
        Description = details.Description;
        RoomType = details.RoomType;
        MonthlyRentVnd = details.MonthlyRentVnd;
        DepositVnd = details.DepositVnd;
        AreaM2 = details.AreaM2;
        MaxOccupants = details.MaxOccupants;
        Furnishing = details.Furnishing;
        AvailableFrom = details.AvailableFrom;
        AddressLine = details.AddressLine;
        Ward = details.Ward;
        District = details.District;
        Latitude = details.Latitude;
        Longitude = details.Longitude;
        ElectricityPriceVndPerKwh = details.ElectricityPriceVndPerKwh;
        WaterPriceVnd = details.WaterPriceVnd;
        WaterPriceUnit = details.WaterPriceUnit;
        InternetFeeVnd = details.InternetFeeVnd;
        ParkingFeeVnd = details.ParkingFeeVnd;
        OtherFeesNote = details.OtherFeesNote;
        Amenities = requestedAmenities;
        SafetyFeatures = requestedSafetyFeatures
            .Select(feature => new RoomSafetyFeatureEntry(
                feature,
                confirmedByFeature.TryGetValue(feature, out var isConfirmed) && isConfirmed))
            .ToArray();

        if (Status == ListingStatus.Rejected)
        {
            Status = ListingStatus.PendingReview;
            StatusReason = null;
            NeedsReReview = false;
        }
        else if (Status is ListingStatus.Available or ListingStatus.Rented or ListingStatus.Unpublished)
        {
            if (isMaterialChange)
            {
                NeedsReReview = true;
            }
        }
        else if (Status == ListingStatus.PendingReview)
        {
            NeedsReReview = false;
        }

        EnsureInvariants();
        Touch();
    }

    // Amenities and safety features share the same whitelist + de-duplicate + keep-order loop.
    private static IReadOnlyList<TEnum> NormalizeEnumList<TEnum>(
        IEnumerable<TEnum>? values,
        string label,
        string errorCode)
        where TEnum : struct, Enum
    {
        if (values is null)
        {
            return Array.Empty<TEnum>();
        }

        var seen = new HashSet<TEnum>();
        var result = new List<TEnum>();
        foreach (var value in values)
        {
            if (!Enum.IsDefined(value))
            {
                // GUARDRAILS 1.5.1 whitelist / BR-LST-08
                throw new DomainValidationException(
                    $"{label} '{Convert.ToInt32(value, CultureInfo.InvariantCulture)}' is not a recognized {label.ToLowerInvariant()}.",
                    errorCode);
            }

            if (seen.Add(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static IReadOnlyList<RoomSafetyFeatureEntry> NormalizeSafetyFeatureEntries(
        IEnumerable<RoomSafetyFeatureEntry>? safetyFeatures)
    {
        if (safetyFeatures is null)
        {
            return Array.Empty<RoomSafetyFeatureEntry>();
        }

        var seen = new HashSet<SafetyFeature>();
        var result = new List<RoomSafetyFeatureEntry>();
        foreach (var entry in safetyFeatures)
        {
            if (entry is null)
            {
                throw new DomainValidationException("Safety feature entry is required.");
            }

            if (!Enum.IsDefined(entry.Feature))
            {
                throw new DomainValidationException(
                    $"Safety feature '{(int)entry.Feature}' is not a recognized safety feature.", // BR-LST-08
                    "INVALID_SAFETY_FEATURE");
            }

            if (seen.Add(entry.Feature))
            {
                result.Add(entry);
            }
        }

        return result;
    }

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

        if (Description is { Length: > DescriptionMaxLength })
        {
            throw new DomainValidationException(
                $"Room description must be at most {DescriptionMaxLength} characters.", // BR-LST-02
                "INVALID_DESCRIPTION");
        }

        if (AddressLine.Length > AddressLineMaxLength)
        {
            throw new DomainValidationException(
                $"Room address line must be at most {AddressLineMaxLength} characters.", // BR-LST-01
                "INVALID_ADDRESS");
        }

        if (Ward.Length > WardMaxLength)
        {
            throw new DomainValidationException(
                $"Room ward must be at most {WardMaxLength} characters.", // BR-LST-01
                "INVALID_WARD");
        }

        if (District.Length > DistrictMaxLength)
        {
            throw new DomainValidationException(
                $"Room district must be at most {DistrictMaxLength} characters.", // BR-LST-01
                "INVALID_DISTRICT");
        }

        if (OtherFeesNote is { Length: > OtherFeesNoteMaxLength })
        {
            throw new DomainValidationException(
                $"Other fees note must be at most {OtherFeesNoteMaxLength} characters.", // BR-LST-03
                "INVALID_OTHER_FEES_NOTE");
        }

        if (!double.IsFinite(Latitude) || Latitude < ThaiNguyenMinLat || Latitude > ThaiNguyenMaxLat)
        {
            throw new DomainValidationException(
                $"Room latitude must be finite and inside Thái Nguyên province [{ThaiNguyenMinLat}, {ThaiNguyenMaxLat}].", // BR-LST-01
                "INVALID_LOCATION");
        }

        if (!double.IsFinite(Longitude) || Longitude < ThaiNguyenMinLng || Longitude > ThaiNguyenMaxLng)
        {
            throw new DomainValidationException(
                $"Room longitude must be finite and inside Thái Nguyên province [{ThaiNguyenMinLng}, {ThaiNguyenMaxLng}].", // BR-LST-01
                "INVALID_LOCATION");
        }

        if (ElectricityPriceVndPerKwh is < 0 or > 10_000)
        {
            throw new DomainValidationException("Electricity price must be between 0 and 10,000 VND per kWh."); // BR-LST-03
        }

        if (WaterPriceVnd is < 0)
        {
            throw new DomainValidationException("Water price must be zero or greater."); // BR-LST-03
        }

        if (InternetFeeVnd is < 0)
        {
            throw new DomainValidationException("Internet fee must be zero or greater."); // BR-LST-03
        }

        if (ParkingFeeVnd is < 0)
        {
            throw new DomainValidationException("Parking fee must be zero or greater."); // BR-LST-03
        }

        if (Furnishing is not null && !IsDefinedName<Furnishing>(Furnishing))
        {
            throw new DomainValidationException(
                $"Furnishing '{Furnishing}' is not a recognized furnishing level.", // GUARDRAILS 1.5.1
                "INVALID_FURNISHING");
        }

        if (WaterPriceUnit is not null && !IsDefinedName<WaterPriceUnit>(WaterPriceUnit))
        {
            throw new DomainValidationException(
                $"Water price unit '{WaterPriceUnit}' is not a recognized water price unit.", // GUARDRAILS 1.5.1
                "INVALID_WATER_PRICE_UNIT");
        }

        foreach (var amenity in Amenities)
        {
            if (!Enum.IsDefined(amenity))
            {
                throw new DomainValidationException($"Amenity '{(int)amenity}' is not a recognized amenity.");
            }
        }

        foreach (var safetyFeature in SafetyFeatures)
        {
            if (!Enum.IsDefined(safetyFeature.Feature))
            {
                throw new DomainValidationException(
                    $"Safety feature '{(int)safetyFeature.Feature}' is not a recognized safety feature.");
            }
        }
    }

    /// <summary>Whitelist of enum names (GUARDRAILS 1.5.1): rejects numbers and combined names.</summary>
    private static bool IsDefinedName<TEnum>(string value) where TEnum : struct, Enum =>
        Enum.GetNames<TEnum>().Contains(value, StringComparer.Ordinal);
}
