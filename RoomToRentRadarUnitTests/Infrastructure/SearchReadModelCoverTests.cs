using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>T00.10: cover photo (BR-MED-05) and furnishing in the search read model.</summary>
[TestFixture]
public class SearchReadModelCoverTests
{
    private SqliteDbContext _dbContext = null!;

    [SetUp]
    public void SetUp() => _dbContext = new SqliteDbContext(new SqliteSettings
    {
        ConnectionString = "Data Source=:memory:",
        SeedDemoRooms = true
    });

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    private void Execute(string sql)
    {
        // The in-memory database has one shared connection owned by the context: never dispose it here.
        var conn = _dbContext.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private void AddPhoto(string id, string kind, int sortOrder) => Execute($"""
        INSERT INTO RoomPhotos (Id, RoomId, UploadedByUserId, Kind, ContentType, SizeBytes, Width, Height,
            OriginalPath, MediumPath, ThumbPath, SortOrder, CreatedAtUtc, AttachedAtUtc)
        VALUES ('{id}', 'seed-z115', 'seed-landlord', '{kind}', 'image/jpeg', 10, 10, 10,
            'rooms/{id}/m.jpg', 'rooms/{id}/m.jpg', 'rooms/{id}/t.jpg', {sortOrder},
            '2026-11-01T00:00:00.0000000Z', '2026-11-01T00:00:00.0000000Z');
        """);

    private async Task<RoomSearchCandidate> QueryZ115Async()
    {
        var filter = new RoomSearchQueryFilter(null, null, null, null, null, null, null, null, null);
        var candidates = await new SqliteRoomSearchReadModel(_dbContext).QueryCandidatesAsync(filter);
        return candidates.Single(c => c.Id == "seed-z115");
    }

    [Test]
    public async Task RoomWithoutPhotos_HasNoCover()
    {
        Assert.That((await QueryZ115Async()).CoverThumbPath, Is.Null);
    }

    [Test]
    public async Task Cover_IsTheStandardPhotoWithTheLowestSortOrder()
    {
        AddPhoto("pano", "Panorama", 0);
        AddPhoto("second", "Standard", 2);
        AddPhoto("first", "Standard", 1);

        Assert.That((await QueryZ115Async()).CoverThumbPath, Is.EqualTo("rooms/first/t.jpg"));
    }

    [TestCase("'Full'", Furnishing.Full)]
    [TestCase("'unfurnished'", Furnishing.Unfurnished)]
    [TestCase("'Luxury'", null)]
    [TestCase("'7'", null)]
    [TestCase("NULL", null)]
    public async Task Furnishing_IsParsed_AndUnknownValuesAreNotStated(string sqlValue, Furnishing? expected)
    {
        Execute($"UPDATE Rooms SET Furnishing = {sqlValue} WHERE Id = 'seed-z115';");

        Assert.That((await QueryZ115Async()).Furnishing, Is.EqualTo(expected));
    }
}
