using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.Sqlite;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// M2 / M3 / ADR-002: SQLite schema, seed and repositories for <c>Rooms</c> and <c>Campuses</c>.
/// RED ở Task 1 — Task 3 (Giai đoạn 3) cung cấp <c>SqliteCampusRepository</c>,
/// <c>SqliteRoomSearchReadModel</c> và thay stub <see cref="NotImplementedException"/> của
/// <c>SqliteRoomRepository</c>; fixture này phải xanh mà không sửa test.
/// </summary>
[TestFixture]
public class SqliteRoomRepositoryTests
{
    private static readonly SqliteSettings InMemorySettings = new() { ConnectionString = "Data Source=:memory:" };

    [Test]
    public async Task Schema_ContainsCampusesTableWithSeededReferenceData()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        using var connection = dbContext.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, IsDefault FROM Campuses ORDER BY SortOrder;";

        var ids = new List<string>();
        long defaultCount = 0;
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetString(0));
            defaultCount += reader.GetInt64(1);
        }

        Assert.Multiple(() =>
        {
            Assert.That(ids, Is.EquivalentTo(new[] { "ICTU", "TNUS", "SFL", "TUEBA", "TUAF", "TNUE", "TUMP", "TNUT" }));
            Assert.That(defaultCount, Is.EqualTo(1), "BR-CMP-01: đúng một cơ sở mặc định");
        });
    }

    [Test]
    public void Schema_ContainsEveryErd44ColumnOfRooms()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        using var connection = dbContext.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Rooms);";

        var columns = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        Assert.That(columns, Is.SupersetOf(new[]
        {
            "Id", "LandlordId", "Title", "Description", "RoomType", "MonthlyRentVnd", "DepositVnd", "AreaM2",
            "MaxOccupants", "Furnishing", "AvailableFrom", "AddressLine", "Ward", "District", "Latitude", "Longitude",
            "ElectricityPriceVndPerKwh", "WaterPriceVnd", "WaterPriceUnit", "InternetFeeVnd", "ParkingFeeVnd",
            "OtherFeesNote", "Status", "StatusReason", "NeedsReReview", "IsSafetyVerified", "VerifiedAtUtc",
            "VerifiedByUserId", "PublishedAtUtc", "LastUpdatedAtUtc", "CreatedAtUtc", "Version"
        }));
    }

    [Test]
    public async Task RoomSeed_IsGatedBySeedDemoRoomsFlag()
    {
        using var withoutDemo = new SqliteDbContext(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:",
            SeedDemoRooms = false
        });
        using var connection = withoutDemo.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM Rooms;";

        var count = Convert.ToInt64(await command.ExecuteScalarAsync());

        Assert.That(count, Is.Zero, "H4: seed demo chỉ chạy khi SeedDemoRooms = true");
    }

    [Test]
    public void SeedDemoRooms_HasFiveAvailableOnePendingOneRentedOneRejected()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:",
            SeedDemoRooms = true
        });
        using var connection = dbContext.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Status, COUNT(1) FROM Rooms GROUP BY Status;";

        var byStatus = new Dictionary<string, long>(StringComparer.Ordinal);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            byStatus[reader.GetString(0)] = reader.GetInt64(1);
        }

        Assert.Multiple(() =>
        {
            Assert.That(byStatus.GetValueOrDefault("Available"), Is.EqualTo(5));
            Assert.That(byStatus.GetValueOrDefault("PendingReview"), Is.EqualTo(1));
            Assert.That(byStatus.GetValueOrDefault("Rented"), Is.EqualTo(1));
            Assert.That(byStatus.GetValueOrDefault("Rejected"), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetByIdAsync_SeededRoom_RoundTripsEveryMappedField()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:",
            SeedDemoRooms = true
        });
        IRoomRepository repository = new SqliteRoomRepository(dbContext);

        var room = await repository.GetByIdAsync("seed-z115");

        Assert.That(room, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(room!.Title, Is.EqualTo("Phòng trọ Sinh viên Z115"));
            Assert.That(room.RoomType, Is.EqualTo(RoomType.BoardingRoom));
            Assert.That(room.MonthlyRentVnd, Is.EqualTo(1_500_000L));
            Assert.That(room.AreaM2, Is.EqualTo(18.0));
            Assert.That(room.Latitude, Is.EqualTo(21.5875));
            Assert.That(room.Longitude, Is.EqualTo(105.8080));
            Assert.That(room.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(room.VerifiedAtUtc, Is.Not.Null);
            Assert.That(room.Version, Is.GreaterThanOrEqualTo(1));
            Assert.That(room.ConcurrencyToken, Is.EqualTo(room.Version.ToString()));
        });
    }

    [Test]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsNewRoom()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        IRoomRepository repository = new SqliteRoomRepository(dbContext);
        var room = NewRoom("room-new");

        await repository.AddAsync(room);
        var found = await repository.GetByIdAsync("room-new");

        Assert.That(found, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(found!.Title, Is.EqualTo(room.Title));
            Assert.That(found.MonthlyRentVnd, Is.EqualTo(room.MonthlyRentVnd));
            Assert.That(found.Status, Is.EqualTo(ListingStatus.PendingReview));
            Assert.That(found.CreatedAtUtc, Is.EqualTo(room.CreatedAtUtc));
        });
    }

    [Test]
    public async Task UpdateAsync_WithStaleVersion_ThrowsConcurrencyConflictException()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        IRoomRepository repository = new SqliteRoomRepository(dbContext);
        var room = NewRoom("room-concurrent");
        await repository.AddAsync(room);

        var stale = (await repository.GetByIdAsync("room-concurrent"))!;
        var fresh = (await repository.GetByIdAsync("room-concurrent"))!;
        fresh.Approve(DateTime.UtcNow);
        await repository.UpdateAsync(fresh);

        Assert.ThrowsAsync<ConcurrencyConflictException>(async () => await repository.UpdateAsync(stale));
    }

    [Test]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        IRoomRepository repository = new SqliteRoomRepository(dbContext);

        Assert.That(await repository.GetByIdAsync("does-not-exist"), Is.Null);
    }

    [Test]
    public async Task AddAsync_NullRoom_ThrowsArgumentNullException()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        IRoomRepository repository = new SqliteRoomRepository(dbContext);

        Assert.ThrowsAsync<ArgumentNullException>(async () => await repository.AddAsync(null!));
    }

    [Test]
    public async Task CampusRepository_ReturnsSeededIctuAsDefault()
    {
        using var dbContext = new SqliteDbContext(InMemorySettings);
        ICampusRepository repository = new SqliteCampusRepository(dbContext);

        var defaultCampus = await repository.GetDefaultCampusAsync();
        var byId = await repository.GetByIdAsync("ICTU");
        var all = await repository.GetAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(defaultCampus, Is.Not.Null);
            Assert.That(defaultCampus!.Id, Is.EqualTo("ICTU"));
            Assert.That(defaultCampus.Latitude, Is.EqualTo(21.5851));
            Assert.That(defaultCampus.Longitude, Is.EqualTo(105.8066));
            Assert.That(byId?.ShortName, Is.EqualTo("ICTU"));
            Assert.That(all, Has.Count.EqualTo(8));
        });
    }

    [Test]
    public async Task SearchReadModel_ReturnsOnlyAvailableRoomsWithinFilters()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:",
            SeedDemoRooms = true
        });
        IRoomSearchReadModel readModel = new SqliteRoomSearchReadModel(dbContext);

        var candidates = await readModel.QueryCandidatesAsync(new RoomSearchQueryFilter(
            MinRentVnd: 1_000_000L,
            MaxRentVnd: 3_000_000L,
            MinAreaM2: 10,
            MaxAreaM2: 30,
            RoomTypes: new[] { RoomType.BoardingRoom },
            MinLat: null,
            MaxLat: null,
            MinLng: null,
            MaxLng: null));

        Assert.That(candidates, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(candidates.Select(candidate => candidate.Status), Is.All.EqualTo(ListingStatus.Available));
            Assert.That(candidates.Select(candidate => candidate.RoomType), Is.All.EqualTo(RoomType.BoardingRoom));
            Assert.That(candidates.Select(candidate => candidate.MonthlyRentVnd), Is.All.InRange(1_000_000L, 3_000_000L));
            Assert.That(candidates.Select(candidate => candidate.AreaM2), Is.All.InRange(10.0, 30.0));
            Assert.That(candidates.Select(candidate => candidate.IsVerified), Is.All.True);
        });
    }

    [Test]
    public async Task SearchReadModel_WithBoundingBox_ExcludesRoomsOutsideBox()
    {
        using var dbContext = new SqliteDbContext(new SqliteSettings
        {
            ConnectionString = "Data Source=:memory:",
            SeedDemoRooms = true
        });
        IRoomSearchReadModel readModel = new SqliteRoomSearchReadModel(dbContext);

        var candidates = await readModel.QueryCandidatesAsync(new RoomSearchQueryFilter(
            null, null, null, null, null,
            MinLat: 21.5840, MaxLat: 21.5890, MinLng: 105.8050, MaxLng: 105.8090));

        Assert.That(candidates, Is.Not.Empty);
        Assert.That(candidates.Select(candidate => candidate.Id), Is.EquivalentTo(new[] { "seed-z115" }));
    }

    [Test]
    public void DependencyInjection_AddSqliteDatastore_RegistersRoomsAndCampusesPorts()
    {
        var services = new ServiceCollection();
        services.AddSqliteDatastore(InMemorySettings);

        Assert.Multiple(() =>
        {
            Assert.That(
                services.Single(d => d.ServiceType == typeof(IRoomRepository)).ImplementationType,
                Is.EqualTo(typeof(SqliteRoomRepository)));
            Assert.That(
                services.Single(d => d.ServiceType == typeof(ICampusRepository)).ImplementationType,
                Is.EqualTo(typeof(SqliteCampusRepository)));
            Assert.That(
                services.Single(d => d.ServiceType == typeof(IRoomSearchReadModel)).ImplementationType,
                Is.EqualTo(typeof(SqliteRoomSearchReadModel)));
        });
    }

    private static Room NewRoom(string id) => Room.Reconstitute(
        id: id,
        landlordId: "landlord-1",
        title: "Phòng mới SQLite",
        roomType: RoomType.BoardingRoom,
        monthlyRentVnd: 1_850_000L,
        depositVnd: 1_850_000L,
        areaM2: 20.5,
        addressLine: "Tân Thịnh",
        ward: "Tân Thịnh",
        district: "TP. Thái Nguyên",
        latitude: 21.5851,
        longitude: 105.8066,
        status: ListingStatus.PendingReview,
        createdAtUtc: new DateTime(2026, 11, 2, 9, 30, 0, DateTimeKind.Utc),
        lastUpdatedAtUtc: new DateTime(2026, 11, 2, 9, 30, 0, DateTimeKind.Utc),
        version: 1);
}
