using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Datastore.Sqlite.Transactions;
using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Transactions;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// T00.9 / T08.2: the Room aggregate's children (ERD §4.5–4.7), the landlord own-listings queries
/// and the atomicity contract of <see cref="SqliteUnitOfWork"/>.
/// </summary>
[TestFixture]
public class SqliteRoomRepositoryChildrenTests
{
    private static readonly SqliteSettings InMemorySettings = new() { ConnectionString = "Data Source=:memory:" };

    private static readonly DateTime FixedNow = new(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);

    private SqliteDbContext _dbContext = null!;
    private SqliteRoomRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _dbContext = new SqliteDbContext(InMemorySettings);
        InsertUser(_dbContext, "landlord-1");
        InsertUser(_dbContext, "landlord-2");
        _repository = new SqliteRoomRepository(_dbContext);
    }

    [TearDown]
    public void TearDown() => _dbContext.Dispose();

    [Test]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsAmenitiesSafetyAndCurrentHouseRules()
    {
        var room = NewRoom(
            "room-children",
            "landlord-1",
            amenities: new[] { Amenity.Wifi, Amenity.AirConditioner },
            safetyFeatures: new[]
            {
                new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true),
                new RoomSafetyFeatureEntry(SafetyFeature.FireSafety, IsConfirmed: false)
            },
            houseRules: NewHouseRules("rules-v1", "room-children", "landlord-1", versionNumber: 1));

        await _repository.AddAsync(room);
        var stored = await _repository.GetByIdAsync("room-children");

        Assert.That(stored, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(stored!.Amenities, Is.EquivalentTo(new[] { Amenity.AirConditioner, Amenity.Wifi }));
            Assert.That(stored.SafetyFeatures, Is.EquivalentTo(new[]
            {
                new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true),
                new RoomSafetyFeatureEntry(SafetyFeature.FireSafety, IsConfirmed: false)
            }));

            var rules = stored.CurrentHouseRules;
            Assert.That(rules, Is.Not.Null);
            Assert.That(rules!.Id, Is.EqualTo("rules-v1"));
            Assert.That(rules.VersionNumber, Is.EqualTo(1));
            Assert.That(rules.QuietHoursStart, Is.EqualTo("22:00"));
            Assert.That(rules.QuietHoursEnd, Is.EqualTo("06:00"));
            Assert.That(rules.CurfewTime, Is.EqualTo("23:30"));
            Assert.That(rules.GuestPolicy, Is.EqualTo(GuestPolicy.DaytimeOnly));
            Assert.That(rules.GuestPolicyNote, Is.EqualTo("Khách báo trước."));
            Assert.That(rules.CookingPolicy, Is.EqualTo(CookingPolicy.SharedKitchenOnly));
            Assert.That(rules.ParkingAvailable, Is.True);
            Assert.That(rules.ParkingRules, Is.EqualTo("Xe máy xếp hàng."));
            Assert.That(rules.PetsAllowed, Is.False);
            Assert.That(rules.SmokingAllowed, Is.False);
            Assert.That(rules.OtherRules, Is.EqualTo("Không tổ chức tiệc."));
            Assert.That(rules.CreatedByUserId, Is.EqualTo("landlord-1"));
            Assert.That(rules.CreatedAtUtc, Is.EqualTo(FixedNow));
        });
    }

    [Test]
    public async Task GetByIdAsync_RoomWithoutChildren_ReturnsEmptyCollectionsAndNullHouseRules()
    {
        await _repository.AddAsync(NewRoom("room-bare", "landlord-1"));

        var stored = await _repository.GetByIdAsync("room-bare");

        Assert.Multiple(() =>
        {
            Assert.That(stored!.Amenities, Is.Empty);
            Assert.That(stored.SafetyFeatures, Is.Empty);
            Assert.That(stored.CurrentHouseRules, Is.Null);
        });
    }

    [Test]
    public async Task UpdateAsync_ReplacesAmenitiesAndSafetyFeaturesAndKeepsConfirmedFlags()
    {
        await _repository.AddAsync(NewRoom(
            "room-edit",
            "landlord-1",
            status: ListingStatus.Available,
            amenities: new[] { Amenity.Wifi, Amenity.Bed },
            safetyFeatures: new[]
            {
                new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true),
                new RoomSafetyFeatureEntry(SafetyFeature.FireSafety, IsConfirmed: false)
            }));

        var loaded = (await _repository.GetByIdAsync("room-edit"))!;
        loaded.UpdateDetails(DetailsOf(loaded,
            amenities: new[] { Amenity.Fridge, Amenity.Wifi },
            safetyFeatures: new[] { SafetyFeature.Cctv, SafetyFeature.FireSafety, SafetyFeature.WellLitAccess }));

        await _repository.UpdateAsync(loaded);
        var stored = (await _repository.GetByIdAsync("room-edit"))!;

        Assert.Multiple(() =>
        {
            Assert.That(stored.Amenities, Is.EquivalentTo(new[] { Amenity.Fridge, Amenity.Wifi }));
            Assert.That(stored.SafetyFeatures, Is.EquivalentTo(new[]
            {
                new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true),
                new RoomSafetyFeatureEntry(SafetyFeature.FireSafety, IsConfirmed: false),
                new RoomSafetyFeatureEntry(SafetyFeature.WellLitAccess, IsConfirmed: false)
            }));
            Assert.That(stored.Version, Is.EqualTo(2));
            Assert.That(stored.NeedsReReview, Is.True, "Thêm safety feature mới là thay đổi material (BR-LST-05).");
        });
    }

    [Test]
    public async Task UpdateAsync_DoesNotTouchHouseRuleVersions()
    {
        await _repository.AddAsync(NewRoom(
            "room-rules-untouched",
            "landlord-1",
            houseRules: NewHouseRules("rules-only", "room-rules-untouched", "landlord-1", versionNumber: 1)));

        var loaded = (await _repository.GetByIdAsync("room-rules-untouched"))!;
        loaded.UpdateDetails(DetailsOf(loaded,
            amenities: new[] { Amenity.Desk },
            safetyFeatures: Array.Empty<SafetyFeature>()));
        await _repository.UpdateAsync(loaded);

        var stored = (await _repository.GetByIdAsync("room-rules-untouched"))!;
        Assert.Multiple(() =>
        {
            Assert.That(ReadScalar("SELECT COUNT(1) FROM HouseRuleVersions;"), Is.EqualTo(1),
                "updateListing bỏ qua houseRules (openapi.yaml) — bảng chỉ được append khi đổi quy định.");
            Assert.That(stored.CurrentHouseRules!.Id, Is.EqualTo("rules-only"));
            Assert.That(stored.CurrentHouseRules.VersionNumber, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetOwnedByIdAsync_AnotherLandlordOrUnknown_ReturnsNull()
    {
        await _repository.AddAsync(NewRoom("room-owned", "landlord-1"));

        var foreign = await _repository.GetOwnedByIdAsync("room-owned", "landlord-2");
        var unknown = await _repository.GetOwnedByIdAsync("room-missing", "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(foreign, Is.Null, "GUARDRAILS §1.5.6: phòng của người khác phải như không tồn tại.");
            Assert.That(unknown, Is.Null);
        });
    }

    [Test]
    public async Task GetOwnedByIdAsync_Owner_ReturnsRoomWithChildren()
    {
        await _repository.AddAsync(NewRoom(
            "room-owned-children",
            "landlord-1",
            amenities: new[] { Amenity.Wifi },
            safetyFeatures: new[] { new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true) }));

        var owned = await _repository.GetOwnedByIdAsync("room-owned-children", "landlord-1");

        Assert.That(owned, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(owned!.LandlordId, Is.EqualTo("landlord-1"));
            Assert.That(owned.Amenities, Is.EquivalentTo(new[] { Amenity.Wifi }));
            Assert.That(owned.SafetyFeatures, Is.EquivalentTo(new[]
            {
                new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true)
            }));
        });
    }

    [Test]
    public async Task CountNonArchivedByLandlordAsync_ExcludesUnpublishedAndOtherLandlords()
    {
        await _repository.AddAsync(NewRoom("room-pending", "landlord-1"));
        await _repository.AddAsync(NewRoom("room-available", "landlord-1", status: ListingStatus.Available));
        await _repository.AddAsync(NewRoom("room-unpublished", "landlord-1", status: ListingStatus.Unpublished));
        await _repository.AddAsync(NewRoom("room-other", "landlord-2"));

        var count = await _repository.CountNonArchivedByLandlordAsync("landlord-1");

        Assert.That(count, Is.EqualTo(2), "BR-LST-11: phòng Unpublished đã lưu trữ, không tính vào hạn mức 50.");
    }

    [Test]
    public async Task ListByLandlordAsync_PagesFiltersOrdersAndCountsEveryStatus()
    {
        await _repository.AddAsync(NewRoom("room-old", "landlord-1",
            status: ListingStatus.Unpublished, lastUpdatedAtUtc: FixedNow.AddDays(-3)));
        await _repository.AddAsync(NewRoom("room-available", "landlord-1",
            status: ListingStatus.Available, lastUpdatedAtUtc: FixedNow.AddDays(-1)));
        await _repository.AddAsync(NewRoom("room-pending", "landlord-1",
            status: ListingStatus.PendingReview, lastUpdatedAtUtc: FixedNow));
        await _repository.AddAsync(NewRoom("room-foreign", "landlord-2"));

        var firstPage = await _repository.ListByLandlordAsync("landlord-1", status: null, pageNumber: 1, pageSize: 2);
        var unpublished = await _repository.ListByLandlordAsync(
            "landlord-1", ListingStatus.Unpublished, pageNumber: 1, pageSize: 20);

        Assert.Multiple(() =>
        {
            Assert.That(firstPage.Items.Select(item => item.Room.Id), Is.EqualTo(new[] { "room-pending", "room-available" }),
                "Sắp theo LastUpdatedAtUtc giảm dần.");
            Assert.That(firstPage.TotalCount, Is.EqualTo(3));
            Assert.That(firstPage.CountsByStatus, Has.Count.EqualTo(Enum.GetValues<ListingStatus>().Length),
                "SCR-LL-01 cần đủ 6 trạng thái, kể cả khi bằng 0.");
            Assert.That(firstPage.CountsByStatus[ListingStatus.PendingReview], Is.EqualTo(1));
            Assert.That(firstPage.CountsByStatus[ListingStatus.Available], Is.EqualTo(1));
            Assert.That(firstPage.CountsByStatus[ListingStatus.Unpublished], Is.EqualTo(1));
            Assert.That(firstPage.CountsByStatus[ListingStatus.Rejected], Is.Zero);

            Assert.That(unpublished.TotalCount, Is.EqualTo(1));
            Assert.That(unpublished.Items.Select(item => item.Room.Id), Is.EqualTo(new[] { "room-old" }));
        });
    }

    [Test]
    public async Task ListByLandlordAsync_CoverThumbPath_IsTheLowestSortOrderPhotoOfTheRoom()
    {
        await _repository.AddAsync(NewRoom("room-cover", "landlord-1"));
        var photoRepository = new SqliteRoomPhotoRepository(_dbContext);
        await photoRepository.AddAsync(NewPhoto("photo-later", roomId: "room-cover", sortOrder: 1, thumb: "rooms/cover/thumb-later.jpg"));
        await photoRepository.AddAsync(NewPhoto("photo-cover", roomId: "room-cover", sortOrder: 0, thumb: "rooms/cover/thumb-cover.jpg"));

        var page = await _repository.ListByLandlordAsync("landlord-1", status: null, pageNumber: 1, pageSize: 20);

        Assert.That(page.Items.Single().CoverThumbPath, Is.EqualTo("rooms/cover/thumb-cover.jpg"),
            "ERD §4.4 CoverPhoto = ảnh SortOrder nhỏ nhất.");
    }

    [Test]
    public async Task ExecuteAsync_WhenActionThrows_RollsBackRoomChildrenAndPhotoWrites()
    {
        IUnitOfWork unitOfWork = new SqliteUnitOfWork(_dbContext);
        var room = NewRoom(
            "room-rollback",
            "landlord-1",
            amenities: new[] { Amenity.Wifi },
            safetyFeatures: new[] { new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true) },
            houseRules: NewHouseRules("rules-rollback", "room-rollback", "landlord-1", versionNumber: 1));
        var photo = NewPhoto("photo-rollback", uploadedByUserId: "landlord-1");

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await unitOfWork.ExecuteAsync(async ct =>
            {
                await _repository.AddAsync(room, ct);
                await new SqliteRoomPhotoRepository(_dbContext).AddAsync(photo, ct);
                throw new InvalidOperationException("boom");
            }));

        var rolledBackRoom = await _repository.GetByIdAsync("room-rollback");
        Assert.Multiple(() =>
        {
            Assert.That(rolledBackRoom, Is.Null);
            Assert.That(ReadScalar("SELECT COUNT(1) FROM Rooms;"), Is.Zero);
            Assert.That(ReadScalar("SELECT COUNT(1) FROM RoomAmenities;"), Is.Zero);
            Assert.That(ReadScalar("SELECT COUNT(1) FROM RoomSafetyFeatures;"), Is.Zero);
            Assert.That(ReadScalar("SELECT COUNT(1) FROM HouseRuleVersions;"), Is.Zero);
            Assert.That(ReadScalar("SELECT COUNT(1) FROM RoomPhotos;"), Is.Zero);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenActionCompletes_CommitsRoomChildrenAndPhotoAttachTogether()
    {
        IUnitOfWork unitOfWork = new SqliteUnitOfWork(_dbContext);
        var room = NewRoom(
            "room-commit",
            "landlord-1",
            amenities: new[] { Amenity.Wifi },
            houseRules: NewHouseRules("rules-commit", "room-commit", "landlord-1", versionNumber: 1));
        var photo = NewPhoto("photo-commit", uploadedByUserId: "landlord-1");

        await unitOfWork.ExecuteAsync(async ct =>
        {
            await _repository.AddAsync(room, ct);
            var photoRepository = new SqliteRoomPhotoRepository(_dbContext);
            await photoRepository.AddAsync(photo, ct);
            await photoRepository.AttachAsync("landlord-1", "room-commit", new[] { "photo-commit" }, ct);
        });

        var stored = await _repository.GetByIdAsync("room-commit");
        var photos = await new SqliteRoomPhotoRepository(_dbContext).ListByRoomAsync("room-commit");

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.Not.Null);
            Assert.That(stored!.Amenities, Is.EquivalentTo(new[] { Amenity.Wifi }));
            Assert.That(stored.CurrentHouseRules!.VersionNumber, Is.EqualTo(1));
            Assert.That(photos.Single().Id, Is.EqualTo("photo-commit"));
            Assert.That(photos.Single().SortOrder, Is.Zero);
            Assert.That(photos.Single().AttachedAtUtc, Is.Not.Null);
        });
    }

    // ----------------------------------------------------------------- fixtures

    private static Room NewRoom(
        string id,
        string landlordId,
        ListingStatus status = ListingStatus.PendingReview,
        DateTime? lastUpdatedAtUtc = null,
        IReadOnlyList<Amenity>? amenities = null,
        IReadOnlyList<RoomSafetyFeatureEntry>? safetyFeatures = null,
        HouseRuleVersion? houseRules = null) =>
        Room.Reconstitute(
            id: id,
            landlordId: landlordId,
            title: "Phòng kiểm thử tầng SQLite",
            roomType: RoomType.BoardingRoom,
            monthlyRentVnd: 1_800_000L,
            depositVnd: 1_800_000L,
            areaM2: 20.0,
            addressLine: "Tân Thịnh",
            ward: "Tân Thịnh",
            district: "TP. Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            status: status,
            createdAtUtc: FixedNow.AddDays(-10),
            lastUpdatedAtUtc: lastUpdatedAtUtc ?? FixedNow,
            version: 1,
            amenities: amenities,
            safetyFeatures: safetyFeatures,
            currentHouseRules: houseRules);

    private static RoomDetails DetailsOf(
        Room room,
        IReadOnlyList<Amenity> amenities,
        IReadOnlyList<SafetyFeature> safetyFeatures) =>
        new(
            Title: room.Title,
            Description: room.Description,
            RoomType: room.RoomType,
            MonthlyRentVnd: room.MonthlyRentVnd,
            DepositVnd: room.DepositVnd,
            AreaM2: room.AreaM2,
            MaxOccupants: room.MaxOccupants,
            Furnishing: room.Furnishing,
            AvailableFrom: room.AvailableFrom,
            AddressLine: room.AddressLine,
            Ward: room.Ward,
            District: room.District,
            Latitude: room.Latitude,
            Longitude: room.Longitude,
            ElectricityPriceVndPerKwh: room.ElectricityPriceVndPerKwh,
            WaterPriceVnd: room.WaterPriceVnd,
            WaterPriceUnit: room.WaterPriceUnit,
            InternetFeeVnd: room.InternetFeeVnd,
            ParkingFeeVnd: room.ParkingFeeVnd,
            OtherFeesNote: room.OtherFeesNote,
            Amenities: amenities,
            SafetyFeatures: safetyFeatures,
            HasAddedNewPhotos: false);

    private static HouseRuleVersion NewHouseRules(
        string id,
        string roomId,
        string createdByUserId,
        int versionNumber) =>
        HouseRuleVersion.Reconstitute(
            id: id,
            roomId: roomId,
            versionNumber: versionNumber,
            guestPolicy: GuestPolicy.DaytimeOnly,
            cookingPolicy: CookingPolicy.SharedKitchenOnly,
            parkingAvailable: true,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: createdByUserId,
            createdAtUtc: FixedNow,
            quietHoursStart: "22:00",
            quietHoursEnd: "06:00",
            curfewTime: "23:30",
            guestPolicyNote: "Khách báo trước.",
            parkingRules: "Xe máy xếp hàng.",
            otherRules: "Không tổ chức tiệc.");

    private static RoomPhoto NewPhoto(
        string id,
        string uploadedByUserId = "landlord-1",
        string? roomId = null,
        int sortOrder = 0,
        string? thumb = null) =>
        RoomPhoto.Reconstitute(
            id: id,
            roomId: roomId,
            uploadedByUserId: uploadedByUserId,
            kind: PhotoKind.Standard,
            contentType: RoomPhoto.JpegContentType,
            sizeBytes: 204_800,
            width: 1280,
            height: 720,
            originalPath: $"rooms/2026/11/{id}/original.jpg",
            mediumPath: $"rooms/2026/11/{id}/medium.jpg",
            thumbPath: thumb ?? $"rooms/2026/11/{id}/thumb.jpg",
            sortOrder: sortOrder,
            createdAtUtc: FixedNow);

    private static void InsertUser(SqliteDbContext dbContext, string userId)
    {
        var connection = dbContext.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (Id, Email, NormalizedEmail, FullName, Role, AuthProvider, IsVerified, FailedLoginCount, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@id, @email, @normalizedEmail, 'Test Landlord', 'Landlord', 'Local', 1, 0, '2026-11-15T08:00:00.0000000Z', '2026-11-15T08:00:00.0000000Z');
            """;
        command.Parameters.AddWithValue("@id", userId);
        command.Parameters.AddWithValue("@email", userId + "@example.com");
        command.Parameters.AddWithValue("@normalizedEmail", userId.ToUpperInvariant() + "@EXAMPLE.COM");
        command.ExecuteNonQuery();
    }

    private long ReadScalar(string sql)
    {
        var connection = _dbContext.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
