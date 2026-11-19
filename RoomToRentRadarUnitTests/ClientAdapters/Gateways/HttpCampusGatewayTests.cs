using System.Net;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Datastore.Sqlite.Seed;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;

namespace RoomToRentRadarUnitTests.ClientAdapters.Gateways;

/// <summary>
/// T00.14 Phase 6 (AC-2, PLAN Q4): HttpCampusGateway is contract-first — covered with a stub handler only.
/// A real-API test is added after T00.8 (PR #59) merges and GET /api/campuses exists.
/// </summary>
[TestFixture]
public class HttpCampusGatewayTests
{
    [Test]
    public async Task ListCampusesAsync_ReturnsAllEightCampuses_IcTuDefault()
    {
        var sample = SampleCampuses();
        var (gateway, _) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, sample));

        var result = await gateway.ListCampusesAsync();

        Assert.That(result, Is.EqualTo(sample));
        Assert.That(result, Has.Count.EqualTo(8));
        Assert.That(result[0].Id, Is.EqualTo("ICTU"));
        Assert.That(result[0].IsDefault, Is.True);
        Assert.That(result.Count(c => c.IsDefault), Is.EqualTo(1));
    }

    [Test]
    public async Task ListCampusesAsync_EmptyArray_ReturnsEmptyList()
    {
        var (gateway, _) = CreateGateway(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        });

        var result = await gateway.ListCampusesAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ListCampusesAsync_404Problem_ThrowsApiExceptionNotFound()
    {
        var (gateway, _) = CreateGateway(_ => Problem(HttpStatusCode.NotFound, ProblemCodes.NotFound));

        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.ListCampusesAsync());

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.NotFound));
    }

    [Test]
    public async Task ListCampusesAsync_CallsGetApiCampuses()
    {
        var (gateway, stub) = CreateGateway(_ => JsonResponse(HttpStatusCode.OK, Array.Empty<CampusDto>()));

        await gateway.ListCampusesAsync();

        var recorded = stub.Requests.Single();
        Assert.That(recorded.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(recorded.Path, Is.EqualTo("/api/campuses"));
        Assert.That(recorded.Uri.Query, Is.Empty);
    }

    private static (HttpCampusGateway Gateway, StubHttpMessageHandler Stub) CreateGateway(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var stub = new StubHttpMessageHandler(responder);
        var client = new HttpClient(stub) { BaseAddress = new Uri("http://localhost") };
        return (new HttpCampusGateway(client), stub);
    }

    private static CampusDto[] SampleCampuses() =>
        CampusSeedData.CreateCampuses()
            .Select(c => new CampusDto(c.Id, c.Name, c.ShortName, c.Latitude, c.Longitude, c.IsDefault))
            .ToArray();

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, ApiResponseReader.JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage Problem(HttpStatusCode status, string errorCode) =>
        new(status)
        {
            Content = new StringContent(
                $$"""{"title":"Error","status":{{(int)status}},"errorCode":"{{errorCode}}"}""",
                Encoding.UTF8,
                "application/problem+json")
        };
}