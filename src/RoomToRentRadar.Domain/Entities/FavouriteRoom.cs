namespace RoomToRentRadar.Domain.Entities;

/// <summary>
/// Represents a user's saved favourite rental room in the domain layer (US06 / T06.1).
/// </summary>
public sealed class FavouriteRoom
{
    public const int MaxRoomIdLength = 128;

    private static readonly char[] ForbiddenRoomIdChars = ['/', '\\', '?', '#'];

    public string Id { get; }

    public Guid UserId { get; }

    /// <summary>
    /// Canonical Room ID preserving the original casing from the Room entity.
    /// </summary>
    public string RoomId { get; }

    /// <summary>
    /// Lowercase normalized Room ID used for deterministic document IDs and case-insensitive deduplication.
    /// </summary>
    public string NormalizedRoomId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    private FavouriteRoom(
        string id,
        Guid userId,
        string roomId,
        string normalizedRoomId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        UserId = userId;
        RoomId = roomId;
        NormalizedRoomId = normalizedRoomId;
        CreatedAtUtc = createdAtUtc;
    }

    public static FavouriteRoom Create(
        Guid userId,
        string canonicalRoomId,
        DateTimeOffset createdAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        }

        if (!TryValidateRoomId(canonicalRoomId, out var trimmedRoomId, out var errorMessage))
        {
            throw new ArgumentException(errorMessage, nameof(canonicalRoomId));
        }

        var normalizedRoomId = trimmedRoomId.ToLowerInvariant();
        var id = BuildIdFromNormalized(userId, normalizedRoomId);

        return new FavouriteRoom(
            id,
            userId,
            trimmedRoomId,
            normalizedRoomId,
            createdAtUtc);
    }

    public static string BuildId(Guid userId, string roomId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        }

        if (!TryValidateRoomId(roomId, out var trimmedRoomId, out var errorMessage))
        {
            throw new ArgumentException(errorMessage, nameof(roomId));
        }

        return BuildIdFromNormalized(userId, trimmedRoomId.ToLowerInvariant());
    }

    public static bool TryValidateRoomId(
        string? roomId,
        out string trimmedRoomId,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            trimmedRoomId = string.Empty;
            errorMessage = "RoomId must not be empty or whitespace.";
            return false;
        }

        trimmedRoomId = roomId.Trim();

        if (trimmedRoomId.Length > MaxRoomIdLength)
        {
            errorMessage = $"RoomId must not exceed {MaxRoomIdLength} characters.";
            return false;
        }

        foreach (var ch in trimmedRoomId)
        {
            if (char.IsControl(ch) || Array.IndexOf(ForbiddenRoomIdChars, ch) >= 0)
            {
                errorMessage = "RoomId contains invalid or forbidden characters ('/', '\\', '?', '#', or control characters).";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    private static string BuildIdFromNormalized(Guid userId, string normalizedRoomId) =>
        $"{userId:D}:{normalizedRoomId}";
}
