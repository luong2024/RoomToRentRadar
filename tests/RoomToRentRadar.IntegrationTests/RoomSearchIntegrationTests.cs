using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.IntegrationTests;

public class RoomSearchIntegrationTests
{
    private AuthWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Environment.SetEnvironmentVariable("CosmosDb__AccountEndpoint", "in-memory");
        Environment.SetEnvironmentVariable("CosmosDb__DatabaseName", "IntegrationTestDb");
    }

    [SetUp]
    public void SetUp()
    {
        Environment.SetEnvironmentVariable("CosmosDb__AccountEndpoint", "in-memory");
        Environment.SetEnvironmentVariable("CosmosDb__DatabaseName", "IntegrationTestDb");

        _factory = new AuthWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task SearchRooms_AllowAnonymous_ShouldReturnOkWithDefaultPagination()
    {
        // Act - Call GET /api/rooms anonymously without Authorization header
        var response = await _client.GetAsync("/api/rooms");

        // Assert - Endpoint must allow anonymous access and return 200 OK
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<SearchRoomsResult>();
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.CurrentPage, Is.EqualTo(1));
            Assert.That(result.ActiveFilters, Is.Empty);
        });
    }

    [Test]
    public async Task SearchRooms_AC4_MustNeverReturnUnverifiedOrUnavailableRooms()
    {
        // Arrange - Seed rooms with different availability and verification statuses
        var verifiedAvailable = new Room
        {
            Id = "room-ac4-01",
            Title = "Phòng khả dụng và đã xác thực",
            Price = 2000000m,
            Area = 25,
            Address = "123 Quang Trung, Thái Nguyên",
            Status = RoomStatus.Available,
            IsVerified = true,
            RoomType = "Khép kín"
        };
        var unverifiedAvailable = new Room
        {
            Id = "room-ac4-02",
            Title = "Phòng chưa xác thực",
            Price = 2000000m,
            Area = 25,
            Address = "124 Quang Trung, Thái Nguyên",
            Status = RoomStatus.Available,
            IsVerified = false,
            RoomType = "Khép kín"
        };
        var verifiedRented = new Room
        {
            Id = "room-ac4-03",
            Title = "Phòng đã cho thuê",
            Price = 2000000m,
            Area = 25,
            Address = "125 Quang Trung, Thái Nguyên",
            Status = RoomStatus.Rented,
            IsVerified = true,
            RoomType = "Khép kín"
        };
        var verifiedPending = new Room
        {
            Id = "room-ac4-04",
            Title = "Phòng đang chờ duyệt",
            Price = 2000000m,
            Area = 25,
            Address = "126 Quang Trung, Thái Nguyên",
            Status = RoomStatus.Pending,
            IsVerified = true,
            RoomType = "Khép kín"
        };

        _factory.RoomRepository.Upsert(verifiedAvailable);
        _factory.RoomRepository.Upsert(unverifiedAvailable);
        _factory.RoomRepository.Upsert(verifiedRented);
        _factory.RoomRepository.Upsert(verifiedPending);

        // Act
        var response = await _client.GetAsync("/api/rooms");

        // Assert - Only verified & available room is returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<SearchRoomsResult>();
        Assert.That(result, Is.Not.Null);

        var matchingRoomIds = result!.Items.Select(i => i.Id).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(matchingRoomIds, Does.Contain(verifiedAvailable.Id));
            Assert.That(matchingRoomIds, Does.Not.Contain(unverifiedAvailable.Id));
            Assert.That(matchingRoomIds, Does.Not.Contain(verifiedRented.Id));
            Assert.That(matchingRoomIds, Does.Not.Contain(verifiedPending.Id));
        });
    }

    [Test]
    public async Task SearchRooms_FilterByPriceAndArea_ShouldReturnMatchingRooms()
    {
        // Arrange
        var match = new Room
        {
            Id = "room-filter-match",
            Title = "Phòng đạt chuẩn lọc",
            Price = 2500000m,
            Area = 25,
            Address = "Đường Z115, Thái Nguyên",
            Status = RoomStatus.Available,
            IsVerified = true,
            RoomType = "Khép kín"
        };
        var cheap = new Room
        {
            Id = "room-filter-cheap",
            Title = "Phòng quá rẻ ngoài khoảng",
            Price = 1000000m,
            Area = 25,
            Address = "Đường Z115, Thái Nguyên",
            Status = RoomStatus.Available,
            IsVerified = true,
            RoomType = "Khép kín"
        };
        var large = new Room
        {
            Id = "room-filter-large",
            Title = "Phòng quá rộng ngoài khoảng",
            Price = 2500000m,
            Area = 50,
            Address = "Đường Z115, Thái Nguyên",
            Status = RoomStatus.Available,
            IsVerified = true,
            RoomType = "Khép kín"
        };

        _factory.RoomRepository.Upsert(match);
        _factory.RoomRepository.Upsert(cheap);
        _factory.RoomRepository.Upsert(large);

        // Act
        var url = "/api/rooms?minPrice=2000000&maxPrice=3000000&minArea=20&maxArea=30";
        var response = await _client.GetAsync(url);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<SearchRoomsResult>();
        Assert.That(result, Is.Not.Null);

        var roomIds = result!.Items.Select(r => r.Id).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(roomIds, Does.Contain(match.Id));
            Assert.That(roomIds, Does.Not.Contain(cheap.Id));
            Assert.That(roomIds, Does.Not.Contain(large.Id));
            Assert.That(result.ActiveFilters, Does.Contain("MinPrice"));
            Assert.That(result.ActiveFilters, Does.Contain("MaxPrice"));
            Assert.That(result.ActiveFilters, Does.Contain("MinArea"));
            Assert.That(result.ActiveFilters, Does.Contain("MaxArea"));
        });
    }

    [Test]
    public async Task SearchRooms_FilterByRoomTypeAndKeyword_ShouldReturnMatchingRooms()
    {
        // Arrange
        var studio = new Room
        {
            Id = "room-studio-01",
            Title = "Căn hộ Studio mini thoáng mát",
            Description = "Nội thất cao cấp, gần trường đại học",
            Price = 3000000m,
            Area = 28,
            Address = "Đường Lương Ngọc Quyến, Thái Nguyên",
            Status = RoomStatus.Available,
            IsVerified = true,
            RoomType = "Studio"
        };
        var dorm = new Room
        {
            Id = "room-dorm-01",
            Title = "Phòng ký túc xá giá rẻ",
            Description = "Giường tầng sinh viên",
            Price = 800000m,
            Area = 35,
            Address = "Đường Tân Thịnh, Thái Nguyên",
            Status = RoomStatus.Available,
            IsVerified = true,
            RoomType = "KTX"
        };

        _factory.RoomRepository.Upsert(studio);
        _factory.RoomRepository.Upsert(dorm);

        // Act
        var url = "/api/rooms?roomType=Studio&keyword=mini";
        var response = await _client.GetAsync(url);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<SearchRoomsResult>();
        Assert.That(result, Is.Not.Null);

        var roomIds = result!.Items.Select(r => r.Id).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(roomIds, Does.Contain(studio.Id));
            Assert.That(roomIds, Does.Not.Contain(dorm.Id));
            Assert.That(result.ActiveFilters, Does.Contain("RoomType"));
            Assert.That(result.ActiveFilters, Does.Contain("Keyword"));
        });
    }

    [Test]
    public async Task SearchRooms_FilterByDistance_ShouldReturnRoomsWithinRadiusAndCalculateDistanceInKm()
    {
        // User coords: lat=21.5852, lon=105.8073
        // Room Near: lat=21.5890, lon=105.8100 (~0.5 km away)
        var nearRoom = new Room
        {
            Id = "room-near",
            Title = "Phòng trọ gần",
            Price = 2000000m,
            Area = 22,
            Address = "Gần trường",
            Latitude = 21.5890,
            Longitude = 105.8100,
            Status = RoomStatus.Available,
            IsVerified = true,
            RoomType = "Khép kín"
        };

        // Room Far: lat=21.6800, lon=105.8800 (~12 km away)
        var farRoom = new Room
        {
            Id = "room-far",
            Title = "Phòng trọ xa",
            Price = 2000000m,
            Area = 22,
            Address = "Xa thành phố",
            Latitude = 21.6800,
            Longitude = 105.8800,
            Status = RoomStatus.Available,
            IsVerified = true,
            RoomType = "Khép kín"
        };

        _factory.RoomRepository.Upsert(nearRoom);
        _factory.RoomRepository.Upsert(farRoom);

        // Act - Request with radius 2.0 km
        var url = "/api/rooms?latitude=21.5852&longitude=105.8073&radiusInKm=2.0";
        var response = await _client.GetAsync(url);

        // Assert - Only near room is returned, distance calculated
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<SearchRoomsResult>();
        Assert.That(result, Is.Not.Null);

        var roomIds = result!.Items.Select(r => r.Id).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(roomIds, Does.Contain(nearRoom.Id));
            Assert.That(roomIds, Does.Not.Contain(farRoom.Id));
            var returnedNear = result.Items.First(r => r.Id == nearRoom.Id);
            Assert.That(returnedNear.DistanceInKm, Is.Not.Null);
            Assert.That(returnedNear.DistanceInKm!.Value, Is.GreaterThan(0.3).And.LessThan(1.0));
            Assert.That(result.ActiveFilters, Does.Contain("Latitude"));
            Assert.That(result.ActiveFilters, Does.Contain("Longitude"));
            Assert.That(result.ActiveFilters, Does.Contain("RadiusInKm"));
        });
    }

    [Test]
    public async Task SearchRooms_Pagination_ShouldReturnCorrectPageAndTotalCount()
    {
        // Seed 7 available verified rooms
        for (int i = 1; i <= 7; i++)
        {
            var r = new Room
            {
                Id = $"room-page-{i:D2}",
                Title = $"Phòng trang {i}",
                Price = 2000000m,
                Area = 20,
                Status = RoomStatus.Available,
                IsVerified = true,
                RoomType = "Khép kín"
            };
            _factory.RoomRepository.Upsert(r);
        }

        // Act - Request page 2, pageSize 3
        var response = await _client.GetAsync("/api/rooms?pageNumber=2&pageSize=3");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<SearchRoomsResult>();
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.TotalCount, Is.GreaterThanOrEqualTo(7));
            Assert.That(result.CurrentPage, Is.EqualTo(2));
            Assert.That(result.Items.Count, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task SearchRooms_WhenMinPriceGreaterThanMaxPrice_ShouldReturnBadRequest()
    {
        // Act - Pass invalid query where minPrice > maxPrice
        var response = await _client.GetAsync("/api/rooms?minPrice=5000000&maxPrice=2000000");

        // Assert - Must return 400 Bad Request
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Detail, Does.Contain("MinPrice"));
    }

    [Test]
    public async Task SearchRooms_WhenPageSizeExceedsMaximumCap_ShouldReturnBadRequest()
    {
        // Act - Attacker attempts to request massive PageSize (e.g., 1,000,000) to exhaust DB RUs
        var response = await _client.GetAsync("/api/rooms?pageSize=1000000");

        // Assert - Must be rejected immediately with 400 Bad Request before querying Cosmos DB
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Detail, Does.Contain("PageSize"));
    }

    [Test]
    public async Task SearchRooms_WhenPageNumberLessThanOne_ShouldReturnBadRequest()
    {
        // Act - Invalid PageNumber <= 0
        var response = await _client.GetAsync("/api/rooms?pageNumber=0");

        // Assert - Must return 400 Bad Request
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Detail, Does.Contain("PageNumber"));
    }

    [Test]
    public async Task SearchRooms_WhenPageNumberExceedsMaximumCap_ShouldReturnBadRequest()
    {
        // Act - Invalid PageNumber > 100,000 (prevents integer overflow and DoS)
        var response = await _client.GetAsync("/api/rooms?pageNumber=200000");

        // Assert - Must return 400 Bad Request
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Detail, Does.Contain("PageNumber"));
    }
}
