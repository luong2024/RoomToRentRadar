using System.Net;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Ports.Contracts.Requests.Profile;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadarUnitTests.ClientAdapters.Gateways;

/// <summary>T13.4 — <c>updateMyProfile</c> client gateway.</summary>
[TestFixture]
public class HttpProfileGatewayTests
{
    private static readonly UpdateMyProfileRequest CampusOnly = new(null, null, "TNUT", null);

    private static (HttpProfileGateway Gateway, StubHttpMessageHandler Stub, HttpClient Client) Create(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var stub = new StubHttpMessageHandler(responder);
        var client = new HttpClient(stub) { BaseAddress = new Uri("http://localhost") };
        return (new HttpProfileGateway(client), stub, client);
    }

    private static HttpResponseMessage Profile() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(
                new UserProfileDto(
                    "u1", "student@ictu.edu.vn", "Student", "Student", "Local", null, null, true,
                    new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), "TNUT", "en"),
                ApiResponseReader.JsonOptions),
            Encoding.UTF8,
            "application/json")
    };

    private static HttpResponseMessage Problem(HttpStatusCode status, string errorCode) => new(status)
    {
        Content = new StringContent(
            $$"""{"title":"Error","status":{{(int)status}},"errorCode":"{{errorCode}}"}""",
            Encoding.UTF8,
            "application/problem+json")
    };

    [Test]
    public async Task UpdateMyProfileAsync_SendsPatchToApiMeProfile_WithOnlyProvidedFieldsInJson()
    {
        var (gateway, stub, client) = Create(_ => Profile());
        using var _ = client;

        await gateway.UpdateMyProfileAsync(CampusOnly);

        var request = stub.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Patch));
            Assert.That(request.Path, Is.EqualTo("/api/me/profile"));
            Assert.That(request.BodyAsString, Is.EqualTo("""{"selectedCampusId":"TNUT"}"""));
            Assert.That(request.Authorization, Is.Null, "AuthTokenHandler attaches the token, not the gateway.");
        });
    }

    [Test]
    public async Task UpdateMyProfileAsync_Returns200Profile()
    {
        var (gateway, _, client) = Create(_ => Profile());
        using var __ = client;

        var result = await gateway.UpdateMyProfileAsync(CampusOnly);

        Assert.That(result.SelectedCampusId, Is.EqualTo("TNUT"));
    }

    [TestCase(HttpStatusCode.BadRequest, "CAMPUS_NOT_FOUND")]
    [TestCase(HttpStatusCode.Unauthorized, "UNAUTHORIZED")]
    public void UpdateMyProfileAsync_ErrorStatus_ThrowsApiExceptionWithErrorCode(HttpStatusCode status, string code)
    {
        var (gateway, _, client) = Create(_ => Problem(status, code));
        using var __ = client;

        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.UpdateMyProfileAsync(CampusOnly));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(status));
            Assert.That(ex.ErrorCode, Is.EqualTo(code));
        });
    }

    [Test]
    public void UpdateMyProfileAsync_NullRequest_Throws()
    {
        var (gateway, _, client) = Create(_ => Profile());
        using var __ = client;

        Assert.ThrowsAsync<ArgumentNullException>(async () => await gateway.UpdateMyProfileAsync(null!));
    }
}
