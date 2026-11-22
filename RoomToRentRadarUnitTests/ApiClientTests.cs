using System.Net;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Notifications;
using RoomToRentRadar.Shared.SavedSearches;

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

    [Test]
    public async Task GetListingsAsync_WithFilters_BuildsSnakeCaseQueryAndParsesSearchResult()
    {
        var handler = new RecordingHandler("""
            {
              "data": [],
              "pagination": { "limit": 12, "next_cursor": "next-page", "has_more": true },
              "totalCount": 42,
              "totalCountCapped": false,
              "activeFilters": { "roomType": "studio", "maxDistanceKm": 5 }
            }
            """);
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        var result = await client.GetListingsAsync(
            12,
            "cursor value",
            new ActiveListingFiltersDto(
                1_500_000,
                3_000_000,
                20,
                40,
                null,
                RoomType.Studio,
                10.7721,
                106.6579,
                5));

        var query = handler.RequestUri!.Query;

        Assert.Multiple(() =>
        {
            Assert.That(query, Does.Contain("limit=12"));
            Assert.That(query, Does.Contain("cursor=cursor%20value"));
            Assert.That(query, Does.Contain("min_price=1500000"));
            Assert.That(query, Does.Contain("max_price=3000000"));
            Assert.That(query, Does.Contain("min_area_m2=20"));
            Assert.That(query, Does.Contain("max_area_m2=40"));
            Assert.That(query, Does.Contain("room_type=studio"));
            Assert.That(query, Does.Contain("campus_lat=10.7721"));
            Assert.That(query, Does.Contain("campus_lng=106.6579"));
            Assert.That(query, Does.Contain("max_distance_km=5"));
            Assert.That(result.TotalCount, Is.EqualTo(42));
            Assert.That(result.Pagination.NextCursor, Is.EqualTo("next-page"));
            Assert.That(result.ActiveFilters?.RoomType, Is.EqualTo(RoomType.Studio));
        });
    }

    [Test]
    public async Task GetListingsAsync_WithoutFilters_OmitsFilterParameters()
    {
        var handler = new RecordingHandler("""
            {
              "data": [],
              "pagination": { "limit": 12, "next_cursor": null, "has_more": false },
              "totalCount": 0,
              "totalCountCapped": false,
              "activeFilters": null
            }
            """);
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        await client.GetListingsAsync(12);

        var query = handler.RequestUri!.Query;

        Assert.Multiple(() =>
        {
            Assert.That(query, Does.Not.Contain("min_price"));
            Assert.That(query, Does.Not.Contain("min_area_m2"));
            Assert.That(query, Does.Not.Contain("room_type"));
            Assert.That(query, Does.Not.Contain("campus_lat"));
            Assert.That(query, Does.Not.Contain("max_distance_km"));
        });
    }

    [Test]
    public async Task CreateSavedSearchAsync_PostsTheNameAreaAndAlertState()
    {
        var handler = new CapturingHandler(() => JsonResponse("""
            {
              "id": "22222222-2222-2222-2222-222222222222",
              "name": "Quận 10",
              "alertState": "active",
              "area": { "type": "Polygon", "coordinates": [[[106.0, 10.0], [107.0, 10.0], [107.0, 11.0], [106.0, 10.0]]] },
              "createdAt": "2026-11-17T03:00:00Z",
              "updatedAt": "2026-11-17T03:00:00Z"
            }
            """));
        var client = CreateClient(handler);

        var created = await client.CreateSavedSearchAsync(new CreateSavedSearchRequest
        {
            Name = "Quận 10",
            AlertState = SavedSearchAlertState.Active,
            Area = Triangle()
        });

        using var body = JsonDocument.Parse(handler.Body!);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(handler.RequestUri!.AbsolutePath, Is.EqualTo("/api/v1/saved-searches"));
            Assert.That(body.RootElement.GetProperty("name").GetString(), Is.EqualTo("Quận 10"));
            Assert.That(body.RootElement.GetProperty("alertState").GetString(), Is.EqualTo("active"));
            Assert.That(body.RootElement.GetProperty("area").GetProperty("type").GetString(), Is.EqualTo("Polygon"));
            Assert.That(created.Id, Is.EqualTo(Guid.Parse("22222222-2222-2222-2222-222222222222")));
            Assert.That(created.AlertState, Is.EqualTo(SavedSearchAlertState.Active));
        });
    }

    [Test]
    public async Task SetSavedSearchAlertAsync_PutsTheStateAndReadsTheUpdatedSearch()
    {
        var id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var handler = new CapturingHandler(() => JsonResponse("""
            {
              "id": "33333333-3333-3333-3333-333333333333",
              "name": "Quận 10",
              "alertState": "paused",
              "area": { "type": "Polygon", "coordinates": [[[106.0, 10.0], [107.0, 10.0], [107.0, 11.0], [106.0, 10.0]]] },
              "createdAt": "2026-11-17T03:00:00Z",
              "updatedAt": "2026-11-17T04:00:00Z"
            }
            """));
        var client = CreateClient(handler);

        var updated = await client.SetSavedSearchAlertAsync(id, SavedSearchAlertState.Paused);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Method, Is.EqualTo(HttpMethod.Put));
            Assert.That(handler.RequestUri!.AbsolutePath, Is.EqualTo($"/api/v1/saved-searches/{id}/alert"));
            Assert.That(handler.Body, Does.Contain("\"alertState\":\"paused\""));
            Assert.That(updated?.AlertState, Is.EqualTo(SavedSearchAlertState.Paused));
        });
    }

    [Test]
    public async Task SetSavedSearchAlertAsync_WhenTheServerRowIsMissing_ReturnsNull()
    {
        var handler = new CapturingHandler(() => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var updated = await client.SetSavedSearchAlertAsync(Guid.NewGuid(), SavedSearchAlertState.Active);

        Assert.That(updated, Is.Null);
    }

    [Test]
    public async Task DeleteSavedSearchAsync_WhenTheServerRowIsAlreadyGone_DoesNotThrow()
    {
        var id = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var handler = new CapturingHandler(() => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        await client.DeleteSavedSearchAsync(id);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Method, Is.EqualTo(HttpMethod.Delete));
            Assert.That(handler.RequestUri!.AbsolutePath, Is.EqualTo($"/api/v1/saved-searches/{id}"));
        });
    }

    [Test]
    public async Task GetNotificationsAsync_RequestsTheInboxWithTheLimit()
    {
        var handler = new CapturingHandler(() => JsonResponse("""
            [
              {
                "id": "55555555-5555-5555-5555-555555555555",
                "title": "Phòng mới trong “Quận 10”",
                "body": "Phòng gần trường · 2.500.000 ₫ · 20 m²",
                "listingId": "11111111-1111-1111-1111-111111111111",
                "createdAt": "2026-11-17T03:05:00Z"
              }
            ]
            """));
        var client = CreateClient(handler);

        var notifications = await client.GetNotificationsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(handler.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(handler.RequestUri!.AbsolutePath, Is.EqualTo("/api/v1/notifications"));
            Assert.That(handler.RequestUri.Query, Is.EqualTo("?limit=20"));
            Assert.That(notifications, Has.Count.EqualTo(1));
            Assert.That(notifications[0].Title, Is.EqualTo("Phòng mới trong “Quận 10”"));
            Assert.That(notifications[0].ListingId, Is.EqualTo(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        });
    }

    private static AreaPolygon Triangle() => new()
    {
        Type = "Polygon",
        Coordinates = [[[106.0, 10.0], [107.0, 10.0], [107.0, 11.0], [106.0, 10.0]]]
    };

    private sealed class CapturingHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return respond();
        }
    }

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
