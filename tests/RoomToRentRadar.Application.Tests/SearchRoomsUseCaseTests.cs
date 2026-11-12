using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.Tests;

public class SearchRoomsUseCaseTests
{
    private FakeRoomRepository _roomRepository = null!;
    private SearchRoomsUseCase _useCase = null!;

    [SetUp]
    public void SetUp()
    {
        _roomRepository = new FakeRoomRepository();
        _useCase = new SearchRoomsUseCase(_roomRepository);
    }

    [Test]
    public void ExecuteAsync_WhenMinPriceGreaterThanMaxPrice_ShouldThrowArgumentException()
    {
        var query = new SearchRoomsQuery(minPrice: 3000000m, maxPrice: 1000000m);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _useCase.ExecuteAsync(query));
    }

    [Test]
    public void ExecuteAsync_WhenMinAreaGreaterThanMaxArea_ShouldThrowArgumentException()
    {
        var query = new SearchRoomsQuery(minArea: 40, maxArea: 20);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _useCase.ExecuteAsync(query));
    }

    [Test]
    [TestCase(-100, null, null, null)]
    [TestCase(null, -100, null, null)]
    [TestCase(null, null, -10, null)]
    [TestCase(null, null, null, -10)]
    public void ExecuteAsync_WhenNegativeValues_ShouldThrowArgumentException(
        decimal? minPrice, decimal? maxPrice, double? minArea, double? maxArea)
    {
        var query = new SearchRoomsQuery(
            minPrice: minPrice,
            maxPrice: maxPrice,
            minArea: minArea,
            maxArea: maxArea);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _useCase.ExecuteAsync(query));
    }

    [Test]
    public void ExecuteAsync_WhenRadiusProvidedWithoutCoordinates_ShouldThrowArgumentException()
    {
        var query = new SearchRoomsQuery(radiusInKm: 5.0);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _useCase.ExecuteAsync(query));
    }

    [Test]
    [TestCase(95.0, 105.0)]
    [TestCase(-95.0, 105.0)]
    [TestCase(21.0, 190.0)]
    [TestCase(21.0, -190.0)]
    public void ExecuteAsync_WhenInvalidCoordinates_ShouldThrowArgumentException(double lat, double lon)
    {
        var query = new SearchRoomsQuery(latitude: lat, longitude: lon);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _useCase.ExecuteAsync(query));
    }

    [Test]
    [TestCase(0)]
    [TestCase(-5)]
    public void ExecuteAsync_WhenRadiusZeroOrNegative_ShouldThrowArgumentException(double radius)
    {
        var query = new SearchRoomsQuery(latitude: 21.58, longitude: 105.81, radiusInKm: radius);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _useCase.ExecuteAsync(query));
    }

    [Test]
    [TestCase(0)]
    [TestCase(-1)]
    public void ExecuteAsync_WhenPageNumberLessThanOne_ShouldThrowArgumentException(int pageNumber)
    {
        var query = new SearchRoomsQuery(pageNumber: pageNumber);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _useCase.ExecuteAsync(query));
    }

    [Test]
    [TestCase(100_001)]
    [TestCase(int.MaxValue)]
    public void ExecuteAsync_WhenPageNumberExceedsMax_ShouldThrowArgumentException(int pageNumber)
    {
        var query = new SearchRoomsQuery(pageNumber: pageNumber);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _useCase.ExecuteAsync(query));
    }

    [Test]
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(51)]
    [TestCase(1000000)]
    public void ExecuteAsync_WhenPageSizeOutOfRange_ShouldThrowArgumentException(int pageSize)
    {
        var query = new SearchRoomsQuery(pageSize: pageSize);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _useCase.ExecuteAsync(query));
    }

    [Test]
    public async Task ExecuteAsync_AC4_MustFilterOutUnverifiedRooms()
    {
        // Seed 1 verified and 1 unverified room
        var verifiedRoom = CreateValidRoom("r1", 2000000m, 25, isVerified: true, status: RoomStatus.Available);
        var unverifiedRoom = CreateValidRoom("r2", 2000000m, 25, isVerified: false, status: RoomStatus.Available);
        _roomRepository.Add(verifiedRoom);
        _roomRepository.Add(unverifiedRoom);

        var result = await _useCase.ExecuteAsync(new SearchRoomsQuery());

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Id, Is.EqualTo("r1"));
        Assert.That(result.Items[0].IsVerified, Is.True);
    }

    [Test]
    public async Task ExecuteAsync_AC4_MustFilterOutUnavailableRooms()
    {
        // Seed available, rented, pending, under maintenance rooms
        var availableRoom = CreateValidRoom("r-avail", 2000000m, 25, isVerified: true, status: RoomStatus.Available);
        var rentedRoom = CreateValidRoom("r-rented", 2000000m, 25, isVerified: true, status: RoomStatus.Rented);
        var pendingRoom = CreateValidRoom("r-pending", 2000000m, 25, isVerified: true, status: RoomStatus.Pending);
        var maintRoom = CreateValidRoom("r-maint", 2000000m, 25, isVerified: true, status: RoomStatus.UnderMaintenance);

        _roomRepository.Add(availableRoom);
        _roomRepository.Add(rentedRoom);
        _roomRepository.Add(pendingRoom);
        _roomRepository.Add(maintRoom);

        var result = await _useCase.ExecuteAsync(new SearchRoomsQuery());

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Id, Is.EqualTo("r-avail"));
    }

    [Test]
    public async Task ExecuteAsync_WhenPriceAndAreaFiltersApplied_ShouldFilterAccurately()
    {
        var match = CreateValidRoom("r-match", 2500000m, 25, isVerified: true, status: RoomStatus.Available);
        var cheap = CreateValidRoom("r-cheap", 1500000m, 25, isVerified: true, status: RoomStatus.Available);
        var expensive = CreateValidRoom("r-exp", 4000000m, 25, isVerified: true, status: RoomStatus.Available);
        var small = CreateValidRoom("r-small", 2500000m, 15, isVerified: true, status: RoomStatus.Available);

        _roomRepository.Add(match);
        _roomRepository.Add(cheap);
        _roomRepository.Add(expensive);
        _roomRepository.Add(small);

        var query = new SearchRoomsQuery(
            minPrice: 2000000m,
            maxPrice: 3000000m,
            minArea: 20,
            maxArea: 30);

        var result = await _useCase.ExecuteAsync(query);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Id, Is.EqualTo("r-match"));
        Assert.That(result.ActiveFilters, Does.Contain("MinPrice"));
        Assert.That(result.ActiveFilters, Does.Contain("MaxPrice"));
        Assert.That(result.ActiveFilters, Does.Contain("MinArea"));
        Assert.That(result.ActiveFilters, Does.Contain("MaxArea"));
    }

    [Test]
    public async Task ExecuteAsync_WhenUserCoordinatesProvided_ShouldCalculateHaversineDistanceWithoutHardcodingLocation()
    {
        // Thai Nguyen University of Information and Communication Technology (ICTU) coords ~ 21.5852, 105.8073
        // A room placed ~ 1 km away
        var room = CreateValidRoom("r-geo", 2000000m, 25, isVerified: true, status: RoomStatus.Available);
        room.Latitude = 21.5900;
        room.Longitude = 105.8100;
        _roomRepository.Add(room);

        var query = new SearchRoomsQuery(latitude: 21.5852, longitude: 105.8073);
        var result = await _useCase.ExecuteAsync(query);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].DistanceInKm, Is.Not.Null);
        Assert.That(result.Items[0].DistanceInKm!.Value, Is.GreaterThan(0.4).And.LessThan(1.0));
    }

    [Test]
    public async Task ExecuteAsync_WhenNoUserCoordinatesProvided_DistanceInKmShouldBeNull()
    {
        var room = CreateValidRoom("r-geo", 2000000m, 25, isVerified: true, status: RoomStatus.Available);
        room.Latitude = 21.5900;
        room.Longitude = 105.8100;
        _roomRepository.Add(room);

        var query = new SearchRoomsQuery();
        var result = await _useCase.ExecuteAsync(query);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].DistanceInKm, Is.Null);
    }

    [Test]
    public async Task ExecuteAsync_Pagination_ShouldComputeTotalPagesCorrectly()
    {
        for (int i = 1; i <= 15; i++)
        {
            _roomRepository.Add(CreateValidRoom($"r-{i}", 2000000m, 20, isVerified: true, status: RoomStatus.Available));
        }

        var query = new SearchRoomsQuery(pageNumber: 2, pageSize: 5);
        var result = await _useCase.ExecuteAsync(query);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(15));
            Assert.That(result.TotalPages, Is.EqualTo(3));
            Assert.That(result.CurrentPage, Is.EqualTo(2));
            Assert.That(result.Items.Count, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task ExecuteAsync_ActiveFilters_ShouldOnlyContainNonNullFilters()
    {
        var query = new SearchRoomsQuery(
            minPrice: 1000000m,
            keyword: "Studio",
            roomType: "Khép kín");

        var result = await _useCase.ExecuteAsync(query);

        Assert.Multiple(() =>
        {
            Assert.That(result.ActiveFilters, Does.Contain("MinPrice"));
            Assert.That(result.ActiveFilters, Does.Contain("Keyword"));
            Assert.That(result.ActiveFilters, Does.Contain("RoomType"));
            Assert.That(result.ActiveFilters, Does.Not.Contain("MaxPrice"));
            Assert.That(result.ActiveFilters, Does.Not.Contain("MinArea"));
            Assert.That(result.ActiveFilters, Does.Not.Contain("MaxArea"));
            Assert.That(result.ActiveFilters, Does.Not.Contain("Latitude"));
            Assert.That(result.ActiveFilters, Does.Not.Contain("Longitude"));
            Assert.That(result.ActiveFilters, Does.Not.Contain("RadiusInKm"));
        });
    }

    private static Room CreateValidRoom(
        string id,
        decimal price,
        double area,
        bool isVerified,
        RoomStatus status,
        string roomType = "Khép kín")
    {
        return new Room
        {
            Id = id,
            Title = $"Phòng {id}",
            Description = $"Mô tả phòng {id}",
            Price = price,
            Area = area,
            Address = $"Địa chỉ {id}",
            IsVerified = isVerified,
            Status = status,
            RoomType = roomType
        };
    }

    private sealed class FakeRoomRepository : IRoomRepository
    {
        private readonly List<Room> _rooms = [];

        public void Add(Room room) => _rooms.Add(room);

        public Task<Room?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_rooms.FirstOrDefault(r => r.Id == id));
        }

        public Task<IReadOnlyList<Room>> GetSimilarRoomsAsync(string roomId, decimal price, double area, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Room>>(_rooms);
        }

        public Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchAsync(
            SearchRoomsQuery query,
            CancellationToken ct = default)
        {
            var filtered = _rooms.AsEnumerable();

            if (query.MinPrice.HasValue)
                filtered = filtered.Where(r => r.Price >= query.MinPrice.Value);

            if (query.MaxPrice.HasValue)
                filtered = filtered.Where(r => r.Price <= query.MaxPrice.Value);

            if (query.MinArea.HasValue)
                filtered = filtered.Where(r => r.Area >= query.MinArea.Value);

            if (query.MaxArea.HasValue)
                filtered = filtered.Where(r => r.Area <= query.MaxArea.Value);

            if (!string.IsNullOrWhiteSpace(query.RoomType))
                filtered = filtered.Where(r => string.Equals(r.RoomType, query.RoomType, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(query.Keyword))
                filtered = filtered.Where(r => r.Title.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) ||
                                               r.Description.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) ||
                                               r.Address.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase));

            var list = filtered.ToList();
            var totalCount = list.Count;
            var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var size = query.PageSize <= 0 ? 10 : query.PageSize;
            var items = list.Skip((page - 1) * size).Take(size).ToList();

            return Task.FromResult<(IReadOnlyList<Room> Items, int TotalCount)>((items, totalCount));
        }
    }
}
