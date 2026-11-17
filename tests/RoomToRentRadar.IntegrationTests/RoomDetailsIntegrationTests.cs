using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Adapters.Outbound.Authentication;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.ValueObjects;
using RoomDetailsStatus = RoomToRentRadar.Domain.Entities.RoomStatus;

namespace RoomToRentRadar.IntegrationTests;

public class RoomDetailsIntegrationTests
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
    public async Task GetRoomDetails_InMemoryMode_AnonymousUser_ShouldMaskLandlordPhone()
    {
        // Arrange
        var room = new Room
        {
            Id = "room-avail-01",
            Title = "Phòng trọ tiện nghi gần trường ICTU",
            Description = "Đầy đủ điều hòa, nóng lạnh, ban công thoáng",
            Price = 2500000m,
            Area = 25,
            Address = "Số 123 Đường Z115, Thái Nguyên",
            LandlordPhone = "0987654321",
            Status = RoomDetailsStatus.Available,
            IsVerified = true,
            RoomType = "Khép kín"
        };
        _factory.RoomRepository.Upsert(room);

        // Act - Call GET /api/rooms/{id} directly in in-memory mode anonymously
        var response = await _client.GetAsync($"/api/rooms/{room.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<RoomDetailsDto>();
        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.Id, Is.EqualTo(room.Id));
            Assert.That(dto.IsAvailable, Is.True);
            Assert.That(dto.LandlordPhone, Is.EqualTo("*** (Yêu cầu xác thực để xem)"));
            Assert.That(dto.Address, Is.EqualTo(room.Address));
        });
    }

    [Test]
    public async Task GetRoomDetails_WhenUserIsAuthenticatedAndUnverified_ShouldMaskLandlordPhone()
    {
        // Arrange - Seed room
        var room = new Room
        {
            Id = "room-avail-02",
            Title = "Phòng trọ giá rẻ",
            Price = 1800000m,
            Area = 20,
            Address = "Ngõ 45 Quang Trung, Thái Nguyên",
            LandlordPhone = "0912345678",
            Status = RoomDetailsStatus.Available,
            IsVerified = true
        };
        _factory.RoomRepository.Upsert(room);

        // Register and login an unverified local student user
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(
            "unverified.student@ictu.edu.vn",
            "StrongPassword123!",
            "STUDENT"));
        Assert.That(registerResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(
            "unverified.student@ictu.edu.vn",
            "StrongPassword123!"));
        Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var authPayload = await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>();
        Assert.That(authPayload, Is.Not.Null);

        // Act - Call with JWT from unverified user
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/rooms/{room.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authPayload!.AccessToken);
        var response = await _client.SendAsync(request);

        // Assert - Phone is still masked
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<RoomDetailsDto>();
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.LandlordPhone, Is.EqualTo("*** (Yêu cầu xác thực để xem)"));
    }

    [Test]
    public async Task GetRoomDetails_WhenUserIsAuthenticatedAndVerified_ShouldReturnActualLandlordPhone()
    {
        // Arrange - Seed room
        var room = new Room
        {
            Id = "room-avail-03",
            Title = "Phòng cao cấp cho sinh viên",
            Price = 3000000m,
            Area = 30,
            Address = "Số 88 Tân Thịnh, Thái Nguyên",
            LandlordPhone = "0988776655",
            Status = RoomDetailsStatus.Available,
            IsVerified = true
        };
        _factory.RoomRepository.Upsert(room);

        // Create verified user and issue real JWT token
        var now = DateTimeOffset.UtcNow;
        var verifiedUser = User.RegisterLocalUser(
            EmailAddress.Create("verified.student@ictu.edu.vn"),
            "hashed-password",
            UserRole.Student,
            now);
        verifiedUser.MarkAsVerified(now);

        var userDoc = CosmosUnitOfWork.MapUserToDocument(verifiedUser);
        await _factory.CosmosDb.Users.CreateItemAsync(userDoc, userDoc.Id);

        var tokenService = _factory.Services.GetRequiredService<ITokenService>();
        var generatedToken = tokenService.GenerateAccessToken(verifiedUser, now);

        // Act - Call with real JWT bearing IsVerified = true
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/rooms/{room.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", generatedToken.Token);
        var response = await _client.SendAsync(request);

        // Assert - Actual phone number is revealed
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<RoomDetailsDto>();
        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.LandlordPhone, Is.EqualTo("0988776655"));
    }

    [Test]
    public async Task GetRoomDetails_WhenRoomIsUnavailable_ShouldReturnSimilarRoomsAndMaskSensitiveInfo()
    {
        // Arrange - Seed an unavailable room
        var unavailableRoom = new Room
        {
            Id = "room-rented-01",
            Title = "Phòng đã cho thuê",
            Price = 2000000m,
            Area = 22,
            Address = "Số 10 Ga Thái Nguyên",
            LandlordPhone = "0933221100",
            Status = RoomDetailsStatus.Rented,
            IsVerified = true
        };
        _factory.RoomRepository.Upsert(unavailableRoom);

        // Seed similar available rooms
        var similarRoom1 = new Room
        {
            Id = "room-similar-01",
            Title = "Phòng tương tự 1",
            Price = 2100000m,
            Area = 20,
            Address = "Số 12 Ga Thái Nguyên",
            Status = RoomDetailsStatus.Available,
            IsVerified = true
        };
        var similarRoom2 = new Room
        {
            Id = "room-similar-02",
            Title = "Phòng tương tự 2",
            Price = 1900000m,
            Area = 23,
            Address = "Số 14 Ga Thái Nguyên",
            Status = RoomDetailsStatus.Available,
            IsVerified = true
        };
        var similarRoomOutOfArea = new Room
        {
            Id = "room-similar-out-of-area",
            Title = "Phòng giá hợp lệ nhưng diện tích ngoài khoảng (quá lớn)",
            Price = 2000000m,
            Area = 40,
            Address = "Số 16 Ga Thái Nguyên",
            Status = RoomDetailsStatus.Available,
            IsVerified = true
        };
        _factory.RoomRepository.Upsert(similarRoom1);
        _factory.RoomRepository.Upsert(similarRoom2);
        _factory.RoomRepository.Upsert(similarRoomOutOfArea);

        // Act
        var response = await _client.GetAsync($"/api/rooms/{unavailableRoom.Id}");

        // Assert - AC3: IsAvailable = false, UnavailableMessage, sensitive info masked, SimilarRooms returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var dto = await response.Content.ReadFromJsonAsync<RoomDetailsDto>();
        Assert.That(dto, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(dto!.IsAvailable, Is.False);
            Assert.That(dto.UnavailableMessage, Is.EqualTo("Phòng này hiện không khả dụng."));
            Assert.That(dto.Address, Is.Empty);
            Assert.That(dto.LandlordPhone, Is.Empty);
            Assert.That(dto.SimilarRooms, Is.Not.Empty);
            Assert.That(dto.SimilarRooms.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(dto.SimilarRooms.Any(r => r.Id == "room-similar-01"), Is.True);
            Assert.That(dto.SimilarRooms.Any(r => r.Id == "room-similar-02"), Is.True);
            Assert.That(dto.SimilarRooms.Any(r => r.Id == "room-similar-out-of-area"), Is.False, "Phòng có diện tích nằm ngoài khoảng cho phép không được xuất hiện trong SimilarRooms");
        });
    }

    [Test]
    public async Task GetRoomDetails_WhenRoomDoesNotExist_ShouldReturn404NotFound()
    {
        var response = await _client.GetAsync("/api/rooms/non-existing-room-id");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
