using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

public sealed class SqliteRoomPhotoRepository : IRoomPhotoRepository
{
    private const string SelectColumns = """
        SELECT Id, RoomId, UploadedByUserId, Kind, ContentType, SizeBytes, Width, Height,
               OriginalPath, MediumPath, ThumbPath, SortOrder, CreatedAtUtc, AttachedAtUtc
        FROM RoomPhotos
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

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO RoomPhotos (
                    Id, RoomId, UploadedByUserId, Kind, ContentType, SizeBytes, Width, Height,
                    OriginalPath, MediumPath, ThumbPath, SortOrder, CreatedAtUtc, AttachedAtUtc
                ) VALUES (
                    @id, @roomId, @uploadedByUserId, @kind, @contentType, @sizeBytes, @width, @height,
                    @originalPath, @mediumPath, @thumbPath, @sortOrder, @createdAtUtc, @attachedAtUtc
                );
                """;

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
            cmd.Parameters.AddWithValue(
                "@attachedAtUtc",
                (object?)photo.AttachedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task<RoomPhoto?> GetOwnedAsync(string photoId, string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(photoId);
        ArgumentNullException.ThrowIfNull(userId);

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            // Anti-IDOR: the uploader is part of the key, so a photo of another user is invisible.
            cmd.CommandText = $"""
                {SelectColumns}
                WHERE Id = @photoId AND UploadedByUserId = @userId
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("@photoId", photoId);
            cmd.Parameters.AddWithValue("@userId", userId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? ReadRow(reader) : null;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task<bool> DeleteUnattachedAsync(string photoId, string userId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(photoId);
        ArgumentNullException.ThrowIfNull(userId);

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            // Anti-IDOR + BR-MED-03: only the owner may delete, and only while not attached.
            cmd.CommandText = """
                DELETE FROM RoomPhotos
                WHERE Id = @photoId AND UploadedByUserId = @userId AND RoomId IS NULL;
                """;
            cmd.Parameters.AddWithValue("@photoId", photoId);
            cmd.Parameters.AddWithValue("@userId", userId);

            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task<IReadOnlyList<RoomPhoto>> ListByRoomAsync(string roomId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roomId);

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                {SelectColumns}
                WHERE RoomId = @roomId
                ORDER BY SortOrder ASC;
                """;
            cmd.Parameters.AddWithValue("@roomId", roomId);

            var photos = new List<RoomPhoto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                photos.Add(ReadRow(reader));
            }

            return photos;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task<int> AttachAsync(
        string userId,
        string roomId,
        IReadOnlyList<string> orderedIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(orderedIds);

        if (orderedIds.Count == 0)
        {
            return 0;
        }

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            // One transaction so a partial attach never leaves half of the gallery linked.
            using var transaction = conn.BeginTransaction();
            var attachedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var updated = 0;

            for (var index = 0; index < orderedIds.Count; index++)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                // Anti-IDOR + race-safe attach (ERD 4.8): only an owned photo that is free or
                // already in this room is updated; anything else leaves the row untouched.
                cmd.CommandText = """
                    UPDATE RoomPhotos
                    SET RoomId = @roomId, SortOrder = @sortOrder, AttachedAtUtc = @attachedAtUtc
                    WHERE Id = @id AND UploadedByUserId = @userId AND (RoomId IS NULL OR RoomId = @roomId);
                    """;
                cmd.Parameters.AddWithValue("@roomId", roomId);
                cmd.Parameters.AddWithValue("@sortOrder", index);
                cmd.Parameters.AddWithValue("@attachedAtUtc", attachedAtUtc);
                cmd.Parameters.AddWithValue("@id", orderedIds[index]);
                cmd.Parameters.AddWithValue("@userId", userId);

                updated += await cmd.ExecuteNonQueryAsync(ct);
            }

            transaction.Commit();
            return updated;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    private static RoomPhoto ReadRow(DbDataReader reader)
    {
        var roomIdOrdinal = reader.GetOrdinal("RoomId");
        var attachedAtUtcOrdinal = reader.GetOrdinal("AttachedAtUtc");

        return new RoomPhoto(
            uploadedByUserId: reader.GetString(reader.GetOrdinal("UploadedByUserId")),
            kind: Enum.Parse<PhotoKind>(reader.GetString(reader.GetOrdinal("Kind"))),
            contentType: reader.GetString(reader.GetOrdinal("ContentType")),
            sizeBytes: reader.GetInt64(reader.GetOrdinal("SizeBytes")),
            width: reader.GetInt32(reader.GetOrdinal("Width")),
            height: reader.GetInt32(reader.GetOrdinal("Height")),
            originalPath: reader.GetString(reader.GetOrdinal("OriginalPath")),
            mediumPath: reader.GetString(reader.GetOrdinal("MediumPath")),
            thumbPath: reader.GetString(reader.GetOrdinal("ThumbPath")),
            roomId: reader.IsDBNull(roomIdOrdinal) ? null : reader.GetString(roomIdOrdinal),
            sortOrder: reader.GetInt32(reader.GetOrdinal("SortOrder")),
            id: reader.GetString(reader.GetOrdinal("Id")),
            createdAtUtc: DateTime.Parse(
                reader.GetString(reader.GetOrdinal("CreatedAtUtc")),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            attachedAtUtc: reader.IsDBNull(attachedAtUtcOrdinal)
                ? null
                : DateTime.Parse(
                    reader.GetString(attachedAtUtcOrdinal),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind));
    }

    private void ReleaseConnection(SqliteConnection conn)
    {
        if (!_dbContext.OwnsSharedConnection)
        {
            conn.Dispose();
        }
    }
}
