using System.Text.RegularExpressions;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Entities;

/// <summary>
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

    public HouseRuleVersion(
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
        CreatedAtUtc = AsUtc(createdAtUtc);
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

    /// <summary>Rebuilds a row from persistence or a test fixture; every column of ERD §4.7 is accepted.</summary>
    public static HouseRuleVersion Reconstitute(
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
        return new HouseRuleVersion(
            id,
            roomId,
            versionNumber,
            guestPolicy,
            cookingPolicy,
            parkingAvailable,
            petsAllowed,
            smokingAllowed,
            createdByUserId,
            createdAtUtc,
            quietHoursStart,
            quietHoursEnd,
            curfewTime,
            guestPolicyNote,
            parkingRules,
            otherRules);
    }

    private static string? NormalizeTimeOfDay(string? value, string fieldName)
    {
        var normalized = NormalizeOptional(value, maxLength: null, fieldName);
        if (normalized is not null && !TimeOfDayPattern.IsMatch(normalized))
        {
            throw new DomainValidationException($"{fieldName} must match HH:mm (00:00–23:59).", "INVALID_TIME_FORMAT"); // BR-HR-01
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
            var code = fieldName switch
            {
                "Parking rules" => "PARKING_RULES_TOO_LONG",
                "Other rules" => "OTHER_RULES_TOO_LONG",
                "Guest policy note" => "GUEST_POLICY_NOTE_TOO_LONG",
                _ => null
            };
            throw new DomainValidationException($"{fieldName} must be at most {maxLength.Value} characters.", code); // BR-HR-01
        }

        return trimmed;
    }

    /// <summary>ERD §1: every date-time column is stored and exposed as UTC.</summary>
    private static DateTime AsUtc(DateTime instant) =>
        instant.Kind switch
        {
            DateTimeKind.Utc => instant,
            DateTimeKind.Local => instant.ToUniversalTime(),
            _ => DateTime.SpecifyKind(instant, DateTimeKind.Utc)
        };
}
