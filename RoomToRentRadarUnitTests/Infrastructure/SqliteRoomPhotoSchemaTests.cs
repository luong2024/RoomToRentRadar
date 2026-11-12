using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;

namespace RoomToRentRadarUnitTests.Infrastructure;

// ERD §4.8: CHECK constraints guard values even when a caller bypasses the RoomPhoto aggregate.
[TestFixture]
public class SqliteRoomPhotoSchemaTests
{
    [Test]
    public async Task Schema_RejectsSizeBytesAboveTheTenMegabyteLimit()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings { ConnectionString = "Data Source=:memory:" });
        var conn = dbContext.CreateOpenConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO RoomPhotos (
                Id, RoomId, UploadedByUserId, Kind, ContentType, SizeBytes, Width, Height,
                OriginalPath, MediumPath, ThumbPath, SortOrder, CreatedAtUtc, AttachedAtUtc
            ) VALUES (
                'too-big', NULL, 'landlord-1', 'Standard', 'image/jpeg', 10485761, 800, 600,
                'a.jpg', 'a.jpg', 'a.jpg', 0, '2026-11-12T00:00:00.0000000Z', NULL
            );
            """;

        await Assert.ThatAsync(
            async () => await cmd.ExecuteNonQueryAsync(),
            Throws.TypeOf<SqliteException>());
    }
}
