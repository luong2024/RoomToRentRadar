using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite.Repositories;

public sealed class SqliteRoomPhotoRepository : IRoomPhotoRepository
{
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
                    Id, RoomId, FileName, Url, ContentType, SizeBytes, UploadedByUserId, UploadedAtUtc
                ) VALUES (
                    @id, @roomId, @fileName, @url, @contentType, @sizeBytes, @uploadedByUserId, @uploadedAtUtc
                );
                """;

            cmd.Parameters.AddWithValue("@id", photo.Id);
            cmd.Parameters.AddWithValue("@roomId", photo.RoomId);
            cmd.Parameters.AddWithValue("@fileName", photo.FileName);
            cmd.Parameters.AddWithValue("@url", photo.Url);
            cmd.Parameters.AddWithValue("@contentType", photo.ContentType);
            cmd.Parameters.AddWithValue("@sizeBytes", photo.SizeBytes);
            cmd.Parameters.AddWithValue("@uploadedByUserId", photo.UploadedByUserId);
            cmd.Parameters.AddWithValue("@uploadedAtUtc", photo.UploadedAtUtc.ToString("O", CultureInfo.InvariantCulture));

            await cmd.ExecuteNonQueryAsync(ct);
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
            cmd.CommandText = """
                SELECT Id, RoomId, FileName, Url, ContentType, SizeBytes, UploadedByUserId, UploadedAtUtc
                FROM RoomPhotos
                WHERE RoomId = @roomId
                ORDER BY UploadedAtUtc ASC;
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

    public async Task<RoomPhoto?> GetAsync(string roomId, string photoId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(photoId);

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT Id, RoomId, FileName, Url, ContentType, SizeBytes, UploadedByUserId, UploadedAtUtc
                FROM RoomPhotos
                WHERE RoomId = @roomId AND Id = @photoId
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("@roomId", roomId);
            cmd.Parameters.AddWithValue("@photoId", photoId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? ReadRow(reader) : null;
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    public async Task DeleteAsync(string photoId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(photoId);

        var conn = _dbContext.CreateOpenConnection();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM RoomPhotos WHERE Id = @photoId;";
            cmd.Parameters.AddWithValue("@photoId", photoId);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            ReleaseConnection(conn);
        }
    }

    private static RoomPhoto ReadRow(DbDataReader reader)
    {
        return new RoomPhoto(
            roomId: reader.GetString(reader.GetOrdinal("RoomId")),
            fileName: reader.GetString(reader.GetOrdinal("FileName")),
            url: reader.GetString(reader.GetOrdinal("Url")),
            contentType: reader.GetString(reader.GetOrdinal("ContentType")),
            sizeBytes: reader.GetInt64(reader.GetOrdinal("SizeBytes")),
            uploadedByUserId: reader.GetString(reader.GetOrdinal("UploadedByUserId")),
            id: reader.GetString(reader.GetOrdinal("Id")),
            uploadedAtUtc: DateTime.Parse(
                reader.GetString(reader.GetOrdinal("UploadedAtUtc")),
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
