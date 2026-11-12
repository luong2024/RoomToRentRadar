using System.Net;
using System.Text;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ApiClientTests
{
    [Test]
    public async Task SearchByAreaAsync_PostsTheDrawnAreaAndReadsThePage()
    {
        string? body = null;
        var handler = new StubHandler(async request =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/api/v1/listings/search/area"));
            body = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""
                {
                  "data": [
                    {
                      "id": "11111111-1111-1111-1111-111111111111",
                      "title": "Phòng gần trường",
                      "price": 2500000,
                      "areaM2": 20,
                      "bedrooms": 1,
                      "address": "1 Đường ABC",
                      "lat": 10.77,
                      "lng": 106.7,
                      "createdAt": "2026-11-12T00:00:00Z",
                      "coverPhotoUrl": null
                    }
                  ],
                  "pagination": { "limit": 100, "next_cursor": null, "has_more": false },
                  "totalCount": 1,
                  "totalCountCapped": false
                }
                """);
        });
        var client = CreateClient(handler);

        var result = await client.SearchByAreaAsync(new AreaSearchRequest
        {
            Area = new AreaPolygon
            {
                Type = "Polygon",
                Coordinates = [[[106.0, 10.0], [107.0, 10.0], [107.0, 11.0], [106.0, 10.0]]]
            },
            Sort = ListingSortWire.DistanceAsc,
            Limit = 100
        });

        Assert.That(body, Does.Contain("\"type\":\"Polygon\""));
        Assert.That(body, Does.Contain("\"sort\":\"distance_asc\""));
        Assert.That(body, Does.Contain("\"limit\":100"));
        Assert.That(result.Data, Has.Count.EqualTo(1));
        Assert.That(result.Data[0].Title, Is.EqualTo("Phòng gần trường"));
        Assert.That(result.TotalCount, Is.EqualTo(1));
        Assert.That(result.Pagination.HasMore, Is.False);
    }

    [Test]
    public void SearchByAreaAsync_UnprocessableProblem_ThrowsApiExceptionWithCode()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent(
                """{"title":"Invalid drawn area","detail":"The drawn area is self-intersecting.","status":422,"code":"self_intersecting_area"}""",
                Encoding.UTF8,
                "application/problem+json")
        }));
        var client = CreateClient(handler);

        var exception = Assert.ThrowsAsync<ApiException>(() => client.SearchByAreaAsync(new AreaSearchRequest
        {
            Area = new AreaPolygon
            {
                Type = "Polygon",
                Coordinates = [[[106.0, 10.0], [107.0, 10.0], [106.0, 10.0]]]
            }
        }))!;

        Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
        Assert.That(exception.Code, Is.EqualTo("self_intersecting_area"));
        Assert.That(exception.Message, Is.EqualTo("The drawn area is self-intersecting."));
    }

    [Test]
    public void SearchByAreaAsync_NonJsonProblem_FallsBackToTheStatusMessage()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream unavailable")
        }));
        var client = CreateClient(handler);

        var exception = Assert.ThrowsAsync<ApiException>(() => client.SearchByAreaAsync(new AreaSearchRequest()))!;

        Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        Assert.That(exception.Code, Is.Null);
        Assert.That(exception.Message, Does.Contain("502"));
    }

    private static ApiClient CreateClient(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => respond(request);
    }
}
