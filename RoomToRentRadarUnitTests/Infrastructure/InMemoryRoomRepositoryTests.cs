using RoomToRentRadar.Domain.Aggregates;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
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

    private static Room NewRoom(string id) => Room.Reconstitute(
        id: id,
        landlordId: "landlord-1",
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
        status: ListingStatus.PendingReview,
        createdAtUtc: new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc),
        lastUpdatedAtUtc: new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc),
        version: 1);
}
