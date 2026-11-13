using RoomToRentRadar.Adapters.Outbound.Persistence.InMemory;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Tests.Persistence;

[TestFixture]
public class InMemoryRoomRepositoryTests
{
    private InMemoryRoomRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryRoomRepository();
    }

    [Test]
    public async Task GetByIdAsync_WhenIdNotFound_ShouldReturnNull()
    {
        var result = await _repository.GetByIdAsync("non-existent");
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByIdAsync_WhenExists_ShouldReturnRoom()
    {
        var room = new Room
        {
            Id = "room-1",
            Title = "Phòng trọ 1",
            Price = 2000000m,
            Area = 25,
            Status = RoomStatus.Available
        };
        _repository.Upsert(room);

        var result = await _repository.GetByIdAsync("room-1");
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("room-1"));
    }

    [Test]
    public async Task GetSimilarRoomsAsync_WhenPriceMatchesButAreaOutsideRange_ShouldExcludeOutOfRangeRooms()
    {
        // Target: Price = 3,000,000 (valid range: 2,100,000 - 3,900,000)
        //         Area = 30.0 (valid range: 21.0 - 39.0)
        var targetRoom = new Room
        {
            Id = "target-room",
            Title = "Phòng mục tiêu",
            Price = 3000000m,
            Area = 30.0,
            Status = RoomStatus.Rented
        };

        // 1. Phù hợp cả giá và diện tích
        var validRoom = new Room
        {
            Id = "valid-room",
            Title = "Phòng phù hợp cả giá và diện tích",
            Price = 3100000m, // trong khoảng [2.1m, 3.9m]
            Area = 31.0,      // trong khoảng [21.0, 39.0]
            Status = RoomStatus.Available
        };

        // 2. Giá phù hợp nhưng Diện tích quá NHỎ (< 21.0)
        var areaTooSmallRoom = new Room
        {
            Id = "area-too-small",
            Title = "Phòng giá phù hợp nhưng diện tích quá nhỏ",
            Price = 3000000m, // hợp lệ
            Area = 18.0,      // < 21.0 (vi phạm)
            Status = RoomStatus.Available
        };

        // 3. Giá phù hợp nhưng Diện tích quá LỚN (> 39.0)
        var areaTooLargeRoom = new Room
        {
            Id = "area-too-large",
            Title = "Phòng giá phù hợp nhưng diện tích quá lớn",
            Price = 3000000m, // hợp lệ
            Area = 45.0,      // > 39.0 (vi phạm)
            Status = RoomStatus.Available
        };

        // 4. Diện tích phù hợp nhưng Giá quá RẺ (< 2,100,000)
        var priceTooCheapRoom = new Room
        {
            Id = "price-too-cheap",
            Title = "Phòng diện tích phù hợp nhưng giá quá rẻ",
            Price = 1800000m, // < 2.1m (vi phạm)
            Area = 30.0,      // hợp lệ
            Status = RoomStatus.Available
        };

        // 5. Diện tích phù hợp nhưng Giá quá ĐẮT (> 3,900,000)
        var priceTooExpensiveRoom = new Room
        {
            Id = "price-too-expensive",
            Title = "Phòng diện tích phù hợp nhưng giá quá đắt",
            Price = 4500000m, // > 3.9m (vi phạm)
            Area = 30.0,      // hợp lệ
            Status = RoomStatus.Available
        };

        // 6. Phù hợp giá và diện tích nhưng không Available
        var notAvailableRoom = new Room
        {
            Id = "not-available",
            Title = "Phòng phù hợp nhưng đã được thuê",
            Price = 3000000m,
            Area = 30.0,
            Status = RoomStatus.Rented
        };

        _repository.Upsert(targetRoom);
        _repository.Upsert(validRoom);
        _repository.Upsert(areaTooSmallRoom);
        _repository.Upsert(areaTooLargeRoom);
        _repository.Upsert(priceTooCheapRoom);
        _repository.Upsert(priceTooExpensiveRoom);
        _repository.Upsert(notAvailableRoom);

        // Act
        var results = await _repository.GetSimilarRoomsAsync("target-room", price: 3000000m, area: 30.0);

        // Assert
        Assert.That(results, Is.Not.Null);
        var resultIds = results.Select(r => r.Id).ToList();

        Assert.Multiple(() =>
        {
            // Phòng thỏa mãn cả giá và diện tích phải có mặt
            Assert.That(resultIds, Does.Contain(validRoom.Id));

            // Phòng có giá phù hợp nhưng diện tích nằm ngoài khoảng cho phép PHẢI BỊ LOẠI
            Assert.That(resultIds, Does.Not.Contain(areaTooSmallRoom.Id), "Phòng có diện tích < 70% phải bị loại");
            Assert.That(resultIds, Does.Not.Contain(areaTooLargeRoom.Id), "Phòng có diện tích > 130% phải bị loại");

            // Các phòng vi phạm điều kiện khác cũng phải bị loại
            Assert.That(resultIds, Does.Not.Contain(priceTooCheapRoom.Id));
            Assert.That(resultIds, Does.Not.Contain(priceTooExpensiveRoom.Id));
            Assert.That(resultIds, Does.Not.Contain(targetRoom.Id));
            Assert.That(resultIds, Does.Not.Contain(notAvailableRoom.Id));
        });
    }

    [Test]
    public async Task GetSimilarRoomsAsync_WhenMoreThanFiveMatchingRooms_ShouldLimitToFive()
    {
        for (var i = 1; i <= 8; i++)
        {
            _repository.Upsert(new Room
            {
                Id = $"room-{i}",
                Title = $"Phòng {i}",
                Price = 2000000m,
                Area = 25.0,
                Status = RoomStatus.Available
            });
        }

        var results = await _repository.GetSimilarRoomsAsync("target", price: 2000000m, area: 25.0);

        Assert.That(results.Count, Is.EqualTo(5));
    }
}
