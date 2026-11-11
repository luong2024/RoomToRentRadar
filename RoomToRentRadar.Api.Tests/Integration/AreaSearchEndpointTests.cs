using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class AreaSearchEndpointTests
{
    private const string Endpoint = "/api/v1/listings/search/area";

    private ListingsApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new ListingsApiFactory();
        await _factory.InitializeDatabaseAsync();
        _client = _factory.CreateClient();
    }

    [SetUp]
    public async Task SetUp()
    {
        await _factory.ResetDataAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SpatialListingsSeed.SeedAsync(dbContext);
        _factory.QueryCounter.Reset();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task PublishedListings_InsideEdgeAndVertex_AreReturnedInNewestOrder()
    {
        var result = await SearchAsync(Body(SpatialListingsSeed.RectangleRing(), limit: 100));

        var expected = new[]
        {
            SpatialListingsSeed.IdFor(14),
            SpatialListingsSeed.VertexSouthEastId,
            SpatialListingsSeed.VertexNorthWestId,
            SpatialListingsSeed.EdgeWestId,
            SpatialListingsSeed.CenterDId,
            SpatialListingsSeed.DuplicateKeyOneId,
            SpatialListingsSeed.DuplicateKeyTwoId,
            SpatialListingsSeed.CenterCId,
            SpatialListingsSeed.CenterBId,
            SpatialListingsSeed.CenterId
        };

        Assert.Multiple(() =>
        {
            Assert.That(result.Data.Select(x => x.Id), Is.EqualTo(expected));
            Assert.That(result.TotalCount, Is.EqualTo(SpatialListingsSeed.RectangleMatchCount));
            Assert.That(result.TotalCountCapped, Is.False);
            Assert.That(result.Pagination.Limit, Is.EqualTo(100));
            Assert.That(result.Pagination.HasMore, Is.False);
            Assert.That(result.Pagination.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task NonPublishedListings_InsideTheArea_AreNeverReturned()
    {
        var result = await SearchAsync(Body(SpatialListingsSeed.RectangleRing(), limit: 100));
        var returnedIds = result.Data.Select(x => x.Id).ToHashSet();

        Assert.Multiple(() =>
        {
            Assert.That(returnedIds, Does.Not.Contain(SpatialListingsSeed.DraftInsideId));
            Assert.That(returnedIds, Does.Not.Contain(SpatialListingsSeed.HiddenInsideId));
            Assert.That(returnedIds, Does.Not.Contain(SpatialListingsSeed.RemovedInsideId));
            Assert.That(returnedIds, Does.Not.Contain(SpatialListingsSeed.OutsideNorthId));
            Assert.That(returnedIds, Does.Not.Contain(SpatialListingsSeed.OutsideWestId));
        });
    }

    [Test]
    public async Task ConcavePolygon_ExcludesListingsInTheNotch()
    {
        var result = await SearchAsync(Body(SpatialListingsSeed.ConcaveRing(), limit: 100));
        var returnedIds = result.Data.Select(x => x.Id).ToHashSet();

        Assert.Multiple(() =>
        {
            Assert.That(returnedIds, Does.Contain(SpatialListingsSeed.CenterCId));
            Assert.That(returnedIds, Does.Contain(SpatialListingsSeed.CenterBId));
            Assert.That(returnedIds, Does.Not.Contain(SpatialListingsSeed.NotchInsideId));
            Assert.That(returnedIds, Does.Not.Contain(SpatialListingsSeed.VertexNorthWestId));
            Assert.That(result.TotalCount, Is.EqualTo(SpatialListingsSeed.RectangleMatchCount - 2));
        });
    }

    [Test]
    public async Task EmptyResult_ReturnsEmptyPageWithZeroTotal()
    {
        var ring = Ring((0d, 0d), (0.01, 0d), (0.01, 0.01), (0d, 0.01), (0d, 0d));

        var result = await SearchAsync(Body(ring));

        Assert.Multiple(() =>
        {
            Assert.That(result.Data, Is.Empty);
            Assert.That(result.TotalCount, Is.Zero);
            Assert.That(result.TotalCountCapped, Is.False);
            Assert.That(result.Pagination.HasMore, Is.False);
            Assert.That(result.Pagination.NextCursor, Is.Null);
        });
    }

    [TestCase(ListingSort.Newest)]
    [TestCase(ListingSort.PriceAsc)]
    [TestCase(ListingSort.PriceDesc)]
    [TestCase(ListingSort.AreaDesc)]
    public async Task CursorPageWalk_ReturnsEveryFilteredMatchExactlyOnce(ListingSort sort)
    {
        var expectedIds = new[]
        {
            SpatialListingsSeed.CenterBId,
            SpatialListingsSeed.CenterCId,
            SpatialListingsSeed.CenterDId,
            SpatialListingsSeed.DuplicateKeyOneId,
            SpatialListingsSeed.DuplicateKeyTwoId
        };

        var allItems = new List<ListingSummaryDto>();
        string? cursor = null;
        var pageCount = 0;

        do
        {
            var body = Body(SpatialListingsSeed.RectangleRing(), limit: 2, sort: ListingSortWire.ToWireValue(sort));
            body["filters"] = new JsonObject { ["bedrooms"] = 2 };

            if (cursor is not null)
            {
                body["cursor"] = cursor;
            }

            var page = await SearchAsync(body);

            Assert.Multiple(() =>
            {
                Assert.That(page.Data.Count, Is.LessThanOrEqualTo(2));
                Assert.That(page.TotalCount, Is.EqualTo(expectedIds.Length));
                Assert.That(page.TotalCountCapped, Is.False);
            });

            allItems.AddRange(page.Data);
            cursor = page.Pagination.NextCursor;
            pageCount++;
        }
        while (cursor is not null && pageCount < 50);

        Assert.Multiple(() =>
        {
            Assert.That(allItems.Select(x => x.Id), Is.EquivalentTo(expectedIds));
            Assert.That(allItems.Select(x => x.Id).Distinct().Count(), Is.EqualTo(allItems.Count));
            Assert.That(allItems, Has.All.Matches<ListingSummaryDto>(item => item.Bedrooms == 2));
        });

        AssertSorted(allItems, sort);
    }

    [Test]
    public async Task PriceFilter_IsAppliedBeforePaging()
    {
        var body = Body(SpatialListingsSeed.RectangleRing(), limit: 100);
        body["filters"] = new JsonObject { ["minPrice"] = 6_000_000, ["maxPrice"] = 8_000_000 };

        var result = await SearchAsync(body);

        Assert.Multiple(() =>
        {
            Assert.That(result.Data.Select(x => x.Id), Is.EqualTo(new[] { SpatialListingsSeed.CenterBId }));
            Assert.That(result.TotalCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TotalCount_IsExactWhenBelowTheCap()
    {
        var result = await SearchAsync(Body(SpatialListingsSeed.RectangleRing(), limit: 3));

        Assert.Multiple(() =>
        {
            Assert.That(result.Data, Has.Count.EqualTo(3));
            Assert.That(result.TotalCount, Is.EqualTo(SpatialListingsSeed.RectangleMatchCount));
            Assert.That(result.TotalCountCapped, Is.False);
            Assert.That(result.Pagination.HasMore, Is.True);
            Assert.That(result.Pagination.NextCursor, Is.Not.Null);
        });
    }

    [Test]
    public async Task TotalCount_IsCappedWhenMatchesExceedOneThousand()
    {
        const int seeded = 1005;

        await _factory.ResetDataAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SpatialListingsSeed.SeedManyInsideAsync(dbContext, seeded);
        }

        _factory.QueryCounter.Reset();

        var result = await SearchAsync(Body(SpatialListingsSeed.RectangleRing(), limit: 50));
        var again = await SearchAsync(Body(SpatialListingsSeed.RectangleRing(), limit: 50));

        Assert.Multiple(() =>
        {
            Assert.That(result.Data, Has.Count.EqualTo(50));
            Assert.That(result.TotalCount, Is.EqualTo(AreaSearchResultDto.TotalCountLimit));
            Assert.That(result.TotalCountCapped, Is.True);
            Assert.That(again.TotalCount, Is.EqualTo(result.TotalCount));
            Assert.That(again.TotalCountCapped, Is.True);
        });
    }

    [Test]
    public async Task CoverPhoto_IsSelectedWithoutExtraQueries()
    {
        _factory.QueryCounter.Reset();

        var result = await SearchAsync(Body(SpatialListingsSeed.RectangleRing(), limit: 100));
        var byId = result.Data.ToDictionary(x => x.Id);

        Assert.Multiple(() =>
        {
            Assert.That(
                byId[SpatialListingsSeed.CenterId].CoverPhotoUrl,
                Is.EqualTo("/media/spatial/thumbs/center.jpg"),
                "the cover thumbnail wins");
            Assert.That(
                byId[SpatialListingsSeed.CenterBId].CoverPhotoUrl,
                Is.EqualTo("/media/spatial/center-b.jpg"),
                "a cover without a thumbnail falls back to the full url");
            Assert.That(
                byId[SpatialListingsSeed.CenterCId].CoverPhotoUrl,
                Is.Null,
                "a listing without photos has no cover");
            Assert.That(
                byId[SpatialListingsSeed.CenterDId].CoverPhotoUrl,
                Is.EqualTo("/media/spatial/thumbs/center-d-0.jpg"),
                "without an explicit cover the lowest sort order wins");
            Assert.That(_factory.QueryCounter.ListingsSelectCount, Is.EqualTo(1));
            Assert.That(_factory.QueryCounter.ListingPhotoSelectCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task EmptyPage_DoesNotQueryPhotos()
    {
        _factory.QueryCounter.Reset();

        var ring = Ring((0d, 0d), (0.01, 0d), (0.01, 0.01), (0d, 0.01), (0d, 0d));
        await SearchAsync(Body(ring));

        Assert.Multiple(() =>
        {
            Assert.That(_factory.QueryCounter.ListingsSelectCount, Is.EqualTo(1));
            Assert.That(_factory.QueryCounter.ListingPhotoSelectCount, Is.Zero);
        });
    }

    [Test]
    public async Task MissingArea_ReturnsInvalidParameterFormat()
    {
        var body = Body(SpatialListingsSeed.RectangleRing());
        body.Remove("area");

        await AssertProblemAsync(body, HttpStatusCode.BadRequest, ListingProblemFactory.InvalidParameterFormat);
    }

    [Test]
    public async Task MalformedArea_ReturnsInvalidParameterFormat()
    {
        var body = Body(SpatialListingsSeed.RectangleRing());
        body["area"] = "Polygon";

        await AssertProblemAsync(body, HttpStatusCode.BadRequest, ListingProblemFactory.InvalidParameterFormat);
    }

    [TestCase(0, "invalid_limit")]
    [TestCase(101, "invalid_limit")]
    [TestCase(-5, "invalid_limit")]
    public async Task InvalidLimit_IsRejected(int limit, string expectedCode)
        => await AssertProblemAsync(
            Body(SpatialListingsSeed.RectangleRing(), limit: limit),
            HttpStatusCode.BadRequest,
            expectedCode);

    [Test]
    public async Task InvalidSort_IsRejected()
        => await AssertProblemAsync(
            Body(SpatialListingsSeed.RectangleRing(), sort: "popular"),
            HttpStatusCode.BadRequest,
            ListingProblemFactory.InvalidSort);

    [Test]
    public async Task InvalidPriceRange_IsRejected()
    {
        var body = Body(SpatialListingsSeed.RectangleRing());
        body["filters"] = new JsonObject { ["minPrice"] = 10_000_000, ["maxPrice"] = 1_000_000 };

        await AssertProblemAsync(body, HttpStatusCode.BadRequest, ListingProblemFactory.InvalidPriceRange);
    }

    [Test]
    public async Task InvalidBedrooms_IsRejected()
    {
        var body = Body(SpatialListingsSeed.RectangleRing());
        body["filters"] = new JsonObject { ["bedrooms"] = 21 };

        await AssertProblemAsync(body, HttpStatusCode.BadRequest, ListingProblemFactory.InvalidBedrooms);
    }

    [Test]
    public async Task InvalidCursor_IsRejected()
        => await AssertProblemAsync(
            Body(SpatialListingsSeed.RectangleRing(), cursor: "not-a-cursor"),
            HttpStatusCode.BadRequest,
            ListingProblemFactory.InvalidCursor);

    [Test]
    public async Task CursorSortMismatch_IsRejected()
    {
        var firstPage = await SearchAsync(
            Body(SpatialListingsSeed.RectangleRing(), limit: 2, sort: ListingSortWire.PriceAsc));
        Assert.That(firstPage.Pagination.NextCursor, Is.Not.Null);

        await AssertProblemAsync(
            Body(SpatialListingsSeed.RectangleRing(), limit: 2, sort: ListingSortWire.Newest, cursor: firstPage.Pagination.NextCursor),
            HttpStatusCode.BadRequest,
            ListingProblemFactory.CursorSortMismatch);
    }

    [Test]
    public async Task WrongGeometryType_ReturnsUnprocessableArea()
    {
        var body = Body(SpatialListingsSeed.RectangleRing());
        body["area"]!["type"] = "LineString";

        await AssertProblemAsync(body, HttpStatusCode.UnprocessableEntity, GeoSearch.InvalidAreaType, GeoSearch.AreaField);
    }

    [Test]
    public async Task UnclosedRing_ReturnsUnprocessableArea()
    {
        var body = Body(Ring(
            (SpatialListingsSeed.MinLng, SpatialListingsSeed.MinLat),
            (SpatialListingsSeed.MaxLng, SpatialListingsSeed.MinLat),
            (SpatialListingsSeed.MaxLng, SpatialListingsSeed.MaxLat),
            (SpatialListingsSeed.MinLng, SpatialListingsSeed.MaxLat),
            (SpatialListingsSeed.MinLng, SpatialListingsSeed.MinLat + 0.001)));

        await AssertProblemAsync(body, HttpStatusCode.UnprocessableEntity, GeoSearch.InvalidAreaRing, GeoSearch.CoordinatesField);
    }

    [Test]
    public async Task TooManyVertices_ReturnsUnprocessableArea()
    {
        var body = Body(CircleRing(106.70, 10.77, 0.01, vertexCount: 201));

        await AssertProblemAsync(
            body,
            HttpStatusCode.UnprocessableEntity,
            GeoSearch.InvalidAreaVertexCount,
            GeoSearch.CoordinatesField);
    }

    [Test]
    public async Task SelfIntersectingPolygon_ReturnsUnprocessableArea()
    {
        var body = Body(Ring(
            (106.68, 10.75),
            (106.72, 10.75),
            (106.72, 10.79),
            (106.70, 10.74),
            (106.68, 10.79),
            (106.68, 10.75)));

        await AssertProblemAsync(
            body,
            HttpStatusCode.UnprocessableEntity,
            GeoSearch.SelfIntersectingArea,
            GeoSearch.CoordinatesField);
    }

    [Test]
    public async Task PolygonWithHoles_ReturnsUnprocessableArea()
    {
        var body = Body(SpatialListingsSeed.RectangleRing());
        body["area"]!["coordinates"] = new JsonArray(
            RingToJson(SpatialListingsSeed.RectangleRing()),
            RingToJson(Ring((106.69, 10.76), (106.71, 10.76), (106.71, 10.78), (106.69, 10.78), (106.69, 10.76))));

        await AssertProblemAsync(
            body,
            HttpStatusCode.UnprocessableEntity,
            GeoSearch.UnsupportedAreaHoles,
            GeoSearch.CoordinatesField);
    }

    [Test]
    public async Task OutOfRangeCoordinate_ReturnsUnprocessableArea()
    {
        var body = Body(Ring(
            (SpatialListingsSeed.MinLng, SpatialListingsSeed.MinLat),
            (200, SpatialListingsSeed.MinLat),
            (SpatialListingsSeed.MaxLng, SpatialListingsSeed.MaxLat),
            (SpatialListingsSeed.MinLng, SpatialListingsSeed.MaxLat),
            (SpatialListingsSeed.MinLng, SpatialListingsSeed.MinLat)));

        await AssertProblemAsync(
            body,
            HttpStatusCode.UnprocessableEntity,
            GeoSearch.InvalidAreaCoordinate,
            GeoSearch.CoordinatesField);
    }

    [Test]
    public async Task AreaTooLarge_ReturnsUnprocessableArea()
    {
        var body = Body(Ring(
            (SpatialListingsSeed.MinLng, SpatialListingsSeed.MinLat),
            (SpatialListingsSeed.MinLng + 0.5, SpatialListingsSeed.MinLat),
            (SpatialListingsSeed.MinLng + 0.5, SpatialListingsSeed.MinLat + 0.5),
            (SpatialListingsSeed.MinLng, SpatialListingsSeed.MinLat + 0.5),
            (SpatialListingsSeed.MinLng, SpatialListingsSeed.MinLat)));

        await AssertProblemAsync(
            body,
            HttpStatusCode.UnprocessableEntity,
            GeoSearch.AreaTooLarge,
            GeoSearch.CoordinatesField);
    }

    [Test]
    public async Task AntimeridianRing_ReturnsUnprocessableArea()
    {
        var body = Body(Ring((-179d, 10d), (179d, 10d), (179d, 11d), (-179d, 11d), (-179d, 10d)));

        await AssertProblemAsync(
            body,
            HttpStatusCode.UnprocessableEntity,
            GeoSearch.AntimeridianUnsupported,
            GeoSearch.CoordinatesField);
    }

    [Test]
    public async Task UnprocessableArea_ReportsRfc9110TypeAndTitle()
    {
        var body = Body(SpatialListingsSeed.RectangleRing());
        body["area"]!["type"] = "Point";

        var problem = await AssertProblemAsync(
            body,
            HttpStatusCode.UnprocessableEntity,
            GeoSearch.InvalidAreaType,
            GeoSearch.AreaField);

        Assert.Multiple(() =>
        {
            Assert.That(problem.GetProperty("type").GetString(), Is.EqualTo("https://tools.ietf.org/html/rfc9110#section-15.5.21"));
            Assert.That(problem.GetProperty("title").GetString(), Is.EqualTo("Invalid search area"));
            Assert.That(problem.GetProperty("status").GetInt32(), Is.EqualTo(422));
            Assert.That(problem.GetProperty("detail").GetString(), Is.Not.Empty);
        });
    }

    private async Task<AreaSearchResultDto> SearchAsync(JsonObject body)
    {
        var response = await _client.PostAsync(Endpoint, JsonContent(body));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), payload);

        var result = JsonSerializer.Deserialize<AreaSearchResultDto>(payload, ApiJson.Options);
        Assert.That(result, Is.Not.Null, payload);
        return result!;
    }

    private async Task<JsonElement> AssertProblemAsync(
        JsonObject body,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string? expectedField = null)
    {
        var response = await _client.PostAsync(Endpoint, JsonContent(body));
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(expectedStatus), payload);
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        });

        using var document = JsonDocument.Parse(payload);
        var problem = document.RootElement.Clone();
        Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(expectedCode), payload);

        if (expectedField is not null)
        {
            Assert.That(problem.GetProperty("field").GetString(), Is.EqualTo(expectedField), payload);
        }

        return problem;
    }

    private static StringContent JsonContent(JsonObject body)
        => new(body.ToJsonString(), Encoding.UTF8, "application/json");

    private static JsonObject Body(
        double[][] ring,
        int? limit = null,
        string? sort = null,
        string? cursor = null)
    {
        var body = new JsonObject
        {
            ["area"] = new JsonObject
            {
                ["type"] = "Polygon",
                ["coordinates"] = new JsonArray(RingToJson(ring))
            }
        };

        if (limit is { } limitValue)
        {
            body["limit"] = limitValue;
        }

        if (sort is not null)
        {
            body["sort"] = sort;
        }

        if (cursor is not null)
        {
            body["cursor"] = cursor;
        }

        return body;
    }

    private static JsonArray RingToJson(double[][] ring)
        => new(ring
            .Select(position => (JsonNode?)new JsonArray(
                position.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()))
            .ToArray());

    private static double[][] Ring(params (double Lng, double Lat)[] positions)
        => positions.Select(position => new[] { position.Lng, position.Lat }).ToArray();

    private static double[][] CircleRing(double centerLng, double centerLat, double radiusDegrees, int vertexCount)
    {
        var ring = new double[vertexCount + 1][];
        for (var index = 0; index < vertexCount; index++)
        {
            var angle = 2 * Math.PI * index / vertexCount;
            ring[index] =
            [
                centerLng + radiusDegrees * Math.Cos(angle),
                centerLat + radiusDegrees * Math.Sin(angle)
            ];
        }

        ring[vertexCount] = [ring[0][0], ring[0][1]];
        return ring;
    }

    private static void AssertSorted(IReadOnlyList<ListingSummaryDto> items, ListingSort sort)
    {
        for (var index = 1; index < items.Count; index++)
        {
            var previous = items[index - 1];
            var current = items[index];
            var comparison = sort switch
            {
                ListingSort.Newest => -previous.CreatedAt.CompareTo(current.CreatedAt),
                ListingSort.PriceAsc => previous.Price.CompareTo(current.Price),
                ListingSort.PriceDesc => -previous.Price.CompareTo(current.Price),
                ListingSort.AreaDesc => -previous.AreaM2.CompareTo(current.AreaM2),
                _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null)
            };

            Assert.That(comparison, Is.LessThanOrEqualTo(0), $"Items are not sorted by {sort} at index {index}.");

            if (comparison == 0)
            {
                Assert.That(
                    previous.Id.CompareTo(current.Id),
                    Is.LessThan(0),
                    $"Equal sort keys must be ordered by id at index {index}.");
            }
        }
    }
}
