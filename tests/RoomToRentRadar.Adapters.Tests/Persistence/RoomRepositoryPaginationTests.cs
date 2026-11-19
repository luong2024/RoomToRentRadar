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

    [Test]
    public async Task SearchAsync_WithPriceAscSort_SortsAcrossMultiplePagesBeforePagination()
    {
        // Arrange
        var repo = new InMemoryRoomRepository();
        // Insert in scrambled order
        repo.Upsert(new Room { Id = "room-5m", Title = "Room 5M", Price = 5_000_000m, Area = 20, IsVerified = true, Status = RoomStatus.Available });
        repo.Upsert(new Room { Id = "room-1m", Title = "Room 1M", Price = 1_000_000m, Area = 20, IsVerified = true, Status = RoomStatus.Available });
        repo.Upsert(new Room { Id = "room-4m", Title = "Room 4M", Price = 4_000_000m, Area = 20, IsVerified = true, Status = RoomStatus.Available });
        repo.Upsert(new Room { Id = "room-2m", Title = "Room 2M", Price = 2_000_000m, Area = 20, IsVerified = true, Status = RoomStatus.Available });
        repo.Upsert(new Room { Id = "room-3m", Title = "Room 3M", Price = 3_000_000m, Area = 20, IsVerified = true, Status = RoomStatus.Available });

        // Act: Fetch Page 1 (size 2) and Page 2 (size 2)
        var page1Query = new SearchRoomsQuery(sortBy: "PriceAsc", pageNumber: 1, pageSize: 2);
        var (page1Items, totalCount1) = await repo.SearchAsync(page1Query);

        var page2Query = new SearchRoomsQuery(sortBy: "PriceAsc", pageNumber: 2, pageSize: 2);
        var (page2Items, totalCount2) = await repo.SearchAsync(page2Query);

        var page3Query = new SearchRoomsQuery(sortBy: "PriceAsc", pageNumber: 3, pageSize: 2);
        var (page3Items, totalCount3) = await repo.SearchAsync(page3Query);

        // Assert: Cross-page sorting guarantees Page 1 max price <= Page 2 min price <= Page 3 min price
        Assert.Multiple(() =>
        {
            Assert.That(totalCount1, Is.EqualTo(5));
            Assert.That(totalCount2, Is.EqualTo(5));
            Assert.That(totalCount3, Is.EqualTo(5));

            Assert.That(page1Items.Select(r => r.Price), Is.EqualTo(new[] { 1_000_000m, 2_000_000m }));
            Assert.That(page2Items.Select(r => r.Price), Is.EqualTo(new[] { 3_000_000m, 4_000_000m }));
            Assert.That(page3Items.Select(r => r.Price), Is.EqualTo(new[] { 5_000_000m }));

            Assert.That(page1Items.Max(r => r.Price), Is.LessThan(page2Items.Min(r => r.Price)), "Page 1 max must be less than Page 2 min");
            Assert.That(page2Items.Max(r => r.Price), Is.LessThan(page3Items.Min(r => r.Price)), "Page 2 max must be less than Page 3 min");
        });
    }

    [Test]
    public async Task SearchAsync_WithPriceDescSort_SortsAcrossMultiplePagesBeforePagination()
    {
        // Arrange
        var repo = new InMemoryRoomRepository();
        repo.Upsert(new Room { Id = "room-1m", Title = "Room 1M", Price = 1_000_000m, Area = 20, IsVerified = true, Status = RoomStatus.Available });
        repo.Upsert(new Room { Id = "room-5m", Title = "Room 5M", Price = 5_000_000m, Area = 20, IsVerified = true, Status = RoomStatus.Available });
        repo.Upsert(new Room { Id = "room-2m", Title = "Room 2M", Price = 2_000_000m, Area = 20, IsVerified = true, Status = RoomStatus.Available });
        repo.Upsert(new Room { Id = "room-4m", Title = "Room 4M", Price = 4_000_000m, Area = 20, IsVerified = true, Status = RoomStatus.Available });
        repo.Upsert(new Room { Id = "room-3m", Title = "Room 3M", Price = 3_000_000m, Area = 20, IsVerified = true, Status = RoomStatus.Available });

        // Act
        var page1Query = new SearchRoomsQuery(sortBy: "PriceDesc", pageNumber: 1, pageSize: 2);
        var (page1Items, _) = await repo.SearchAsync(page1Query);

        var page2Query = new SearchRoomsQuery(sortBy: "PriceDesc", pageNumber: 2, pageSize: 2);
        var (page2Items, _) = await repo.SearchAsync(page2Query);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(page1Items.Select(r => r.Price), Is.EqualTo(new[] { 5_000_000m, 4_000_000m }));
            Assert.That(page2Items.Select(r => r.Price), Is.EqualTo(new[] { 3_000_000m, 2_000_000m }));
            Assert.That(page1Items.Min(r => r.Price), Is.GreaterThan(page2Items.Max(r => r.Price)));
        });
    }

    [Test]
    public async Task SearchAsync_WithDistanceAscSort_SortsAcrossMultiplePagesBeforePagination()
    {
        // Arrange: User at ICTU (21.5852, 105.8073)
        var repo = new InMemoryRoomRepository();
        // Room Near (~0.3km)
        repo.Upsert(new Room { Id = "room-near", Title = "Near", Price = 2_000_000m, Latitude = 21.587, Longitude = 105.808, IsVerified = true, Status = RoomStatus.Available });
        // Room Far (~10km)
        repo.Upsert(new Room { Id = "room-far", Title = "Far", Price = 1_500_000m, Latitude = 21.650, Longitude = 105.850, IsVerified = true, Status = RoomStatus.Available });
        // Room Mid (~2km)
        repo.Upsert(new Room { Id = "room-mid", Title = "Mid", Price = 1_800_000m, Latitude = 21.600, Longitude = 105.815, IsVerified = true, Status = RoomStatus.Available });

        // Act: Page 1 (size 1) and Page 2 (size 1)
        var queryPage1 = new SearchRoomsQuery(latitude: 21.5852, longitude: 105.8073, sortBy: "DistanceAsc", pageNumber: 1, pageSize: 1);
        var (page1Items, totalCount1) = await repo.SearchAsync(queryPage1);

        var queryPage2 = new SearchRoomsQuery(latitude: 21.5852, longitude: 105.8073, sortBy: "DistanceAsc", pageNumber: 2, pageSize: 1);
        var (page2Items, totalCount2) = await repo.SearchAsync(queryPage2);

        var queryPage3 = new SearchRoomsQuery(latitude: 21.5852, longitude: 105.8073, sortBy: "DistanceAsc", pageNumber: 3, pageSize: 1);
        var (page3Items, totalCount3) = await repo.SearchAsync(queryPage3);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(totalCount1, Is.EqualTo(3));
            Assert.That(page1Items[0].Id, Is.EqualTo("room-near"));
            Assert.That(page2Items[0].Id, Is.EqualTo("room-mid"));
            Assert.That(page3Items[0].Id, Is.EqualTo("room-far"));
        });
    }

    [Test]
    public async Task SearchAsync_WithNewestSort_SortsAcrossMultiplePagesBeforePagination_BasedOnCreatedAt()
    {
        // Arrange
        var repo = new InMemoryRoomRepository();
        var baseDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        // 4 rooms with different CreatedAt and LastUpdatedDate
        // room1: Created Nov 10, Updated Nov 10
        repo.Upsert(new Room { Id = "room-3rd", Title = "3rd Newest", Price = 2_000_000m, CreatedAt = baseDate.AddDays(10), LastUpdatedDate = baseDate.AddDays(10), IsVerified = true, Status = RoomStatus.Available });
        // room2: Created Nov 20, Updated Nov 20 -> Newest!
        repo.Upsert(new Room { Id = "room-1st", Title = "1st Newest", Price = 2_000_000m, CreatedAt = baseDate.AddDays(20), LastUpdatedDate = baseDate.AddDays(20), IsVerified = true, Status = RoomStatus.Available });
        // room3: Created Nov 5, Updated Nov 25 (Updated later, but created earlier -> must be 4th!)
        repo.Upsert(new Room { Id = "room-4th", Title = "4th Newest (Updated Late)", Price = 2_000_000m, CreatedAt = baseDate.AddDays(5), LastUpdatedDate = baseDate.AddDays(25), IsVerified = true, Status = RoomStatus.Available });
        // room4: Created Nov 15, Updated Nov 15 -> 2nd!
        repo.Upsert(new Room { Id = "room-2nd", Title = "2nd Newest", Price = 2_000_000m, CreatedAt = baseDate.AddDays(15), LastUpdatedDate = baseDate.AddDays(15), IsVerified = true, Status = RoomStatus.Available });

        // Act: Page 1 (size 2) and Page 2 (size 2)
        var page1Query = new SearchRoomsQuery(sortBy: "Newest", pageNumber: 1, pageSize: 2);
        var (page1Items, totalCount1) = await repo.SearchAsync(page1Query);

        var page2Query = new SearchRoomsQuery(sortBy: "Newest", pageNumber: 2, pageSize: 2);
        var (page2Items, totalCount2) = await repo.SearchAsync(page2Query);

        // Assert: Page 1 has 1st and 2nd; Page 2 has 3rd and 4th
        Assert.Multiple(() =>
        {
            Assert.That(totalCount1, Is.EqualTo(4));
            Assert.That(totalCount2, Is.EqualTo(4));
            Assert.That(page1Items.Select(r => r.Id), Is.EqualTo(new[] { "room-1st", "room-2nd" }));
            Assert.That(page2Items.Select(r => r.Id), Is.EqualTo(new[] { "room-3rd", "room-4th" }));
        });
    }
}
