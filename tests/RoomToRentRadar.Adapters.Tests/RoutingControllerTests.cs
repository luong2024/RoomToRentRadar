using Microsoft.AspNetCore.Mvc;
using Moq;
using RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using Xunit;
using Assert = Xunit.Assert;

namespace RoomToRentRadar.Adapters.Tests;

public class RoutingControllerTests
{
    [Fact]
    public async Task CalculateRoute_WhenRequestIsValid_ShouldReturnCalculatedRoute()
    {
        var request = new RouteRequest(21.0285, 105.8542, 21.0368, 105.8342);
        var expectedRoute = new RouteResponse(
            2.5,
            6,
            [new RouteCoordinate(21.0285, 105.8542), new RouteCoordinate(21.0368, 105.8342)]);
        var routingService = new Mock<IRoutingService>();
        routingService
            .Setup(service => service.GetRouteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRoute);
        var controller = new RoutingController(routingService.Object);

        var result = await controller.CalculateRoute(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expectedRoute, okResult.Value);
        routingService.Verify(service => service.GetRouteAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }
}
