using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

/// <summary>
/// SQLite storage of <see cref="RoomPhoto"/> against the ERD §4.8 <c>RoomPhotos</c> table. Every query
/// is scoped by the uploader (anti-IDOR, GUARDRAILS §1.5.6) and the attach uses the race-safe
/// conditional UPDATE of ERD §4.8. Enums are TEXT names, date-times ISO-8601 round-trip ("O") strings.
/// Commands join the ambient <see cref="SqliteAmbientTransaction"/> when a unit of work is active.
/// </summary>
public sealed class SqliteRoomPhotoRepository : IRoomPhotoRepository
{
    private const string SelectColumns = """
        Id, RoomId, UploadedByUserId, Kind, ContentType, SizeBytes, Width, Height,
        OriginalPath, MediumPath, ThumbPath, SortOrder, CreatedAtUtc, AttachedAtUtc
        """;

    private readonly SqliteDbContext _dbContext;

    public SqliteRoomPhotoRepository(SqliteDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task AddAsync(RoomPhoto photo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(photo);

        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = """
            INSERT INTO RoomPhotos (
                Id, RoomId, UploadedByUserId, Kind, ContentType, SizeBytes, Width, Height,
                OriginalPath, MediumPath, ThumbPath, SortOrder, CreatedAtUtc, AttachedAtUtc
            ) VALUES (
                @id, @roomId, @uploadedByUserId, @kind, @contentType, @sizeBytes, @width, @height,
                @originalPath, @mediumPath, @thumbPath, @sortOrder, @createdAtUtc, @attachedAtUtc
            );
            """;
        BindPhotoParameters(cmd, photo);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<RoomPhoto?> GetOwnedAsync(string photoId, string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(photoId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        return await ReadOwnedAsync(lease, photoId, userId, ct);
    }

    /// <summary>
    /// BR-MED-03: only the uploader can delete, and only while the photo is still unattached. The row is
    /// returned so the caller can remove the physical files after a successful delete.
    /// </summary>
    public async Task<PhotoDeleteAttempt> DeleteUnattachedAsync(
        string photoId,
        string userId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(photoId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        using var lease = SqliteConnectionLease.Acquire(_dbContext);

        var photo = await ReadOwnedAsync(lease, photoId, userId, ct);
        if (photo is null)
        {
            return new PhotoDeleteAttempt(PhotoDeleteOutcome.NotFound, Photo: null);
        }

        if (photo.RoomId is not null)
        {
            return new PhotoDeleteAttempt(PhotoDeleteOutcome.Attached, photo);
        }

        await using var cmd = lease.CreateCommand();
        cmd.CommandText = "DELETE FROM RoomPhotos WHERE Id = @id AND UploadedByUserId = @userId AND RoomId IS NULL;";
        cmd.Parameters.AddWithValue("@id", photoId);
        cmd.Parameters.AddWithValue("@userId", userId);
        var deletedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (deletedRows > 0)
        {
            return new PhotoDeleteAttempt(PhotoDeleteOutcome.Deleted, photo);
        }

        // The row changed after the read: either it was attached concurrently or another caller
        // deleted it first. Re-read so the outcome is the one the caller must act on.
        var current = await ReadOwnedAsync(lease, photoId, userId, ct);
        return current is null
            ? new PhotoDeleteAttempt(PhotoDeleteOutcome.NotFound, Photo: null)
            : new PhotoDeleteAttempt(PhotoDeleteOutcome.Attached, current);
    }

    public async Task<IReadOnlyList<RoomPhoto>> ListExpiredUnattachedAsync(
        DateTime cutoffUtc,
        CancellationToken ct = default)
    {
        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        await using var cmd = lease.CreateCommand();
        // ISO-8601 "O" strings sort lexicographically in the same order as the instants they encode.
        cmd.CommandText = """
            SELECT Id, RoomId, UploadedByUserId, Kind, ContentType, SizeBytes, Width, Height,
                   OriginalPath, MediumPath, ThumbPath, SortOrder, CreatedAtUtc, AttachedAtUtc
            FROM RoomPhotos
            WHERE RoomId IS NULL AND CreatedAtUtc < @cutoffUtc
            ORDER BY CreatedAtUtc;
            """;
        cmd.Parameters.AddWithValue("@cutoffUtc", ToUtcIso(cutoffUtc));

        return await ReadPhotosAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<RoomPhoto>> ListByRoomAsync(string roomId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);

        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        return await ReadByRoomAsync(lease, roomId, ct);
    }

    public async Task<IReadOnlyList<RoomPhoto>> ListOwnedByIdsAsync(
        string userId,
        IReadOnlyCollection<string> photoIds,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(photoIds);

        if (photoIds.Count == 0)
        {
            return Array.Empty<RoomPhoto>();
        }

        using var lease = SqliteConnectionLease.Acquire(_dbContext);
        await using var cmd = lease.CreateCommand();

        var idParameters = new List<string>(photoIds.Count);
        var index = 0;
        foreach (var photoId in photoIds)
        {
            var name = "@photoId" + index.ToString(CultureInfo.InvariantCulture);
            idParameters.Add(name);
            cmd.Parameters.AddWithValue(name, photoId);
            index++;
        }

        cmd.CommandText = $"""
            SELECT {SelectColumns}
            FROM RoomPhotos
            WHERE UploadedByUserId = @userId AND Id IN ({string.Join(", ", idParameters)})
            ORDER BY Id;
            """;
        cmd.Parameters.AddWithValue("@userId", userId);

        return await ReadPhotosAsync(cmd, ct);
    }

    /// <summary>
    /// ERD §4.8 attach: the conditional UPDATE only succeeds while the photo is still owned and either
    /// unattached or already on this room, so two concurrent creates cannot steal the same photo. Any id
    /// that affects 0 rows makes the whole call fail — inside an <c>IUnitOfWork</c> everything rolls back.
    /// </summary>
    public async Task AttachAsync(
        string userId,
        string roomId,
        IReadOnlyList<string> photoIds,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(photoIds);

        var attachedAtUtc = DateTime.UtcNow;
        using var lease = SqliteConnectionLease.Acquire(_dbContext);

        for (var sortOrder = 0; sortOrder < photoIds.Count; sortOrder++)
        {
            var photoId = photoIds[sortOrder];
            await using var cmd = lease.CreateCommand();
            cmd.CommandText = """
                UPDATE RoomPhotos
                SET RoomId = @roomId, SortOrder = @sortOrder, AttachedAtUtc = @attachedAtUtc
                WHERE Id = @id AND UploadedByUserId = @userId AND (RoomId IS NULL OR RoomId = @roomId);
                """;
            cmd.Parameters.AddWithValue("@id", photoId);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@roomId", roomId);
            cmd.Parameters.AddWithValue("@sortOrder", sortOrder);
            cmd.Parameters.AddWithValue("@attachedAtUtc", attachedAtUtc.ToString("O", CultureInfo.InvariantCulture));

            var rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
            if (rowsAffected == 0)
            {
                throw new DomainValidationException(
                    $"Photo '{photoId}' is not owned, expired or already attached to another room.",
                    "VALIDATION_FAILED");
            }
        }
    }

    /// <summary>
    /// Removes the room's photos that are no longer referenced by the edited listing (updateListing).
    /// The rows are returned — ordered by SortOrder — so the caller can delete the physical files.
    /// </summary>
    public async Task<IReadOnlyList<RoomPhoto>> RemoveNotInListAsync(
        string roomId,
        IReadOnlyCollection<string> keepPhotoIds,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(keepPhotoIds);

        var keep = new HashSet<string>(keepPhotoIds, StringComparer.Ordinal);
        using var lease = SqliteConnectionLease.Acquire(_dbContext);

        var current = await ReadByRoomAsync(lease, roomId, ct);
        var toRemove = current.Where(photo => !keep.Contains(photo.Id)).ToList();
        if (toRemove.Count == 0)
        {
            return Array.Empty<RoomPhoto>();
        }

        foreach (var photo in toRemove)
        {
            await using var cmd = lease.CreateCommand();
            cmd.CommandText = "DELETE FROM RoomPhotos WHERE Id = @id AND RoomId = @roomId;";
            cmd.Parameters.AddWithValue("@id", photo.Id);
            cmd.Parameters.AddWithValue("@roomId", roomId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return toRemove;
    }

    // ----------------------------------------------------------------- helpers

    private static async Task<RoomPhoto?> ReadOwnedAsync(
        SqliteConnectionLease lease,
        string photoId,
        string userId,
        CancellationToken ct)
    {
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = $"""
            SELECT {SelectColumns}
            FROM RoomPhotos
            WHERE Id = @id AND UploadedByUserId = @userId
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@id", photoId);
        cmd.Parameters.AddWithValue("@userId", userId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapPhoto(reader) : null;
    }

    private static async Task<IReadOnlyList<RoomPhoto>> ReadByRoomAsync(
        SqliteConnectionLease lease,
        string roomId,
        CancellationToken ct)
    {
        await using var cmd = lease.CreateCommand();
        cmd.CommandText = $"""
            SELECT {SelectColumns}
            FROM RoomPhotos
            WHERE RoomId = @roomId
            ORDER BY SortOrder, Id;
            """;
        cmd.Parameters.AddWithValue("@roomId", roomId);

        return await ReadPhotosAsync(cmd, ct);
    }

    private static async Task<IReadOnlyList<RoomPhoto>> ReadPhotosAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var photos = new List<RoomPhoto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            photos.Add(MapPhoto(reader));
        }

        return photos;
    }

    private static void BindPhotoParameters(SqliteCommand cmd, RoomPhoto photo)
    {
        cmd.Parameters.AddWithValue("@id", photo.Id);
        cmd.Parameters.AddWithValue("@roomId", (object?)photo.RoomId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@uploadedByUserId", photo.UploadedByUserId);
        cmd.Parameters.AddWithValue("@kind", photo.Kind.ToString());
        cmd.Parameters.AddWithValue("@contentType", photo.ContentType);
        cmd.Parameters.AddWithValue("@sizeBytes", photo.SizeBytes);
        cmd.Parameters.AddWithValue("@width", photo.Width);
        cmd.Parameters.AddWithValue("@height", photo.Height);
        cmd.Parameters.AddWithValue("@originalPath", photo.OriginalPath);
        cmd.Parameters.AddWithValue("@mediumPath", photo.MediumPath);
        cmd.Parameters.AddWithValue("@thumbPath", photo.ThumbPath);
        cmd.Parameters.AddWithValue("@sortOrder", photo.SortOrder);
        cmd.Parameters.AddWithValue("@createdAtUtc", photo.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@attachedAtUtc", photo.AttachedAtUtc.HasValue
            ? photo.AttachedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture)
            : DBNull.Value);
    }

    private static RoomPhoto MapPhoto(SqliteDataReader reader)
    {
        // Column order matches SelectColumns / BindPhotoParameters exactly.
        return RoomPhoto.Reconstitute(
            id: reader.GetString(0),
            roomId: reader.IsDBNull(1) ? null : reader.GetString(1),
            uploadedByUserId: reader.GetString(2),
            kind: Enum.Parse<PhotoKind>(reader.GetString(3), ignoreCase: false),
            contentType: reader.GetString(4),
            sizeBytes: reader.GetInt64(5),
            width: reader.GetInt32(6),
            height: reader.GetInt32(7),
            originalPath: reader.GetString(8),
            mediumPath: reader.GetString(9),
            thumbPath: reader.GetString(10),
            sortOrder: reader.GetInt32(11),
            createdAtUtc: ParseUtc(reader.GetString(12)),
            attachedAtUtc: reader.IsDBNull(13) ? null : ParseUtc(reader.GetString(13)));
    }

    private static DateTime ParseUtc(string iso8601) =>
        DateTime.Parse(iso8601, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string ToUtcIso(DateTime value) =>
        (value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc))
        .ToString("O", CultureInfo.InvariantCulture);
}
