using RoomToRentRadar.Adapters.Outbound.Persistence.InMemory;
using RoomToRentRadar.Application.DTOs;
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
            Status = RoomStatus.Available,
            IsVerified = true
        };

        // 2. Giá phù hợp nhưng Diện tích quá NHỎ (< 21.0)
        var areaTooSmallRoom = new Room
        {
            Id = "area-too-small",
            Title = "Phòng giá phù hợp nhưng diện tích quá nhỏ",
            Price = 3000000m, // hợp lệ
            Area = 18.0,      // < 21.0 (vi phạm)
            Status = RoomStatus.Available,
            IsVerified = true
        };

        // 3. Giá phù hợp nhưng Diện tích quá LỚN (> 39.0)
        var areaTooLargeRoom = new Room
        {
            Id = "area-too-large",
            Title = "Phòng giá phù hợp nhưng diện tích quá lớn",
            Price = 3000000m, // hợp lệ
            Area = 45.0,      // > 39.0 (vi phạm)
            Status = RoomStatus.Available,
            IsVerified = true
        };

        // 4. Diện tích phù hợp nhưng Giá quá RẺ (< 2,100,000)
        var priceTooCheapRoom = new Room
        {
            Id = "price-too-cheap",
            Title = "Phòng diện tích phù hợp nhưng giá quá rẻ",
            Price = 1800000m, // < 2.1m (vi phạm)
            Area = 30.0,      // hợp lệ
            Status = RoomStatus.Available,
            IsVerified = true
        };

        // 5. Diện tích phù hợp nhưng Giá quá ĐẮT (> 3,900,000)
        var priceTooExpensiveRoom = new Room
        {
            Id = "price-too-expensive",
            Title = "Phòng diện tích phù hợp nhưng giá quá đắt",
            Price = 4500000m, // > 3.9m (vi phạm)
            Area = 30.0,      // hợp lệ
            Status = RoomStatus.Available,
            IsVerified = true
        };

        // 6. Phù hợp giá và diện tích nhưng không Available
        var notAvailableRoom = new Room
        {
            Id = "not-available",
            Title = "Phòng phù hợp nhưng đã được thuê",
            Price = 3000000m,
            Area = 30.0,
            Status = RoomStatus.Rented,
            IsVerified = true
        };

        // 7. Phù hợp giá và diện tích nhưng chưa xác minh (IsVerified = false)
        var unverifiedRoom = new Room
        {
            Id = "unverified-room",
            Title = "Phòng phù hợp nhưng chưa xác minh",
            Price = 3000000m,
            Area = 30.0,
            Status = RoomStatus.Available,
            IsVerified = false
        };

        _repository.Upsert(targetRoom);
        _repository.Upsert(validRoom);
        _repository.Upsert(areaTooSmallRoom);
        _repository.Upsert(areaTooLargeRoom);
        _repository.Upsert(priceTooCheapRoom);
        _repository.Upsert(priceTooExpensiveRoom);
        _repository.Upsert(notAvailableRoom);
        _repository.Upsert(unverifiedRoom);

        // Act
        var results = await _repository.GetSimilarRoomsAsync("target-room", price: 3000000m, area: 30.0);

        // Assert
        Assert.That(results, Is.Not.Null);
        var resultIds = results.Select(r => r.Id).ToList();

        Assert.Multiple(() =>
        {
            // Phòng thỏa mãn cả giá và diện tích và đã xác minh phải có mặt
            Assert.That(resultIds, Does.Contain(validRoom.Id));

            // Phòng có giá phù hợp nhưng diện tích nằm ngoài khoảng cho phép PHẢI BỊ LOẠI
            Assert.That(resultIds, Does.Not.Contain(areaTooSmallRoom.Id), "Phòng có diện tích < 70% phải bị loại");
            Assert.That(resultIds, Does.Not.Contain(areaTooLargeRoom.Id), "Phòng có diện tích > 130% phải bị loại");

            // Các phòng vi phạm điều kiện khác cũng phải bị loại
            Assert.That(resultIds, Does.Not.Contain(priceTooCheapRoom.Id));
            Assert.That(resultIds, Does.Not.Contain(priceTooExpensiveRoom.Id));
            Assert.That(resultIds, Does.Not.Contain(targetRoom.Id));
            Assert.That(resultIds, Does.Not.Contain(notAvailableRoom.Id));
            Assert.That(resultIds, Does.Not.Contain(unverifiedRoom.Id), "Phòng chưa xác minh (IsVerified = false) phải bị loại");
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
                Status = RoomStatus.Available,
                IsVerified = true
            });
        }

        var results = await _repository.GetSimilarRoomsAsync("target", price: 2000000m, area: 25.0);

        Assert.That(results.Count, Is.EqualTo(5));
    }

    [Test]
    public async Task SearchAsync_WhenKeywordHasWhitespace_ShouldTrimAndFindMatchingRooms()
    {
        var matchingRoom = new Room
        {
            Id = "room-keyword-match",
            Title = "Phòng trọ cao cấp gần trường",
            Price = 2500000m,
            Area = 28.0,
            Status = RoomStatus.Available,
            IsVerified = true
        };
        var nonMatchingRoom = new Room
        {
            Id = "room-keyword-non-match",
            Title = "Nhà nguyên căn",
            Price = 5000000m,
            Area = 50.0,
            Status = RoomStatus.Available,
            IsVerified = true
        };

        _repository.Upsert(matchingRoom);
        _repository.Upsert(nonMatchingRoom);

        var query = new SearchRoomsQuery(keyword: "   cao cấp   ");

        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.That(totalCount, Is.EqualTo(1));
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].Id, Is.EqualTo(matchingRoom.Id));
    }

    [Test]
    public async Task SearchAsync_WhenKeywordIsWhitespaceOnly_ShouldNotFilterByKeyword()
    {
        var room1 = new Room
        {
            Id = "room-1",
            Title = "Phòng 1",
            Price = 2000000m,
            Area = 25.0,
            Status = RoomStatus.Available,
            IsVerified = true
        };
        var room2 = new Room
        {
            Id = "room-2",
            Title = "Phòng 2",
            Price = 3000000m,
            Area = 30.0,
            Status = RoomStatus.Available,
            IsVerified = true
        };

        _repository.Upsert(room1);
        _repository.Upsert(room2);

        var query = new SearchRoomsQuery(keyword: "   ");

        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.That(totalCount, Is.EqualTo(2));
        Assert.That(items, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SearchAsync_WhenSortedByNewest_ShouldOrderByCreatedAtDescending_EvenWhenUpdatedLater()
    {
        // Arrange
        // Room A: Created earlier (Nov 1), but updated very recently (Nov 20)
        var roomA = new Room
        {
            Id = "room-older-created-recent-update",
            Title = "Phòng cũ vừa cập nhật",
            Price = 2000000m,
            Area = 20.0,
            Status = RoomStatus.Available,
            IsVerified = true,
            CreatedAt = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            LastUpdatedDate = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc)
        };

        // Room B: Created later (Nov 15), but not updated since creation (Nov 15)
        var roomB = new Room
        {
            Id = "room-newer-created",
            Title = "Phòng mới tạo",
            Price = 2000000m,
            Area = 20.0,
            Status = RoomStatus.Available,
            IsVerified = true,
            CreatedAt = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc),
            LastUpdatedDate = new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        _repository.Upsert(roomA);
        _repository.Upsert(roomB);

        var query = new SearchRoomsQuery(sortBy: "Newest", pageSize: 10);

        // Act
        var (items, totalCount) = await _repository.SearchAsync(query);

        // Assert: Newest sorts by CreatedAt DESC, so roomB (Nov 15) must come BEFORE roomA (Nov 1)
        Assert.Multiple(() =>
        {
            Assert.That(totalCount, Is.EqualTo(2));
            Assert.That(items, Has.Count.EqualTo(2));
            Assert.That(items[0].Id, Is.EqualTo(roomB.Id), "Room with more recent CreatedAt must be first.");
            Assert.That(items[1].Id, Is.EqualTo(roomA.Id), "Room with older CreatedAt must be second even if LastUpdatedDate is newer.");
        });
    }

    [Test]
    public async Task SearchAsync_WhenAmenitiesCombinedWithAreaAndDistance_ShouldReturnOnlyRoomsMatchingAllCriteria()
    {
        // Arrange: ICTU coordinates: 21.5852, 105.8073
        var matchingRoom = new Room
        {
            Id = "room-match",
            Title = "Phòng trọ phù hợp",
            Price = 2000000m,
            Area = 25.0,
            Latitude = 21.5860,
            Longitude = 105.8080,
            Status = RoomStatus.Available,
            IsVerified = true,
            Amenities = ["Wifi", "Aircon", "Parking"]
        };

        var roomMissingAmenity = new Room
        {
            Id = "room-missing-amenity",
            Title = "Phòng thiếu tiện ích",
            Price = 2000000m,
            Area = 25.0,
            Latitude = 21.5860,
            Longitude = 105.8080,
            Status = RoomStatus.Available,
            IsVerified = true,
            Amenities = ["Wifi"]
        };

        var roomOutOfArea = new Room
        {
            Id = "room-out-of-area",
            Title = "Phòng quá nhỏ",
            Price = 2000000m,
            Area = 15.0,
            Latitude = 21.5860,
            Longitude = 105.8080,
            Status = RoomStatus.Available,
            IsVerified = true,
            Amenities = ["Wifi", "Aircon"]
        };

        var roomTooFar = new Room
        {
            Id = "room-too-far",
            Title = "Phòng quá xa",
            Price = 2000000m,
            Area = 25.0,
            Latitude = 21.7000,
            Longitude = 105.9000,
            Status = RoomStatus.Available,
            IsVerified = true,
            Amenities = ["Wifi", "Aircon"]
        };

        _repository.Upsert(matchingRoom);
        _repository.Upsert(roomMissingAmenity);
        _repository.Upsert(roomOutOfArea);
        _repository.Upsert(roomTooFar);

        var query = new SearchRoomsQuery(
            minArea: 20.0,
            maxArea: 30.0,
            latitude: 21.5852,
            longitude: 105.8073,
            radiusInKm: 5.0,
            amenities: ["wifi", "aircon"]);

        // Act
        var (items, totalCount) = await _repository.SearchAsync(query);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(totalCount, Is.EqualTo(1));
            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0].Id, Is.EqualTo("room-match"));
        });
    }
}
