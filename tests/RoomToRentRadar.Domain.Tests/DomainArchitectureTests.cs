using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Domain.Tests;

public class DomainArchitectureTests
{
    [Test]
    public void DomainAssembly_ShouldNotReferenceInfrastructureOrApplicationAssemblies()
    {
        var domainAssembly = typeof(DomainException).Assembly;
        var referencedAssemblies = domainAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(referencedAssemblies, Has.None.StartWith("RoomToRentRadar.Application"));
            Assert.That(referencedAssemblies, Has.None.StartWith("RoomToRentRadar.Adapters"));
            Assert.That(referencedAssemblies, Has.None.StartWith("RoomToRentRadar.Bootstrap"));
            Assert.That(referencedAssemblies, Has.None.StartWith("Microsoft.AspNetCore"));
            Assert.That(referencedAssemblies, Has.None.StartWith("Microsoft.EntityFrameworkCore"));
            Assert.That(referencedAssemblies, Has.None.StartWith("Microsoft.IdentityModel"));
            Assert.That(referencedAssemblies, Has.None.StartWith("BCrypt"));
            Assert.That(referencedAssemblies, Has.None.StartWith("Microsoft.Azure.Cosmos"));
        });
    }
}
