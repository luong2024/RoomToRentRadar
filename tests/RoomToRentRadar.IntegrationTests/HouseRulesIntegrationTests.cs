using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.IntegrationTests;

public class HouseRulesIntegrationTests
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
    public async Task HouseRules_Lifecycle_WithRbacVersioningAndConcurrency_ShouldBehaveCorrectly()
    {
        var ownerAuth = await RegisterAndLoginAsync("landlord.owner@ictu.edu.vn", "LANDLORD");
        var otherLandlordAuth = await RegisterAndLoginAsync("landlord.other@ictu.edu.vn", "LANDLORD");
        var studentAuth = await RegisterAndLoginAsync("student.viewer@ictu.edu.vn", "STUDENT");

        var room = new Room
        {
            Id = "room-hr-01",
            Title = "Phòng gần ICTU",
            Price = 2200000m,
            Area = 22,
            Address = "Đường Z115",
            LandlordId = ownerAuth.UserId,
            LandlordPhone = "0988000111",
            Status = RoomStatus.Available,
            IsVerified = true,
            HouseRules = ["Legacy rule 1"]
        };
        _factory.RoomRepository.Upsert(room);

        // 1. Anonymous GET returns legacy fallback
        var getInitialResponse = await _client.GetAsync($"/api/v1/rooms/{room.Id}/house-rules");
        Assert.That(getInitialResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var initialDto = await getInitialResponse.Content.ReadFromJsonAsync<RoomHouseRulesDto>();
        Assert.That(initialDto, Is.Not.Null);
        Assert.That(initialDto!.IsLegacyFallback, Is.True);

        var updatePayloadV1 = new UpdateHouseRulesRequest(
            QuietHours: "22:00 - 06:00",
            GuestPolicy: "Guests allowed until 21:30",
            CookingRules: "Electric cooker allowed",
            ParkingRules: "Free 1 motorbike",
            AdditionalRules: ["No pets"],
            ChangeNote: "Initial structured rules");

        // 2. Anonymous PUT -> 401
        var anonPut = await _client.PutAsJsonAsync($"/api/v1/rooms/{room.Id}/house-rules", updatePayloadV1);
        Assert.That(anonPut.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // 3. Student PUT -> 403
        var studentPut = await SendAuthorizedPutAsync($"/api/v1/rooms/{room.Id}/house-rules", studentAuth.AccessToken, updatePayloadV1);
        Assert.That(studentPut.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        // 4. Other Landlord PUT -> 403
        var otherLandlordPut = await SendAuthorizedPutAsync($"/api/v1/rooms/{room.Id}/house-rules", otherLandlordAuth.AccessToken, updatePayloadV1);
        Assert.That(otherLandlordPut.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        // 5. Owner Landlord PUT -> 200 (v1)
        var ownerPutV1 = await SendAuthorizedPutAsync($"/api/v1/rooms/{room.Id}/house-rules", ownerAuth.AccessToken, updatePayloadV1);
        Assert.That(ownerPutV1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var v1Dto = await ownerPutV1.Content.ReadFromJsonAsync<RoomHouseRulesDto>();
        Assert.Multiple(() =>
        {
            Assert.That(v1Dto!.Version, Is.EqualTo(1));
            Assert.That(v1Dto.LandlordId, Is.EqualTo(ownerAuth.UserId));
        });

        // 5b. No-op PUT with identical rules -> stays at v1
        var noOpPut = await SendAuthorizedPutAsync($"/api/v1/rooms/{room.Id}/house-rules", ownerAuth.AccessToken, updatePayloadV1 with { ExpectedVersion = 1 });
        Assert.That(noOpPut.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var noOpDto = await noOpPut.Content.ReadFromJsonAsync<RoomHouseRulesDto>();
        Assert.That(noOpDto!.Version, Is.EqualTo(1));

        // 6. Owner Landlord PUT -> 200 (v2)
        var updatePayloadV2 = new UpdateHouseRulesRequest(
            QuietHours: "23:00 - 06:00",
            GuestPolicy: "Guests allowed until 22:00",
            CookingRules: "Electric cooker allowed",
            ParkingRules: "Free 1 motorbike, 50k for 2nd",
            AdditionalRules: ["No pets", "Keep corridor clean"],
            ChangeNote: "Extended quiet hours",
            ExpectedVersion: 1);

        var ownerPutV2 = await SendAuthorizedPutAsync($"/api/v1/rooms/{room.Id}/house-rules", ownerAuth.AccessToken, updatePayloadV2);
        Assert.That(ownerPutV2.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var v2Dto = await ownerPutV2.Content.ReadFromJsonAsync<RoomHouseRulesDto>();
        Assert.That(v2Dto!.Version, Is.EqualTo(2));

        // 7. Stale ExpectedVersion -> 409 Conflict
        var stalePut = await SendAuthorizedPutAsync($"/api/v1/rooms/{room.Id}/house-rules", ownerAuth.AccessToken, updatePayloadV2);
        Assert.That(stalePut.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        // 8. History endpoint returns [v2, v1]
        var historyResponse = await _client.GetAsync($"/api/v1/rooms/{room.Id}/house-rules/versions");
        Assert.That(historyResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var history = await historyResponse.Content.ReadFromJsonAsync<List<HouseRulesVersionDto>>();
        Assert.That(history, Is.Not.Null);
        Assert.That(history!.Select(h => h.Version), Is.EqualTo(new[] { 2, 1 }));

        // 9. Room details includes StructuredHouseRules at v2
        var roomDetailResponse = await _client.GetAsync($"/api/rooms/{room.Id}");
        Assert.That(roomDetailResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var roomDetail = await roomDetailResponse.Content.ReadFromJsonAsync<RoomDetailsDto>();
        Assert.That(roomDetail?.StructuredHouseRules?.Version, Is.EqualTo(2));
    }

    private async Task<AuthSessionResponse> RegisterAndLoginAsync(string email, string role)
    {
        var reg = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterUserRequest(email, "StrongPassword123!", role));
        Assert.That(reg.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginWithEmailRequest(email, "StrongPassword123!"));
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        return (await login.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
    }

    private Task<HttpResponseMessage> SendAuthorizedPutAsync(string url, string token, UpdateHouseRulesRequest body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
