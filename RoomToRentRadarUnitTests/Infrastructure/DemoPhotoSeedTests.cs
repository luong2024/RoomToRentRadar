using Microsoft.Data.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>T00.10: demo photo rows (BR-MED-01, BR-MED-03, BR-MED-05).</summary>
[TestFixture]
public class DemoPhotoSeedTests
{
    private static List<SeedPhoto> Photos()
    {
        var photos = new List<SeedPhoto>();
        foreach (var roomType in Enum.GetValues<RoomType>())
        {
            photos.Add(Photo(roomType, $"{roomType}-a"));
            photos.Add(Photo(roomType, $"{roomType}-b"));
        }

        photos.Add(Photo(null, "bath-a"));
        return photos;
    }

    private static SeedPhoto Photo(RoomType? roomType, string name) =>
        new(roomType, $"seed/rooms/{name}-thumb.jpg", $"seed/rooms/{name}-medium.jpg", 1280, 853, 100_000);

    private static SqliteSettings Settings(string connectionString, bool extended = true, bool demoRooms = true) =>
        new()
        {
            ConnectionString = connectionString,
            SeedDemoRooms = demoRooms,
            SeedExtendedDemoData = extended,
            SeedPhotos = Photos()
        };

    private static long Scalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [Test]
    public void EveryDemoRoom_GetsThreeDistinctPhotos_OrderedFromZero()
    {
        using var dbContext = new SqliteDbContext(Settings("Data Source=:memory:"));
        using var conn = dbContext.CreateOpenConnection();

        Assert.Multiple(() =>
        {
            Assert.That(Scalar(conn, "SELECT COUNT(1) FROM RoomPhotos;"), Is.EqualTo(90));
            Assert.That(Scalar(conn, """
                SELECT COUNT(1) FROM (
                    SELECT RoomId FROM RoomPhotos GROUP BY RoomId
                    HAVING COUNT(DISTINCT ThumbPath) = 3 AND MIN(SortOrder) = 0 AND MAX(SortOrder) = 2);
                """), Is.EqualTo(30));
        });
    }

    [Test]
    public void Cover_ShowsTheRoomsOwnType()
    {
        using var dbContext = new SqliteDbContext(Settings("Data Source=:memory:"));
        using var conn = dbContext.CreateOpenConnection();

        var mismatched = Scalar(conn, """
            SELECT COUNT(1) FROM RoomPhotos p INNER JOIN Rooms r ON r.Id = p.RoomId
            WHERE p.SortOrder = 0 AND p.ThumbPath NOT LIKE 'seed/rooms/' || r.RoomType || '-%';
            """);

        Assert.That(mismatched, Is.Zero);
    }

    [Test]
    public void Rows_AreAttachedStandardPhotosOwnedByTheRoomsLandlord()
    {
        using var dbContext = new SqliteDbContext(Settings("Data Source=:memory:"));
        using var conn = dbContext.CreateOpenConnection();

        var invalid = Scalar(conn, """
            SELECT COUNT(1) FROM RoomPhotos p INNER JOIN Rooms r ON r.Id = p.RoomId
            WHERE p.UploadedByUserId <> r.LandlordId OR p.Kind <> 'Standard' OR p.AttachedAtUtc IS NULL
               OR p.ContentType <> 'image/jpeg' OR p.OriginalPath <> p.MediumPath;
            """);

        Assert.That(invalid, Is.Zero);
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void NoPhotos_WhenDemoRoomsOrExtendedDataAreOff(bool extended, bool demoRooms)
    {
        using var dbContext = new SqliteDbContext(Settings("Data Source=:memory:", extended, demoRooms));
        using var conn = dbContext.CreateOpenConnection();

        Assert.That(Scalar(conn, "SELECT COUNT(1) FROM RoomPhotos;"), Is.Zero);
    }

    [Test]
    public void Restart_AddsNothing_KeepsUserRemovals_AndRefillsOnlyEmptyRooms()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"rtr_photo_seed_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var connectionString = $"Data Source={Path.Combine(folder, "seed.db")}";

        try
        {
            // An existing database seeded before photos existed.
            using (var before = new SqliteDbContext(new SqliteSettings
            {
                ConnectionString = connectionString,
                SeedDemoRooms = true,
                SeedExtendedDemoData = true
            }))
            {
                using var conn = before.CreateOpenConnection();
                Assert.That(Scalar(conn, "SELECT COUNT(1) FROM RoomPhotos;"), Is.Zero);
            }

            using (var first = new SqliteDbContext(Settings(connectionString)))
            {
                using var conn = first.CreateOpenConnection();
                Assert.That(Scalar(conn, "SELECT COUNT(1) FROM RoomPhotos;"), Is.EqualTo(90));
                Scalar(conn, "DELETE FROM RoomPhotos WHERE Id = 'photo-demo-01-3'; SELECT changes();");
                Scalar(conn, "DELETE FROM RoomPhotos WHERE RoomId = 'demo-02'; SELECT changes();");
            }

            using (var second = new SqliteDbContext(Settings(connectionString)))
            {
                using var conn = second.CreateOpenConnection();
                Assert.Multiple(() =>
                {
                    Assert.That(Scalar(conn, "SELECT COUNT(1) FROM RoomPhotos WHERE RoomId = 'demo-01';"), Is.EqualTo(2));
                    Assert.That(Scalar(conn, "SELECT COUNT(1) FROM RoomPhotos WHERE RoomId = 'demo-02';"), Is.EqualTo(3));
                    Assert.That(Scalar(conn, "SELECT COUNT(1) FROM RoomPhotos;"), Is.EqualTo(89));
                });
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(folder, recursive: true);
        }
    }
}
