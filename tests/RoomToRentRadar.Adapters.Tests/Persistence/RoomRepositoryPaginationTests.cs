using RoomToRentRadar.Adapters.Outbound.Persistence.InMemory;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Adapters.Tests.Persistence;

[TestFixture]
public class RoomRepositoryPaginationTests
{
    private InMemoryRoomRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryRoomRepository();
        for (int i = 1; i <= 5; i++)
        {
            _repository.Upsert(new Room
            {
                Id = $"room-{i}",
                Title = $"Room {i}",
                Description = $"Description {i}",
                Price = 1_000_000m * i,
                Area = 20.0 + i,
                RoomType = "Single",
                IsVerified = true,
                Status = RoomStatus.Available,
                Address = "Test Address",
                Amenities = ["Wifi"],
                HouseRules = ["No smoking"]
            });
        }
    }

    [Test]
    public async Task SearchAsync_WhenPageNumberVeryLarge_ShouldNotThrowAndReturnEmpty()
    {
        var query = new SearchRoomsQuery(pageNumber: 100_000, pageSize: 50);

        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.That(totalCount, Is.EqualTo(5));
        Assert.That(items, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_WhenPageNumberCausesIntegerOverflow_ShouldNotThrowAndReturnEmpty()
    {
        var query = new SearchRoomsQuery(pageNumber: int.MaxValue, pageSize: 50);

        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.That(totalCount, Is.EqualTo(5));
        Assert.That(items, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_WhenOffsetExceedsTotalCount_ShouldReturnEmpty()
    {
        var query = new SearchRoomsQuery(pageNumber: 2, pageSize: 10);

        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.That(totalCount, Is.EqualTo(5));
        Assert.That(items, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_NormalPagination_ShouldReturnCorrectSlice()
    {
        var query = new SearchRoomsQuery(pageNumber: 1, pageSize: 2);

        var (items, totalCount) = await _repository.SearchAsync(query);

        Assert.That(totalCount, Is.EqualTo(5));
        Assert.That(items.Count, Is.EqualTo(2));
        Assert.That(items[0].Id, Is.EqualTo("room-1"));
        Assert.That(items[1].Id, Is.EqualTo("room-2"));
    }

    [Test]
    public async Task GetSimilarRoomsAsync_WhenPriceMatchesButAreaOutOfRange_ShouldExcludeRoom()
    {
        // Target: price = 3,000,000, area = 30.0 (Area range 21.0 - 39.0)
        var repo = new InMemoryRoomRepository();
        var targetRoom = new Room
        {
            Id = "target-room",
            Title = "Target",
            Price = 3000000m,
            Area = 30.0,
            Status = RoomStatus.Rented,
            IsVerified = true
        };
        var matchingRoom = new Room
        {
            Id = "match-room",
            Title = "Match Both Price and Area",
            Price = 3100000m, // within 2.1M - 3.9M
            Area = 32.0,      // within 21.0 - 39.0
            Status = RoomStatus.Available,
            IsVerified = true
        };
        var areaTooSmallRoom = new Room
        {
            Id = "area-too-small",
            Title = "Price Matches But Area Too Small",
            Price = 3000000m, // within range
            Area = 15.0,      // < 21.0 (out of range!)
            Status = RoomStatus.Available,
            IsVerified = true
        };
        var areaTooLargeRoom = new Room
        {
            Id = "area-too-large",
            Title = "Price Matches But Area Too Large",
            Price = 3000000m, // within range
            Area = 45.0,      // > 39.0 (out of range!)
            Status = RoomStatus.Available,
            IsVerified = true
        };

        repo.Upsert(targetRoom);
        repo.Upsert(matchingRoom);
        repo.Upsert(areaTooSmallRoom);
        repo.Upsert(areaTooLargeRoom);

        var similarRooms = await repo.GetSimilarRoomsAsync("target-room", price: 3000000m, area: 30.0);

        Assert.That(similarRooms, Is.Not.Null);
        var similarIds = similarRooms.Select(r => r.Id).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(similarIds, Does.Contain(matchingRoom.Id));
            Assert.That(similarIds, Does.Not.Contain(targetRoom.Id));
            Assert.That(similarIds, Does.Not.Contain(areaTooSmallRoom.Id), "Phòng có diện tích quá nhỏ (15m2) phải bị loại.");
            Assert.That(similarIds, Does.Not.Contain(areaTooLargeRoom.Id), "Phòng có diện tích quá lớn (45m2) phải bị loại.");
        });
    }
}
