using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// M1: <see cref="InMemoryRoomRepository"/> là adapter chỉ dùng trong test — không seed demo,
/// nhận dữ liệu qua constructor và trả bản sao để store không bị sửa qua alias.
/// </summary>
[TestFixture]
public class InMemoryRoomRepositoryTests
{
    [Test]
    public async Task NewRepository_WithoutSeed_IsEmpty()
    {
        IRoomRepository repository = new InMemoryRoomRepository();

        Assert.That(await repository.GetByIdAsync("any-id"), Is.Null);
    }

    [Test]
    public async Task Constructor_WithSeed_ExposesSeededRooms()
    {
        var room = NewRoom("mem-1");

        var repository = new InMemoryRoomRepository(new[] { room });

        var found = await repository.GetByIdAsync("mem-1");
        Assert.Multiple(() =>
        {
            Assert.That(found, Is.Not.Null);
            Assert.That(found!.Id, Is.EqualTo("mem-1"));
            Assert.That(found.Title, Is.EqualTo(room.Title));
            Assert.That(found.Status, Is.EqualTo(ListingStatus.PendingReview));
        });
    }

    [Test]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsRoom()
    {
        var repository = new InMemoryRoomRepository();
        var room = NewRoom("mem-2");

        await repository.AddAsync(room);

        var found = await repository.GetByIdAsync("mem-2");
        Assert.That(found?.Title, Is.EqualTo(room.Title));
    }

    [Test]
    public async Task UpdateAsync_ReplacesStoredRoom()
    {
        var repository = new InMemoryRoomRepository(new[] { NewRoom("mem-3") });
        var room = (await repository.GetByIdAsync("mem-3"))!;

        room.Approve(new DateTime(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc));
        await repository.UpdateAsync(room);

        var found = await repository.GetByIdAsync("mem-3");
        Assert.That(found!.Status, Is.EqualTo(ListingStatus.Available));
    }

    [Test]
    public async Task UpdateAsync_WithStaleVersion_ThrowsConcurrencyConflictException()
    {
        // GUARDRAILS §1.6.3: InMemory phải hành xử như Sqlite, không được nuốt xung đột.
        var repository = new InMemoryRoomRepository(new[] { NewRoom("mem-5") });
        var stale = (await repository.GetByIdAsync("mem-5"))!;
        var current = (await repository.GetByIdAsync("mem-5"))!;

        current.Approve(new DateTime(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc));
        await repository.UpdateAsync(current);

        stale.Approve(new DateTime(2026, 11, 13, 8, 0, 0, DateTimeKind.Utc));

        await Assert.ThatAsync(
            async () => await repository.UpdateAsync(stale),
            Throws.TypeOf<ConcurrencyConflictException>());
    }

    [Test]
    public async Task UpdateAsync_UnknownId_ThrowsConcurrencyConflictException()
    {
        var repository = new InMemoryRoomRepository();

        await Assert.ThatAsync(
            async () => await repository.UpdateAsync(NewRoom("missing")),
            Throws.TypeOf<ConcurrencyConflictException>());
    }

    [Test]
    public async Task AddAsync_DuplicateId_ThrowsDomainValidationExceptionWithDuplicateCode()
    {
        var repository = new InMemoryRoomRepository(new[] { NewRoom("mem-dup") });

        await Assert.ThatAsync(
            async () => await repository.AddAsync(NewRoom("mem-dup")),
            Throws.TypeOf<DomainValidationException>()
                .With.Property("ErrorCode").EqualTo("DUPLICATE_ROOM"));
    }

    [Test]
    public async Task GetByIdAsync_ReturnsCopy_SoCallersCannotMutateTheStore()
    {
        var repository = new InMemoryRoomRepository(new[] { NewRoom("mem-4") });

        var first = (await repository.GetByIdAsync("mem-4"))!;
        first.Approve(new DateTime(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc));
        var second = await repository.GetByIdAsync("mem-4");

        Assert.That(second!.Status, Is.EqualTo(ListingStatus.PendingReview));
    }

    [Test]
    public async Task AddAsync_NullRoom_ThrowsArgumentNullException()
    {
        var repository = new InMemoryRoomRepository();

        await Assert.ThatAsync(
            async () => await repository.AddAsync(null!),
            Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public async Task GetOwnedByIdAsync_OtherLandlord_ReturnsNullAndOwnerGetsRoom()
    {
        // GUARDRAILS §1.5.6 + §1.6.3: anti-IDOR giống SqliteRoomRepository.
        var repository = new InMemoryRoomRepository(new[] { NewRoom("mem-owned") });

        var foreign = await repository.GetOwnedByIdAsync("mem-owned", "landlord-other");
        var owner = await repository.GetOwnedByIdAsync("mem-owned", "landlord-1");

        Assert.Multiple(() =>
        {
            Assert.That(foreign, Is.Null);
            Assert.That(owner?.Id, Is.EqualTo("mem-owned"));
        });
    }

    [Test]
    public async Task CountNonArchivedByLandlordAsync_ExcludesUnpublished()
    {
        var repository = new InMemoryRoomRepository(new[]
        {
            NewRoom("mem-active"),
            NewRoom("mem-archived", ListingStatus.Unpublished),
            NewRoom("mem-foreign", ListingStatus.Available, landlordId: "landlord-2")
        });

        var count = await repository.CountNonArchivedByLandlordAsync("landlord-1");

        Assert.That(count, Is.EqualTo(1), "BR-LST-11: Unpublished không tính vào hạn mức.");
    }

    [Test]
    public async Task ListByLandlordAsync_ReturnsFilteredPageWithAllStatusCounts()
    {
        var repository = new InMemoryRoomRepository(new[]
        {
            NewRoom("mem-newest", ListingStatus.Available, lastUpdated: new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc)),
            NewRoom("mem-older", ListingStatus.Available, lastUpdated: new DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc)),
            NewRoom("mem-archived", ListingStatus.Unpublished, lastUpdated: new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc)),
            NewRoom("mem-foreign", ListingStatus.Available, lastUpdated: new DateTime(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc), landlordId: "landlord-2")
        });

        var page = await repository.ListByLandlordAsync("landlord-1", status: null, pageNumber: 1, pageSize: 2);
        var archivedOnly = await repository.ListByLandlordAsync("landlord-1", ListingStatus.Unpublished, 1, 20);

        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Select(item => item.Room.Id), Is.EqualTo(new[] { "mem-newest", "mem-older" }));
            Assert.That(page.Items.Select(item => item.CoverThumbPath), Is.All.Null,
                "InMemory không lưu ảnh nên cover luôn null — cùng contract với SQLite khi phòng chưa có ảnh.");
            Assert.That(page.TotalCount, Is.EqualTo(3));
            Assert.That(page.CountsByStatus.Keys, Is.EquivalentTo(Enum.GetValues<ListingStatus>()));
            Assert.That(page.CountsByStatus[ListingStatus.Available], Is.EqualTo(2));
            Assert.That(page.CountsByStatus[ListingStatus.PendingReview], Is.Zero);
            Assert.That(archivedOnly.Items.Select(item => item.Room.Id), Is.EqualTo(new[] { "mem-archived" }));
            Assert.That(archivedOnly.TotalCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Copy_CarriesAmenitiesSafetyFeaturesAndHouseRules()
    {
        var rules = HouseRuleVersion.Create(
            roomId: "mem-children",
            versionNumber: 1,
            guestPolicy: GuestPolicy.DaytimeOnly,
            cookingPolicy: CookingPolicy.NotAllowed,
            parkingAvailable: true,
            petsAllowed: false,
            smokingAllowed: false,
            createdByUserId: "admin-1",
            utcNow: new DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc));
        var room = NewRoom("mem-children");
        room.SetChildren(
            new[] { Amenity.Wifi },
            new[] { new RoomSafetyFeatureEntry(SafetyFeature.Cctv, IsConfirmed: true) },
            rules);
        var repository = new InMemoryRoomRepository(new[] { room });

        var found = await repository.GetByIdAsync("mem-children");

        Assert.Multiple(() =>
        {
            Assert.That(found!.Amenities, Is.EqualTo(new[] { Amenity.Wifi }));
            Assert.That(found.SafetyFeatures, Is.EqualTo(new[] { new RoomSafetyFeatureEntry(SafetyFeature.Cctv, true) }));
            Assert.That(found.CurrentHouseRules?.Id, Is.EqualTo(rules.Id));
        });
    }

    private static Room NewRoom(
        string id,
        ListingStatus status = ListingStatus.PendingReview,
        DateTime? lastUpdated = null,
        string landlordId = "landlord-1") => Room.Reconstitute(
        id: id,
        landlordId: landlordId,
        title: "Phòng mới trong bộ nhớ",
        roomType: RoomType.BoardingRoom,
        monthlyRentVnd: 1_800_000L,
        depositVnd: 0L,
        areaM2: 20.0,
        addressLine: "Tân Thịnh",
        ward: "Tân Thịnh",
        district: "TP. Thái Nguyên",
        latitude: 21.5851,
        longitude: 105.8066,
        status: status,
        createdAtUtc: new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc),
        lastUpdatedAtUtc: lastUpdated ?? new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc),
        version: 1);
}
