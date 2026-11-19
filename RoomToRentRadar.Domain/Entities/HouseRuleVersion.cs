using System.Text.RegularExpressions;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Entities;

/// <summary>
<<<<<<< HEAD
/// An immutable versioned record of house rules for a room listing (<c>docs/erd.md</c> §4.7, BR-HR-01..02).
/// Invariants enforce BR-HR-01 field length and time format rules.
/// </summary>
public sealed class HouseRuleVersion
{
    private static readonly Regex TimePattern = new(
        @"^([01]\d|2[0-3]):[0-5]\d$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const int ParkingRulesMaxLength = 300;
    public const int OtherRulesMaxLength = 1000;

=======
/// One immutable version of a room's house rules (<c>docs/erd.md</c> §4.7, BR-HR-01..02).
/// The current rules of a room are the row with the highest <see cref="VersionNumber"/>.
/// </summary>
public sealed class HouseRuleVersion
{
    public const int GuestPolicyNoteMaxLength = 200;
    public const int ParkingRulesMaxLength = 300;
    public const int OtherRulesMaxLength = 1000;

    private static readonly Regex TimeOfDayPattern =
        new("^([01][0-9]|2[0-3]):[0-5][0-9]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

>>>>>>> 37726835fc6f10144470956f6bb02e9d68177239
    public string Id { get; }
    public string RoomId { get; }
    public int VersionNumber { get; }
    public string? QuietHoursStart { get; }
    public string? QuietHoursEnd { get; }
    public string? CurfewTime { get; }
    public GuestPolicy GuestPolicy { get; }
    public string? GuestPolicyNote { get; }
    public CookingPolicy CookingPolicy { get; }
    public bool ParkingAvailable { get; }
    public string? ParkingRules { get; }
    public bool PetsAllowed { get; }
    public bool SmokingAllowed { get; }
    public string? OtherRules { get; }
    public string CreatedByUserId { get; }
    public DateTime CreatedAtUtc { get; }

<<<<<<< HEAD
    public HouseRuleVersion(
=======
    private HouseRuleVersion(
        string id,
        string roomId,
        int versionNumber,
        GuestPolicy guestPolicy,
        CookingPolicy cookingPolicy,
        bool parkingAvailable,
        string? parkingRules,
        bool petsAllowed,
        bool smokingAllowed,
        string? otherRules,
        string createdByUserId,
        DateTime createdAtUtc,
        string? quietHoursStart,
        string? quietHoursEnd,
        string? curfewTime,
        string? guestPolicyNote)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DomainValidationException("House rule version Id is required.");
        }

        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new DomainValidationException("House rule version room is required.");
        }

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new DomainValidationException("House rule version author is required.");
        }

        if (versionNumber < 1)
        {
            throw new DomainValidationException("House rule version number must be at least 1."); // BR-HR-02
        }

        if (!Enum.IsDefined(guestPolicy))
        {
            throw new DomainValidationException($"Guest policy '{(int)guestPolicy}' is not a recognized guest policy.");
        }

        if (!Enum.IsDefined(cookingPolicy))
        {
            throw new DomainValidationException($"Cooking policy '{(int)cookingPolicy}' is not a recognized cooking policy.");
        }

        QuietHoursStart = NormalizeTimeOfDay(quietHoursStart, "Quiet hours start");
        QuietHoursEnd = NormalizeTimeOfDay(quietHoursEnd, "Quiet hours end");
        CurfewTime = NormalizeTimeOfDay(curfewTime, "Curfew time");
        GuestPolicyNote = NormalizeOptional(guestPolicyNote, GuestPolicyNoteMaxLength, "Guest policy note");
        ParkingRules = NormalizeOptional(parkingRules, ParkingRulesMaxLength, "Parking rules");
        OtherRules = NormalizeOptional(otherRules, OtherRulesMaxLength, "Other rules");

        Id = id.Trim();
        RoomId = roomId.Trim();
        VersionNumber = versionNumber;
        GuestPolicy = guestPolicy;
        CookingPolicy = cookingPolicy;
        ParkingAvailable = parkingAvailable;
        PetsAllowed = petsAllowed;
        SmokingAllowed = smokingAllowed;
        CreatedByUserId = createdByUserId.Trim();
        CreatedAtUtc = AsUtc(createdAtUtc)!.Value;
    }

    /// <summary>Creates version 1 (or a later version) of a room's house rules.</summary>
    public static HouseRuleVersion Create(
        string roomId,
        int versionNumber,
        GuestPolicy guestPolicy,
        CookingPolicy cookingPolicy,
        bool parkingAvailable,
        bool petsAllowed,
        bool smokingAllowed,
        string createdByUserId,
        DateTime? utcNow = null,
        string? quietHoursStart = null,
        string? quietHoursEnd = null,
        string? curfewTime = null,
        string? guestPolicyNote = null,
        string? parkingRules = null,
        string? otherRules = null)
    {
        return new HouseRuleVersion(
            id: Guid.NewGuid().ToString("N"),
            roomId: roomId,
            versionNumber: versionNumber,
            guestPolicy: guestPolicy,
            cookingPolicy: cookingPolicy,
            parkingAvailable: parkingAvailable,
            parkingRules: parkingRules,
            petsAllowed: petsAllowed,
            smokingAllowed: smokingAllowed,
            otherRules: otherRules,
            createdByUserId: createdByUserId,
            createdAtUtc: utcNow ?? DateTime.UtcNow,
            quietHoursStart: quietHoursStart,
            quietHoursEnd: quietHoursEnd,
            curfewTime: curfewTime,
            guestPolicyNote: guestPolicyNote);
    }

    /// <summary>Rebuilds a row from persistence or a test fixture; every column of ERD §4.7 is accepted.</summary>
    public static HouseRuleVersion Reconstitute(
>>>>>>> 37726835fc6f10144470956f6bb02e9d68177239
        string id,
        string roomId,
        int versionNumber,
        GuestPolicy guestPolicy,
        CookingPolicy cookingPolicy,
        bool parkingAvailable,
        bool petsAllowed,
        bool smokingAllowed,
        string createdByUserId,
        DateTime createdAtUtc,
        string? quietHoursStart = null,
        string? quietHoursEnd = null,
        string? curfewTime = null,
        string? guestPolicyNote = null,
        string? parkingRules = null,
        string? otherRules = null)
    {
<<<<<<< HEAD
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DomainValidationException("House rule version Id is required.");
        }

        if (string.IsNullOrWhiteSpace(roomId))
        {
            throw new DomainValidationException("Room Id is required.");
        }

        if (versionNumber < 1)
        {
            throw new DomainValidationException("Version number must be at least 1.");
        }

        if (!Enum.IsDefined(guestPolicy))
        {
            throw new DomainValidationException($"Guest policy '{(int)guestPolicy}' is not recognized.");
        }

        if (!Enum.IsDefined(cookingPolicy))
        {
            throw new DomainValidationException($"Cooking policy '{(int)cookingPolicy}' is not recognized.");
        }

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new DomainValidationException("CreatedByUserId is required.");
        }

        ValidateTimeFormat(quietHoursStart, nameof(QuietHoursStart));
        ValidateTimeFormat(quietHoursEnd, nameof(QuietHoursEnd));
        ValidateTimeFormat(curfewTime, nameof(CurfewTime));

        if (parkingRules != null && parkingRules.Length > ParkingRulesMaxLength)
        {
            throw new DomainValidationException(
                $"Parking rules must not exceed {ParkingRulesMaxLength} characters.",
                "PARKING_RULES_TOO_LONG");
        }

        if (otherRules != null && otherRules.Length > OtherRulesMaxLength)
        {
            throw new DomainValidationException(
                $"Other rules must not exceed {OtherRulesMaxLength} characters.",
                "OTHER_RULES_TOO_LONG");
        }

        Id = id.Trim();
        RoomId = roomId.Trim();
        VersionNumber = versionNumber;
        QuietHoursStart = quietHoursStart?.Trim();
        QuietHoursEnd = quietHoursEnd?.Trim();
        CurfewTime = curfewTime?.Trim();
        GuestPolicy = guestPolicy;
        GuestPolicyNote = string.IsNullOrWhiteSpace(guestPolicyNote) ? null : guestPolicyNote.Trim();
        CookingPolicy = cookingPolicy;
        ParkingAvailable = parkingAvailable;
        ParkingRules = string.IsNullOrWhiteSpace(parkingRules) ? null : parkingRules.Trim();
        PetsAllowed = petsAllowed;
        SmokingAllowed = smokingAllowed;
        OtherRules = string.IsNullOrWhiteSpace(otherRules) ? null : otherRules.Trim();
        CreatedByUserId = createdByUserId.Trim();
        CreatedAtUtc = AsUtc(createdAtUtc);
    }

    public static HouseRuleVersion Create(
        string roomId,
        int versionNumber,
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
        return new HouseRuleVersion(
            id: Guid.NewGuid().ToString("N"),
            roomId: roomId,
            versionNumber: versionNumber,
            guestPolicy: guestPolicy,
            cookingPolicy: cookingPolicy,
            parkingAvailable: parkingAvailable,
            petsAllowed: petsAllowed,
            smokingAllowed: smokingAllowed,
            createdByUserId: createdByUserId,
            createdAtUtc: utcNow ?? DateTime.UtcNow,
            quietHoursStart: quietHoursStart,
            quietHoursEnd: quietHoursEnd,
            curfewTime: curfewTime,
            guestPolicyNote: guestPolicyNote,
            parkingRules: parkingRules,
            otherRules: otherRules);
    }

    private static void ValidateTimeFormat(string? time, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(time))
        {
            return;
        }

        if (!TimePattern.IsMatch(time.Trim()))
        {
            throw new DomainValidationException(
                $"{fieldName} '{time}' must be in 'HH:mm' 24-hour format (e.g. '22:00').",
                "INVALID_TIME_FORMAT");
        }
    }

    private static DateTime AsUtc(DateTime instant) =>
        instant.Kind switch
=======
        return new HouseRuleVersion(
            id,
            roomId,
            versionNumber,
            guestPolicy,
            cookingPolicy,
            parkingAvailable,
            parkingRules,
            petsAllowed,
            smokingAllowed,
            otherRules,
            createdByUserId,
            createdAtUtc,
            quietHoursStart,
            quietHoursEnd,
            curfewTime,
            guestPolicyNote);
    }

    private static string? NormalizeTimeOfDay(string? value, string fieldName)
    {
        var normalized = NormalizeOptional(value, maxLength: null, fieldName);
        if (normalized is not null && !TimeOfDayPattern.IsMatch(normalized))
        {
            throw new DomainValidationException($"{fieldName} must match HH:mm (00:00–23:59)."); // BR-HR-01
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int? maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (maxLength.HasValue && trimmed.Length > maxLength.Value)
        {
            throw new DomainValidationException($"{fieldName} must be at most {maxLength.Value} characters."); // BR-HR-01
        }

        return trimmed;
    }

    /// <summary>ERD §1: every date-time column is stored and exposed as UTC.</summary>
    private static DateTime? AsUtc(DateTime? value)
    {
        if (value is not { } instant)
        {
            return null;
        }

        return instant.Kind switch
>>>>>>> 37726835fc6f10144470956f6bb02e9d68177239
        {
            DateTimeKind.Utc => instant,
            DateTimeKind.Local => instant.ToUniversalTime(),
            _ => DateTime.SpecifyKind(instant, DateTimeKind.Utc)
        };
<<<<<<< HEAD
=======
    }
>>>>>>> 37726835fc6f10144470956f6bb02e9d68177239
}
