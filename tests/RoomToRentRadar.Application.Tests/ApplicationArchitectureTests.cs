using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Tests;

public class ApplicationArchitectureTests
{
    [Test]
    public void ApplicationAssembly_ShouldOnlyReferenceDomainWithinSolution()
    {
        var applicationAssembly = typeof(PagedResult<>).Assembly;
        var referencedAssemblies = applicationAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(referencedAssemblies, Has.None.StartWith("RoomToRentRadar.Adapters"));
            Assert.That(referencedAssemblies, Has.None.StartWith("RoomToRentRadar.Bootstrap"));
            Assert.That(referencedAssemblies, Has.None.StartWith("Microsoft.AspNetCore"));
            Assert.That(referencedAssemblies, Has.None.StartWith("Microsoft.Azure.Cosmos"));
        });
    }
}
