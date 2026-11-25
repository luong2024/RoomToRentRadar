using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Tests;

public class GetRoomDetailsUseCaseTests
{
    private FakeRoomRepository _roomRepository = null!;
    private GetRoomDetailsUseCase _useCase = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepository = new FakeRoomRepository();
        _useCase = new GetRoomDetailsUseCase(_roomRepository);
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomNotFound_ShouldThrowNotFoundException()
    {
        Assert.ThrowsAsync<NotFoundException>(async () =>
            await _useCase.ExecuteAsync("non-existent", isUserVerified: true));
    }

    [TestCase(RoomStatus.Available, true, true)]
    [TestCase(RoomStatus.Available, false, false)]
    [TestCase(RoomStatus.Rented, true, false)]
    public async Task ExecuteAsync_CoordinatesAreVisibleOnlyForAvailableVerifiedRooms(RoomStatus status, bool verified, bool expectedVisible)
    {
        _roomRepository.Add(new Room { Id = "coordinate-room", Status = status, IsVerified = verified, Latitude = 21.5, Longitude = 105.8 });

        var result = await _useCase.ExecuteAsync("coordinate-room", isUserVerified: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Latitude, Is.EqualTo(expectedVisible ? 21.5 : null));
            Assert.That(result.Longitude, Is.EqualTo(expectedVisible ? 105.8 : null));
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomIsAvailableAndUserVerified_ShouldReturnActualPhoneAndFullDetails()
    {
        var room = new Room
        {
            Id = "room-01",
            Title = "Phòng trọ đẹp",
            Price = 2000000m,
            Area = 20,
            Address = "123 Đường Láng",
            LandlordPhone = "0987654321",
            Status = RoomStatus.Available,
            IsVerified = true
        };
        _roomRepository.Add(room);

        var result = await _useCase.ExecuteAsync("room-01", isUserVerified: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.True);
            Assert.That(result.UnavailableMessage, Is.Null);
            Assert.That(result.Address, Is.EqualTo("123 Đường Láng"));
            Assert.That(result.LandlordPhone, Is.EqualTo("0987654321"));
            Assert.That(result.SimilarRooms, Is.Empty);
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomIsAvailableAndUserUnverified_ShouldMaskLandlordPhone()
    {
        var room = new Room
        {
            Id = "room-02",
            Title = "Phòng trọ giá tốt",
            Price = 1800000m,
            Area = 18,
            Address = "456 Cầu Giấy",
            LandlordPhone = "0987654321",
            Status = RoomStatus.Available,
            IsVerified = true
        };
        _roomRepository.Add(room);

        var result = await _useCase.ExecuteAsync("room-02", isUserVerified: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.True);
            Assert.That(result.Address, Is.EqualTo("456 Cầu Giấy"));
            Assert.That(result.LandlordPhone, Is.EqualTo("*** (Yêu cầu xác thực để xem)"));
        });
    }

    [Test]
    public async Task ExecuteAsync_WhenRoomIsUnavailable_ShouldMaskSensitiveDataAndReturnSimilarRooms()
    {
        var unavailableRoom = new Room
        {
            Id = "room-03",
            Title = "Phòng trọ đã thuê",
            Price = 2000000m,
            Area = 22,
            Address = "789 Kim Mã",
            ThumbnailUrl = "https://cdn.example.test/unavailable-thumbnail.jpg",
            ImageUrls = ["https://cdn.example.test/unavailable-image.jpg"],
            LandlordPhone = "0987654321",
            Status = RoomStatus.Rented,
            IsVerified = true
        };
        _roomRepository.Add(unavailableRoom);

        var similarRoom = new Room
        {
            Id = "room-04",
            Title = "Phòng tương tự",
            Price = 2100000m,
            Area = 20,
            ThumbnailUrl = "https://cdn.example.test/room-04.jpg",
            Address = "790 Kim Mã",
            Status = RoomStatus.Available,
            IsVerified = true
        };
        _roomRepository.Add(similarRoom);

        var result = await _useCase.ExecuteAsync("room-03", isUserVerified: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.UnavailableMessage, Is.EqualTo("Phòng này hiện không khả dụng."));
            Assert.That(result.Address, Is.Empty);
            Assert.That(result.LandlordPhone, Is.Empty);
            Assert.That(result.ThumbnailUrl, Is.Null);
            Assert.That(result.ImageUrls, Is.Empty);
            Assert.That(result.SimilarRooms, Has.Count.EqualTo(1));
            Assert.That(result.SimilarRooms[0].Id, Is.EqualTo("room-04"));
            Assert.That(result.SimilarRooms[0].ThumbnailUrl, Is.EqualTo("https://cdn.example.test/room-04.jpg"));
        });
    }

    private sealed class FakeRoomRepository : IRoomRepository
    {
        private readonly List<Room> _rooms = [];

        public void Add(Room room) => _rooms.Add(room);

        public Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var match = _rooms.FirstOrDefault(r => r.Id == id);
            return Task.FromResult(match);
        }

        public Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default)
        {
            var results = _rooms
                .Where(r => r.Id != roomId && r.Status == RoomStatus.Available)
                .ToList();
            return Task.FromResult<IReadOnlyList<Room>>(results);
        }

        public Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchAsync(
            RoomToRentRadar.Application.DTOs.SearchRoomsQuery query,
            CancellationToken ct = default)
        {
            return Task.FromResult<(IReadOnlyList<Room> Items, int TotalCount)>((_rooms.AsReadOnly(), _rooms.Count));
        }
    }
}
