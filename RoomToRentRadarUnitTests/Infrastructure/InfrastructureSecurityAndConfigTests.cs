using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Infrastructure;
using RoomToRentRadar.Infrastructure.Adapters.Mocks;
using RoomToRentRadar.Infrastructure.Adapters.Security;
using RoomToRentRadar.Ports.Outbound.ExternalServices;

namespace RoomToRentRadarUnitTests.Infrastructure;

[TestFixture]
public class InfrastructureSecurityAndConfigTests
{
    [Test]
    public void AddInfrastructure_DefaultCall_RegistersRealIctuSsoVerifierAdapter()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();

        var descriptor = services.Single(d => d.ServiceType == typeof(IIctuSsoVerifierPort));
        Assert.That(descriptor.ImplementationType, Is.EqualTo(typeof(IctuSsoVerifierAdapter)));

        var googleDescriptor = services.Single(d => d.ServiceType == typeof(IGoogleAuthVerifierPort));
        Assert.That(googleDescriptor.ImplementationType, Is.EqualTo(typeof(GoogleAuthVerifierAdapter)));
    }

    [Test]
    public void AddInfrastructure_ExplicitMockTrue_RegistersMockIctuSsoVerifierAdapter()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(useMockSsoVerifier: true);

        var descriptor = services.Single(d => d.ServiceType == typeof(IIctuSsoVerifierPort));
        Assert.That(descriptor.ImplementationType, Is.EqualTo(typeof(MockIctuSsoVerifierAdapter)));

        var googleDescriptor = services.Single(d => d.ServiceType == typeof(IGoogleAuthVerifierPort));
        Assert.That(googleDescriptor.ImplementationType, Is.EqualTo(typeof(MockGoogleAuthVerifierAdapter)));
    }
}

