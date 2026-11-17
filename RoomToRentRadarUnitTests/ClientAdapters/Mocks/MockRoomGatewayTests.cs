using System.Net;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Datastore.Sqlite.Seed;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Services;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadarUnitTests.ClientAdapters.Mocks;

/// <summary>
/// T00.14 Phase 8 (AC-4): MockRoomGateway reproduces the seed rooms and the search semantics of
/// SearchRoomsUseCase (filters, four sorts, paging, campus origin, problem error codes) deterministically.
/// </summary>
[TestFixture]
public class MockRoomGatewayTests
{
    private const double IctuLatitude = 21.5851;
    private const double IctuLongitude = 105.8066;
    private const double TnueLatitude = 21.59861;
    private const double TnueLongitude = 105.82163;

    private static readonly string[] DistanceOrder =
        ["seed-z115", "seed-thao-nguyen", "seed-tan-thinh", "seed-thinh-dan", "seed-ktx-32"];

    private readonly MockRoomGateway _gateway = new();

    [Test]
    public async Task SearchRoomsAsync_DefaultRequest_Returns5RoomsOrderedByDistance()
    {
        var result = await _gateway.SearchRoomsAsync(new RoomSearchRequest());

        Assert.That(result.TotalCount, Is.EqualTo(5));
        Assert.That(result.Items.Select(c => c.Id).ToList(), Is.EqualTo(DistanceOrder));
        foreach (var card in result.Items)
        {
            Assert.Multiple(() =>
            {
                Assert.That(card.DistanceKm, Is.EqualTo(
                    HaversineDistanceCalculator.CalculateDistanceKm(IctuLatitude, IctuLongitude, card.Latitude, card.Longitude)));
                Assert.That(card.IsVerified, Is.True);
                Assert.That(card.Status, Is.EqualTo(ListingStatus.Available));
                Assert.That(card.CoverThumbUrl, Is.Null);
                Assert.That(card.IsFavourite, Is.False);
            });
        }
    }

    [Test]
    public async Task SearchRoomsAsync_PriceAsc_OrdersByRentAscending()
    {
        var result = await _gateway.SearchRoomsAsync(new RoomSearchRequest(Sort: "price_asc"));

        Assert.That(result.Items.Select(c => c.Id).ToList(), Is.EqualTo(new[]
        {
            "seed-thao-nguyen", "seed-z115", "seed-ktx-32", "seed-tan-thinh", "seed-thinh-dan"
        }));
    }

    [Test]
    public async Task SearchRoomsAsync_PriceDesc_OrdersByRentDescending()
    {
        var result = await _gateway.SearchRoomsAsync(new RoomSearchRequest(Sort: "price_desc"));

        Assert.That(result.Items.Select(c => c.Id).ToList(), Is.EqualTo(new[]
        {
            "seed-thinh-dan", "seed-tan-thinh", "seed-ktx-32", "seed-z115", "seed-thao-nguyen"
        }));
    }

    [Test]
    public async Task SearchRoomsAsync_Newest_OrdersByPublishedAtDescending()
    {
        var result = await _gateway.SearchRoomsAsync(new RoomSearchRequest(Sort: "newest"));

        Assert.That(result.Items.Select(c => c.Id).ToList(), Is.EqualTo(new[]
        {
            "seed-ktx-32", "seed-thinh-dan", "seed-thao-nguyen", "seed-tan-thinh", "seed-z115"
        }));
    }

    [Test]
    public async Task SearchRoomsAsync_RoomTypeFilter_ReturnsOnlyMatchingType()
    {
        var result = await _gateway.SearchRoomsAsync(new RoomSearchRequest(RoomTypes: new[] { RoomType.BoardingRoom }));

        Assert.That(result.TotalCount, Is.EqualTo(2));
        Assert.That(result.Items.Select(c => c.Id).ToList(), Is.EqualTo(new[] { "seed-z115", "seed-thao-nguyen" }));
        Assert.That(result.Items.All(c => c.RoomType == RoomType.BoardingRoom), Is.True);
    }

    [Test]
    public async Task SearchRoomsAsync_RentRangeFilter_AppliesInclusiveBounds()
    {
        var result = await _gateway.SearchRoomsAsync(new RoomSearchRequest(MinRentVnd: 1_500_000, MaxRentVnd: 2_800_000));

        Assert.That(result.Items.Select(c => c.Id).ToList(), Is.EqualTo(new[] { "seed-z115", "seed-tan-thinh", "seed-ktx-32" }));
    }

    [Test]
    public async Task SearchRoomsAsync_AreaRangeFilter_AppliesInclusiveBounds()
    {
        var result = await _gateway.SearchRoomsAsync(new RoomSearchRequest(MinAreaM2: 18, MaxAreaM2: 28));

        Assert.That(result.Items.Select(c => c.Id).ToList(), Is.EqualTo(new[] { "seed-z115", "seed-tan-thinh", "seed-ktx-32" }));
    }

    [Test]
    public async Task SearchRoomsAsync_MaxDistanceFilter_CutsByExactDistance()
    {
        var result = await _gateway.SearchRoomsAsync(new RoomSearchRequest(MaxDistanceKm: 1.0));

        Assert.That(result.TotalCount, Is.EqualTo(3));
        Assert.That(result.Items.Select(c => c.Id).ToList(), Is.EqualTo(new[] { "seed-z115", "seed-thao-nguyen", "seed-tan-thinh" }));
        Assert.That(result.Items.All(c => c.DistanceKm <= 1.0), Is.True);
    }

    [Test]
    public async Task SearchRoomsAsync_Paging_ReturnsCorrectPageAndTotals()
    {
        var result = await _gateway.SearchRoomsAsync(new RoomSearchRequest(PageNumber: 2, PageSize: 2));

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Select(c => c.Id).ToList(), Is.EqualTo(new[] { "seed-tan-thinh", "seed-thinh-dan" }));
            Assert.That(result.TotalCount, Is.EqualTo(5));
            Assert.That(result.TotalPages, Is.EqualTo(3));
            Assert.That(result.PageNumber, Is.EqualTo(2));
            Assert.That(result.PageSize, Is.EqualTo(2));
            Assert.That(result.HasPreviousPage, Is.True);
            Assert.That(result.HasNextPage, Is.True);
        });
    }

    [Test]
    public async Task SearchRoomsAsync_TnueCampus_ChangesDistanceKm()
    {
        var result = await _gateway.SearchRoomsAsync(new RoomSearchRequest(CampusId: "TNUE"));

        Assert.That(result.TotalCount, Is.EqualTo(5));
        foreach (var card in result.Items)
        {
            var expected = HaversineDistanceCalculator.CalculateDistanceKm(TnueLatitude, TnueLongitude, card.Latitude, card.Longitude);
            Assert.That(card.DistanceKm, Is.EqualTo(expected), card.Id);
            Assert.That(card.DistanceKm, Is.Not.EqualTo(
                HaversineDistanceCalculator.CalculateDistanceKm(IctuLatitude, IctuLongitude, card.Latitude, card.Longitude)), card.Id);
        }
    }

    [Test]
    public void SearchRoomsAsync_UnknownCampus_ThrowsApiExceptionCampusNotFound()
    {
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await _gateway.SearchRoomsAsync(new RoomSearchRequest(CampusId: "NOPE")));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.CampusNotFound));
    }

    [Test]
    public void SearchRoomsAsync_UnknownSort_ThrowsApiExceptionInvalidSortOption()
    {
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await _gateway.SearchRoomsAsync(new RoomSearchRequest(Sort: "bogus")));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.InvalidSortOption));
    }

    [Test]
    public void SearchRoomsAsync_EmptySort_ThrowsApiExceptionInvalidSortOption()
    {
        // Ruling 21: only null means the default; a present-but-empty sort is rejected like on the server.
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await _gateway.SearchRoomsAsync(new RoomSearchRequest(Sort: "")));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.InvalidSortOption));
    }

    [Test]
    public async Task SearchRoomsAsync_TwoCalls_ReturnIdenticalResults()
    {
        var first = await _gateway.SearchRoomsAsync(new RoomSearchRequest());
        var second = await _gateway.SearchRoomsAsync(new RoomSearchRequest());

        Assert.That(second.Items, Is.EqualTo(first.Items));
        Assert.That(second.TotalCount, Is.EqualTo(first.TotalCount));
    }

    [Test]
    public async Task SearchRoomsInAreaAsync_PolygonContainingRooms_ReturnsThoseRooms()
    {
        var polygon = new[]
        {
            new GeoPointDto(21.575, 105.795),
            new GeoPointDto(21.595, 105.795),
            new GeoPointDto(21.595, 105.815),
            new GeoPointDto(21.575, 105.815)
        };
        var request = new AreaSearchRequest("ICTU", null, null, null, null, null, null, "distance", polygon);

        var result = await _gateway.SearchRoomsInAreaAsync(request);

        Assert.That(result.TotalCount, Is.EqualTo(3));
        Assert.That(result.Truncated, Is.False);
        Assert.That(result.Items.Select(c => c.Id).ToList(), Is.EqualTo(new[] { "seed-z115", "seed-thao-nguyen", "seed-tan-thinh" }));
    }

    [Test]
    public async Task GetRoomDetailAsync_SeedRoom_ReturnsDetailMatchingSeed()
    {
        var seed = RoomSeedData.CreateRooms().Single(r => r.Id == "seed-z115");

        var detail = await _gateway.GetRoomDetailAsync("seed-z115");

        Assert.Multiple(() =>
        {
            Assert.That(detail.Id, Is.EqualTo(seed.Id));
            Assert.That(detail.Title, Is.EqualTo(seed.Title));
            Assert.That(detail.Description, Is.EqualTo(seed.Description));
            Assert.That(detail.Address.Ward, Is.EqualTo(seed.Ward));
            Assert.That(detail.Address.District, Is.EqualTo(seed.District));
            Assert.That(detail.Address.AddressLine, Is.EqualTo(seed.AddressLine));
            Assert.That(detail.MonthlyRentVnd, Is.EqualTo(seed.MonthlyRentVnd));
            Assert.That(detail.AreaM2, Is.EqualTo(seed.AreaM2));
            Assert.That(detail.RoomType, Is.EqualTo(seed.RoomType));
            Assert.That(detail.Status, Is.EqualTo(ListingStatus.Available));
            Assert.That(detail.IsVerified, Is.True);
            Assert.That(detail.DistanceKm, Is.EqualTo(
                HaversineDistanceCalculator.CalculateDistanceKm(IctuLatitude, IctuLongitude, seed.Latitude, seed.Longitude)));
            Assert.That(detail.Photos, Is.Empty);
            Assert.That(detail.HouseRules, Is.Null);
            Assert.That(detail.Landlord.Id, Is.EqualTo("seed-landlord"));
            Assert.That(detail.PublishedAtUtc, Is.EqualTo(seed.PublishedAtUtc));
        });
    }

    [Test]
    public void GetRoomDetailAsync_UnknownRoom_ThrowsApiExceptionRoomNotFound()
    {
        var ex = Assert.ThrowsAsync<ApiException>(async () => await _gateway.GetRoomDetailAsync("does-not-exist"));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));
    }

    [Test]
    public async Task GetSimilarRoomsAsync_SeedRoom_ExcludesSelfAndPrefersSameType()
    {
        var similar = await _gateway.GetSimilarRoomsAsync("seed-z115");

        Assert.That(similar.Select(c => c.Id), Does.Not.Contain("seed-z115"));
        Assert.That(similar[0].Id, Is.EqualTo("seed-thao-nguyen")); // the other BoardingRoom
        Assert.That(similar.All(c => c.Id != "seed-z115"), Is.True);
    }

    [Test]
    public void GetSimilarRoomsAsync_UnknownRoom_ThrowsApiExceptionRoomNotFound()
    {
        var ex = Assert.ThrowsAsync<ApiException>(async () => await _gateway.GetSimilarRoomsAsync("does-not-exist"));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));
    }

    [Test]
    public async Task CompareRoomsAsync_SeedRooms_ReturnsComparisonForRequestedIds()
    {
        var comparison = await _gateway.CompareRoomsAsync(new[] { "seed-z115", "seed-ktx-32" });

        Assert.That(comparison.CampusId, Is.EqualTo("ICTU"));
        Assert.That(comparison.Rooms.Select(r => r.Id).ToList(), Is.EqualTo(new[] { "seed-z115", "seed-ktx-32" }));
        Assert.That(comparison.Rooms[0].MonthlyRentVnd, Is.EqualTo(1_500_000L));
        Assert.That(comparison.Rooms[1].MonthlyRentVnd, Is.EqualTo(2_000_000L));
    }

    [Test]
    public void CompareRoomsAsync_UnknownRoom_ThrowsApiExceptionRoomNotFound()
    {
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await _gateway.CompareRoomsAsync(new[] { "seed-z115", "does-not-exist" }));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));
    }

    [Test]
    public async Task GetRoomTravelTimesAsync_SeedRoom_ReturnsFallbackEstimates()
    {
        var travel = await _gateway.GetRoomTravelTimesAsync("seed-z115");

        Assert.Multiple(() =>
        {
            Assert.That(travel.CampusId, Is.EqualTo("ICTU"));
            Assert.That(travel.IsFallback, Is.True);
            Assert.That(travel.StraightLineDistanceKm, Is.EqualTo(
                HaversineDistanceCalculator.CalculateDistanceKm(IctuLatitude, IctuLongitude, 21.5875, 105.8080)));
            Assert.That(travel.Items, Has.Count.EqualTo(2));
            Assert.That(travel.Items.All(i => i.IsEstimate), Is.True);
            Assert.That(travel.Items.Select(i => i.Mode).ToList(), Is.EqualTo(new[] { TravelMode.Motorbike, TravelMode.Walking }));
            Assert.That(travel.Items.All(i => i.DurationSeconds > 0 && i.DistanceMeters > 0), Is.True);
        });
    }

    [Test]
    public void GetRoomTravelTimesAsync_UnknownRoom_ThrowsApiExceptionRoomNotFound()
    {
        var ex = Assert.ThrowsAsync<ApiException>(async () => await _gateway.GetRoomTravelTimesAsync("does-not-exist"));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(ex.ErrorCode, Is.EqualTo(ProblemCodes.RoomNotFound));
    }
}