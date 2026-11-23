using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

namespace RoomToRentRadar.Domain.Entities;

/// <summary>
/// Aggregate Root managing versioned house rules and building regulations for a rental room (US11).
/// </summary>
public sealed class RoomHouseRules
{
    public const int MaxChangeNoteLength = 250;

    public string Id { get; }
    public string RoomId { get; }
    public Guid? LandlordId { get; private set; }
    public int CurrentVersion { get; private set; }
    public HouseRulesPolicy CurrentPolicy { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public string? ETag { get; private set; }

    private RoomHouseRules(
        string id,
        string roomId,
        Guid? landlordId,
        int currentVersion,
        HouseRulesPolicy currentPolicy,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId,
        string? etag = null)
    {
        Id = id;
        RoomId = roomId;
        LandlordId = landlordId;
        CurrentVersion = currentVersion;
        CurrentPolicy = currentPolicy;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
        ETag = etag;
    }

    public static string BuildId(string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        return $"rules:{roomId.Trim()}";
    }

    public static (RoomHouseRules Rules, HouseRulesRevision InitialRevision) CreateInitial(
        string roomId,
        Guid? landlordId,
        HouseRulesPolicy policy,
        Guid actorUserId,
        bool isAdmin,
        string? changeNote,
        DateTimeOffset utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(policy);

        if (actorUserId == Guid.Empty)
        {
            throw new HouseRulesValidationException("Actor user identifier is required.");
        }

        EnsureCanModify(landlordId, actorUserId, isAdmin);

        var normalizedRoomId = roomId.Trim();
        var normalizedNote = NormalizeChangeNote(changeNote) ?? "Initial house rules";

        var rules = new RoomHouseRules(
            id: BuildId(normalizedRoomId),
            roomId: normalizedRoomId,
            landlordId: landlordId,
            currentVersion: 1,
            currentPolicy: policy,
            updatedAtUtc: utcNow,
            updatedByUserId: actorUserId);

        var revision = new HouseRulesRevision(
            roomId: normalizedRoomId,
            version: 1,
            policy: policy,
            changeNote: normalizedNote,
            updatedByUserId: actorUserId,
            updatedAtUtc: utcNow);

        return (rules, revision);
    }

    public static RoomHouseRules Rehydrate(
        string id,
        string roomId,
        Guid? landlordId,
        int currentVersion,
        HouseRulesPolicy currentPolicy,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId,
        string? etag = null)
    {
        if (currentVersion < 1)
        {
            throw new HouseRulesValidationException("House rules version must be at least 1.");
        }

        return new RoomHouseRules(
            id,
            roomId,
            landlordId,
            currentVersion,
            currentPolicy,
            updatedAtUtc,
            updatedByUserId,
            etag);
    }

    public HouseRulesRevision UpdatePolicy(
        HouseRulesPolicy newPolicy,
        Guid actorUserId,
        bool isAdmin,
        string? changeNote,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(newPolicy);

        if (actorUserId == Guid.Empty)
        {
            throw new HouseRulesValidationException("Actor user identifier is required.");
        }

        EnsureCanModify(actorUserId, isAdmin);

        CurrentVersion += 1;
        CurrentPolicy = newPolicy;
        UpdatedAtUtc = utcNow;
        UpdatedByUserId = actorUserId;

        return new HouseRulesRevision(
            roomId: RoomId,
            version: CurrentVersion,
            policy: newPolicy,
            changeNote: NormalizeChangeNote(changeNote),
            updatedByUserId: actorUserId,
            updatedAtUtc: utcNow);
    }

    public void EnsureCanModify(Guid actorUserId, bool isAdmin) =>
        EnsureCanModify(LandlordId, actorUserId, isAdmin);

    private static void EnsureCanModify(Guid? landlordId, Guid actorUserId, bool isAdmin)
    {
        if (isAdmin)
        {
            return;
        }

        if (landlordId is null || landlordId == Guid.Empty)
        {
            throw new HouseRulesOwnershipException("This room does not have an assigned landlord owner.");
        }

        if (landlordId.Value != actorUserId)
        {
            throw new HouseRulesOwnershipException("Only the landlord who owns this room can update its house rules.");
        }
    }

    private static string? NormalizeChangeNote(string? changeNote)
    {
        if (string.IsNullOrWhiteSpace(changeNote))
        {
            return null;
        }

        var trimmed = changeNote.Trim();
        if (trimmed.Length > MaxChangeNoteLength)
        {
            throw new HouseRulesValidationException($"Change note must not exceed {MaxChangeNoteLength} characters.");
        }

        return trimmed;
    }
}

public sealed record HouseRulesRevision
{
    public string RoomId { get; }
    public int Version { get; }
    public HouseRulesPolicy Policy { get; }
    public string? ChangeNote { get; }
    public Guid UpdatedByUserId { get; }
    public DateTimeOffset UpdatedAtUtc { get; }

    public HouseRulesRevision(
        string roomId,
        int version,
        HouseRulesPolicy policy,
        string? changeNote,
        Guid updatedByUserId,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(policy);

        if (version < 1)
        {
            throw new HouseRulesValidationException("Revision version must be at least 1.");
        }

        RoomId = roomId;
        Version = version;
        Policy = policy;
        ChangeNote = changeNote;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = updatedAtUtc;
    }
}
