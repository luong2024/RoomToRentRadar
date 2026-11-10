using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;

namespace RoomToRentRadar.Adapters.Tests;

public class HealthControllerTests
{
    [Test]
    public void GetHealth_ShouldReturnOkResult()
    {
        var controller = new HealthController();

        var result = controller.GetHealth();

        Assert.That(result, Is.InstanceOf<OkObjectResult>());
    }
}
