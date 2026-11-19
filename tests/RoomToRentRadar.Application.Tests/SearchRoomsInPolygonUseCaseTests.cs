namespace RoomToRentRadar.Application.Tests;

using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.RoomListings;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using ListingRoomStatus = RoomToRentRadar.Domain.Enums.RoomStatus;

[TestFixture]
public class SearchRoomsInPolygonUseCaseTests
{
    private FakeSpatialRoomListingRepository _repository = null!;
    private SearchRoomsInPolygonUseCase _useCase = null!;

    // Valid square polygon coordinates near ICTU campus (~10,000 m²)
    private static readonly List<CoordinateDto> ValidCoordinates =
    [
        new(21.5850, 105.8070),
        new(21.5850, 105.8080),
        new(21.5860, 105.8080),
        new(21.5860, 105.8070)
    ];

    [SetUp]
    public void SetUp()
    {
        _repository = new FakeSpatialRoomListingRepository();
        _useCase = new SearchRoomsInPolygonUseCase(_repository);
    }

    [Test]
    public async Task ExecuteAsync_WithValidRequest_ReturnsPagedResult()
    {
        var sampleListing = new RoomListing(
            id: "room-101",
            title: "Cozy Studio near ICTU",
            description: "Fully furnished room",
            monthlyRent: new Money(2_500_000m, "VND"),
            areaSquareMeters: 25.0,
            address: "Z115 Street, Thai Nguyen",
            location: new GeoLocation(21.5855, 105.8075),
            status: ListingRoomStatus.Available,
            verificationStatus: VerificationStatus.Verified,
            campusId: "campus-ictu",
            thumbnailUrl: "https://example.com/room.jpg");

        _repository.SeedItems([sampleListing]);

        var request = new SpatialRoomSearchRequestDto
        {
            PolygonCoordinates = ValidCoordinates,
            MinPrice = 1_000_000m,
            MaxPrice = 5_000_000m,
            PageIndex = 1,
            PageSize = 10
        };

        var result = await _useCase.ExecuteAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].Id, Is.EqualTo("room-101"));
            Assert.That(result.Items[0].Latitude, Is.EqualTo(21.5855));
            Assert.That(result.Items[0].Longitude, Is.EqualTo(105.8075));
            Assert.That(result.Items[0].MonthlyRent, Is.EqualTo(2_500_000m));
            Assert.That(result.Items[0].IsVerified, Is.True);
        });
    }

    [Test]
    public void ExecuteAsync_WithNullRequest_ThrowsValidationException()
    {
        Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(null!));
    }

    [Test]
    public void ExecuteAsync_WithLessThanThreeCoordinates_ThrowsValidationException()
    {
        var request = new SpatialRoomSearchRequestDto
        {
            PolygonCoordinates = [new(21.5850, 105.8070), new(21.5850, 105.8080)],
            PageIndex = 1,
            PageSize = 10
        };

        var ex = Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request));
        Assert.That(ex!.Errors.ContainsKey(nameof(request.PolygonCoordinates)), Is.True);
    }

    [Test]
    public void ExecuteAsync_WithMinPriceGreaterThanMaxPrice_ThrowsValidationException()
    {
        var request = new SpatialRoomSearchRequestDto
        {
            PolygonCoordinates = ValidCoordinates,
            MinPrice = 5_000_000m,
            MaxPrice = 2_000_000m,
            PageIndex = 1,
            PageSize = 10
        };

        var ex = Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request));
        Assert.That(ex!.Errors.ContainsKey(nameof(request.MinPrice)), Is.True);
    }

    [TestCase(-1)]
    [TestCase(0)]
    public void ExecuteAsync_WithInvalidPageIndex_ThrowsValidationException(int pageIndex)
    {
        var request = new SpatialRoomSearchRequestDto
        {
            PolygonCoordinates = ValidCoordinates,
            PageIndex = pageIndex,
            PageSize = 10
        };

        var ex = Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request));
        Assert.That(ex!.Errors.ContainsKey(nameof(request.PageIndex)), Is.True);
    }

    [TestCase(0)]
    [TestCase(51)]
    [TestCase(100)]
    public void ExecuteAsync_WithInvalidPageSize_ThrowsValidationException(int pageSize)
    {
        var request = new SpatialRoomSearchRequestDto
        {
            PolygonCoordinates = ValidCoordinates,
            PageIndex = 1,
            PageSize = pageSize
        };

        var ex = Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request));
        Assert.That(ex!.Errors.ContainsKey(nameof(request.PageSize)), Is.True);
    }

    [Test]
    public void ExecuteAsync_WithSubThresholdArea_ThrowsValidationException()
    {
        // 10m x 10m triangle (< 400 m² threshold)
        var tinyTriangle = new List<CoordinateDto>
        {
            new(21.5850, 105.8070),
            new(21.5851, 105.8070),
            new(21.5850, 105.8071)
        };

        var request = new SpatialRoomSearchRequestDto
        {
            PolygonCoordinates = tinyTriangle,
            PageIndex = 1,
            PageSize = 10
        };

        var ex = Assert.ThrowsAsync<ValidationException>(() => _useCase.ExecuteAsync(request));
        Assert.That(ex!.Errors.ContainsKey(nameof(request.PolygonCoordinates)), Is.True);
    }

    private sealed class FakeSpatialRoomListingRepository : IRoomListingRepository
    {
        private readonly List<RoomListing> _items = [];

        public void SeedItems(IEnumerable<RoomListing> items) => _items.AddRange(items);

        public Task<PagedResult<RoomListing>> GetAvailableVerifiedRoomsByCampusAsync(
            string campusId,
            GeoLocation campusLocation,
            int page,
            int pageSize,
            string sortBy,
            bool descending,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PagedResult<RoomListing>([], page, pageSize, 0));
        }

        public Task AddAsync(RoomListing roomListing, CancellationToken cancellationToken = default)
        {
            _items.Add(roomListing);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string roomId, string campusId, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(i => i.Id == roomId && i.CampusId == campusId);
            return Task.CompletedTask;
        }

        public Task<PagedResult<RoomListing>> SearchByPolygonAsync(
            GeoPolygon polygon,
            RoomSearchFilter filter,
            int pageIndex,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var filtered = _items
                .Where(r => r.Status == ListingRoomStatus.Available && r.VerificationStatus == VerificationStatus.Verified)
                .ToList();

            if (filter.MinPrice.HasValue)
            {
                filtered = filtered.Where(r => r.MonthlyRent.Amount >= filter.MinPrice.Value).ToList();
            }

            if (filter.MaxPrice.HasValue)
            {
                filtered = filtered.Where(r => r.MonthlyRent.Amount <= filter.MaxPrice.Value).ToList();
            }

            var paged = filtered.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedResult<RoomListing>(paged, pageIndex, pageSize, filtered.Count));
        }
    }
}
