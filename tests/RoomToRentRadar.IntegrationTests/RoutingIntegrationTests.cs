using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.IntegrationTests;

public class RoutingIntegrationTests
{
    private AuthWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
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
    public async Task CalculateRoute_WhenCoordinatesAreValid_ShouldReturnDistanceDurationAndGeometry()
    {
        _factory.RoutingProvider.Result = new RoutingProviderResult(
            2540,
            125,
            [new RouteCoordinate(21.0285, 105.8542), new RouteCoordinate(21.0368, 105.8342)]);

        var response = await _client.PostAsJsonAsync(
            "/api/routing",
            new RouteRequest(21.0285, 105.8542, 21.0368, 105.8342));

        var route = await response.Content.ReadFromJsonAsync<RouteResponse>();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(route, Is.Not.Null);
            Assert.That(route!.DistanceKm, Is.EqualTo(2.54));
            Assert.That(route.DurationMinutes, Is.EqualTo(3));
            Assert.That(route.RouteCoordinates, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task GetTravelTime_WhenRoomHasCoordinates_ShouldRouteFromStoredLocation()
    {
        _factory.RoomRepository.Upsert(new Room
        {
            Id = "routing-room",
            Latitude = 21.0285,
            Longitude = 105.8542
        });
        _factory.RoutingProvider.Result = new RoutingProviderResult(1000, 60, Array.Empty<RouteCoordinate>());

        var response = await _client.GetAsync(
            "/api/rooms/routing-room/travel-time?destinationLatitude=21.0368&destinationLongitude=105.8342");

        var route = await response.Content.ReadFromJsonAsync<RouteResponse>();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(route, Is.Not.Null);
            Assert.That(route!.DistanceKm, Is.EqualTo(1));
            Assert.That(route.DurationMinutes, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CalculateRoute_WhenCoordinatesAreInvalid_ShouldReturnBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/routing",
            new RouteRequest(91, 105.8542, 21.0368, 105.8342));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Detail, Does.Contain("latitude"));
        });
    }

    [Test]
    public async Task CalculateRoute_WhenNoRouteExists_ShouldReturnNotFound()
    {
        _factory.RoutingProvider.Result = null;

        var response = await _client.PostAsJsonAsync(
            "/api/routing",
            new RouteRequest(21.0285, 105.8542, 21.0368, 105.8342));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Title, Is.EqualTo("Route Not Found"));
        });
    }

    [TestCase(typeof(RoutingProviderUnavailableException), HttpStatusCode.BadGateway)]
    [TestCase(typeof(RoutingProviderTimeoutException), HttpStatusCode.GatewayTimeout)]
    public async Task CalculateRoute_WhenProviderFails_ShouldReturnMappedGatewayStatus(
        Type exceptionType,
        HttpStatusCode expectedStatusCode)
    {
        _factory.RoutingProvider.ExceptionToThrow = exceptionType == typeof(RoutingProviderUnavailableException)
            ? new RoutingProviderUnavailableException("OSRM is unavailable.")
            : new RoutingProviderTimeoutException();

        var response = await _client.PostAsJsonAsync(
            "/api/routing",
            new RouteRequest(21.0285, 105.8542, 21.0368, 105.8342));

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatusCode));
    }
}
