using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// US09 / T09.1 / ERD §4.15: SQLite persistence for <c>TravelTimeEstimates</c>,
/// 30-day cache upsert, pin-move invalidation, expiration pruning, and FK cascade.
/// </summary>
[TestFixture]
public class SqliteTravelTimeCacheRepositoryTests
{
    private static readonly SqliteSettings InMemorySettings = new() { ConnectionString = "Data Source=:memory:" };
    private static readonly DateTime FixedNow = new(2026, 11, 25, 8, 0, 0, DateTimeKind.Utc);

    private SqliteDbContext _dbContext = null!;
    private ITravelTimeCacheRepository _repository = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dbContext = new SqliteDbContext(InMemorySettings);
        await AddRoomAsync("room-1", "landlord-1");
        await AddRoomAsync("room-2", "landlord-1");
        _repository = new SqliteTravelTimeCacheRepository(_dbContext);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    [Test]
    public async Task UpsertRangeAsync_ThenGetByRoomAndCampusAsync_RoundTripsAllThreeModes()
    {
        var entries = new[]
        {
            new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Motorbike, 1850, 320, "Routing", FixedNow),
            new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Bus, 1850, 1112, "Estimated", FixedNow),
            new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Walking, 1620, 1180, "Routing", FixedNow)
        };

        await _repository.UpsertRangeAsync(entries);
        var loaded = await _repository.GetByRoomAndCampusAsync("room-1", "ICTU");

        Assert.That(loaded, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            var motorbike = loaded.Single(e => e.Mode == TravelMode.Motorbike);
            Assert.That(motorbike.DistanceMeters, Is.EqualTo(1850));
            Assert.That(motorbike.DurationSeconds, Is.EqualTo(320));
            Assert.That(motorbike.Source, Is.EqualTo("Routing"));
            Assert.That(motorbike.ComputedAtUtc, Is.EqualTo(FixedNow));

            var bus = loaded.Single(e => e.Mode == TravelMode.Bus);
            Assert.That(bus.DistanceMeters, Is.EqualTo(1850));
            Assert.That(bus.DurationSeconds, Is.EqualTo(1112));
            Assert.That(bus.Source, Is.EqualTo("Estimated"));

            var walking = loaded.Single(e => e.Mode == TravelMode.Walking);
            Assert.That(walking.DistanceMeters, Is.EqualTo(1620));
            Assert.That(walking.DurationSeconds, Is.EqualTo(1180));
            Assert.That(walking.Source, Is.EqualTo("Routing"));
        });
    }

    [Test]
    public async Task UpsertRangeAsync_ExistingPrimaryKey_UpdatesRowInPlace()
    {
        var initial = new[]
        {
            new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Motorbike, 1800, 300, "Routing", FixedNow.AddDays(-35))
        };
        await _repository.UpsertRangeAsync(initial);

        var refreshed = new[]
        {
            new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Motorbike, 2100, 410, "Routing", FixedNow)
        };
        await _repository.UpsertRangeAsync(refreshed);

        var loaded = await _repository.GetByRoomAndCampusAsync("room-1", "ICTU");
        Assert.That(loaded, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(loaded[0].DistanceMeters, Is.EqualTo(2100));
            Assert.That(loaded[0].DurationSeconds, Is.EqualTo(410));
            Assert.That(loaded[0].ComputedAtUtc, Is.EqualTo(FixedNow));
        });
    }

    [Test]
    public async Task InvalidateByRoomIdAsync_DeletesOnlyTargetRoomEntriesAcrossAllCampuses()
    {
        await _repository.UpsertRangeAsync(new[]
        {
            new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Motorbike, 1800, 300, "Routing", FixedNow),
            new TravelTimeCacheEntry("room-1", "TNUS", TravelMode.Motorbike, 2400, 420, "Routing", FixedNow),
            new TravelTimeCacheEntry("room-2", "ICTU", TravelMode.Motorbike, 1500, 260, "Routing", FixedNow)
        });

        await _repository.InvalidateByRoomIdAsync("room-1");

        var room1Ictu = await _repository.GetByRoomAndCampusAsync("room-1", "ICTU");
        var room1Tnus = await _repository.GetByRoomAndCampusAsync("room-1", "TNUS");
        var room2Ictu = await _repository.GetByRoomAndCampusAsync("room-2", "ICTU");

        Assert.Multiple(() =>
        {
            Assert.That(room1Ictu, Is.Empty);
            Assert.That(room1Tnus, Is.Empty);
            Assert.That(room2Ictu, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task PruneExpiredAsync_RemovesOnlyEntriesOlderThanCutoff()
    {
        await _repository.UpsertRangeAsync(new[]
        {
            new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Motorbike, 1800, 300, "Routing", FixedNow.AddDays(-31)),
            new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Bus, 1800, 1080, "Estimated", FixedNow.AddDays(-30)),
            new TravelTimeCacheEntry("room-2", "ICTU", TravelMode.Motorbike, 1500, 260, "Routing", FixedNow.AddDays(-5))
        });

        var deleted = await _repository.PruneExpiredAsync(FixedNow.AddDays(-30));

        var room1 = await _repository.GetByRoomAndCampusAsync("room-1", "ICTU");
        var room2 = await _repository.GetByRoomAndCampusAsync("room-2", "ICTU");

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.EqualTo(1));
            Assert.That(room1.Select(x => x.Mode), Is.EqualTo(new[] { TravelMode.Bus }));
            Assert.That(room2, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task DeletingRoom_CascadesToTravelTimeEstimates()
    {
        await _repository.UpsertRangeAsync(new[]
        {
            new TravelTimeCacheEntry("room-1", "ICTU", TravelMode.Motorbike, 1800, 300, "Routing", FixedNow)
        });

        var connection = _dbContext.CreateOpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Rooms WHERE Id = 'room-1';";
        cmd.ExecuteNonQuery();

        var remaining = await _repository.GetByRoomAndCampusAsync("room-1", "ICTU");
        Assert.That(remaining, Is.Empty);
    }

    private async Task AddRoomAsync(string roomId, string landlordId)
    {
        var room = Room.Reconstitute(
            id: roomId,
            landlordId: landlordId,
            title: "Phòng trọ kiểm thử travel time",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 1_800_000L,
            depositVnd: 1_800_000L,
            areaM2: 20.0,
            addressLine: "Đường Z115",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5875,
            longitude: 105.8080,
            status: ListingStatus.Available,
            createdAtUtc: FixedNow,
            lastUpdatedAtUtc: FixedNow,
            version: 1);

        await new SqliteRoomRepository(_dbContext).AddAsync(room);
    }
}
