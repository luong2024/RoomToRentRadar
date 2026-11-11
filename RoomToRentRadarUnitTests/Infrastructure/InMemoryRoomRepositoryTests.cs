using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Infrastructure;

/// <summary>
/// RED ở Task 2: <see cref="InMemoryRoomRepository"/> do Task 3 triển khai.
/// Bộ test này sẽ chuyển GREEN sau Task 3 mà không cần sửa (nếu Task 3 giữ đúng seed 6 phòng + 1 phòng chưa xác minh như plan).
/// </summary>
[TestFixture]
public class InMemoryRoomRepositoryTests
{
    private static readonly (string Title, decimal Price, double Area, string Address)[] ExpectedSeed =
    {
        ("Phòng trọ Sinh viên Z115", 1_500_000m, 18, "21.5875, 105.8080"),
        ("Chung cư mini Tân Thịnh", 2_800_000m, 28, "21.5790, 105.8120"),
        ("Nhà trọ Thảo Nguyên - Quyết Thắng", 1_200_000m, 15, "21.5900, 105.8020"),
        ("Phòng cao cấp Thịnh Đán", 3_500_000m, 35, "21.5710, 105.8190"),
        ("Ký túc xá tư nhân Đường 3/2", 2_000_000m, 22, "21.5600, 105.8250"),
        ("Nhà trọ sinh viên giá rẻ Đồng Hỷ", 900_000m, 12, "21.6200, 105.8500")
    };

    [Test]
    public async Task GetAllAsync_ReturnsSeededRoomsAroundIctu()
    {
        var repository = new InMemoryRoomRepository();

        var rooms = await repository.GetAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rooms, Is.Not.Empty);
            Assert.That(rooms.Select(r => r.Title), Is.SupersetOf(ExpectedSeed.Select(s => s.Title)));

            foreach (var (title, price, area, coordinates) in ExpectedSeed)
            {
                var room = rooms.Single(r => r.Title == title);
                var (latitude, longitude) = ParseCoordinates(coordinates);

                Assert.That(room.Price, Is.EqualTo(price), $"{title} - Price");
                Assert.That(room.Area, Is.EqualTo(area), $"{title} - Area");
                Assert.That(room.Latitude, Is.EqualTo(latitude), $"{title} - Latitude");
                Assert.That(room.Longitude, Is.EqualTo(longitude), $"{title} - Longitude");
            }
        });
    }

    [Test]
    public async Task GetAllAsync_SeedContainsAtLeastOneRoomsWithLimitFilterableData()
    {
        var repository = new InMemoryRoomRepository();

        var rooms = await repository.GetAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rooms, Has.Some.Matches<Room>(r => !r.IsVerified), "Cần ít nhất 1 phòng chưa xác minh để test bộ lọc");
            Assert.That(rooms, Has.Some.Matches<Room>(r => !r.IsAvailable), "Cần ít nhất 1 phòng đã thuê để test bộ lọc");
            Assert.That(rooms.Where(r => r.IsVerified && r.IsAvailable), Has.Exactly(6).Items);
        });
    }

    [Test]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsRoom()
    {
        var repository = new InMemoryRoomRepository();
        var room = Room.Create(
            title: "Phòng mới",
            description: "Mô tả phòng mới",
            price: 1_800_000m,
            area: 20,
            address: "Tân Thịnh, Thái Nguyên",
            latitude: 21.5851,
            longitude: 105.8066,
            landlordId: "landlord-1");

        await repository.AddAsync(room);
        var found = await repository.GetByIdAsync(room.Id);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.Not.Null);
            Assert.That(found!.Id, Is.EqualTo(room.Id));
        });
    }

    [Test]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var repository = new InMemoryRoomRepository();

        var found = await repository.GetByIdAsync("does-not-exist");

        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task AddAsync_NullRoom_ThrowsArgumentNullException()
    {
        var repository = new InMemoryRoomRepository();

        Assert.ThrowsAsync<ArgumentNullException>(async () => await repository.AddAsync(null!));
    }

    [Test]
    public async Task GetAllAsync_ReturnsCopy_SoExternalMutationDoesNotAffectStore()
    {
        var repository = new InMemoryRoomRepository();
        var before = (await repository.GetAllAsync()).Count;

        var snapshot = await repository.GetAllAsync();
        if (snapshot is ICollection<Room> mutable)
        {
            mutable.Clear();
        }

        var after = await repository.GetAllAsync();
        Assert.That(after, Has.Count.EqualTo(before));
    }

    [Test]
    public async Task Repository_ImplementsIRoomRepositoryContract()
    {
        IRoomRepository repository = new InMemoryRoomRepository();

        var rooms = await repository.GetAllAsync(CancellationToken.None);

        Assert.That(rooms, Is.Not.Null);
    }

    private static (double Latitude, double Longitude) ParseCoordinates(string value)
    {
        var parts = value.Split(", ");
        return (double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }
}
