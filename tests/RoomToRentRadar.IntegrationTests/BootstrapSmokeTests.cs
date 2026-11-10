using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Authentication;
using RoomToRentRadar.Bootstrap.DependencyInjection;

namespace RoomToRentRadar.IntegrationTests;

public class BootstrapSmokeTests
{
    [Test]
    public void ProgramType_ShouldBeAccessibleForIntegrationTesting()
    {
        Assert.That(typeof(Program), Is.Not.Null);
    }

    [Test]
    public void AddOutboundAdapters_WhenJwtSecretMissing_ShouldFailOptionsValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "RoomToRentRadar.Api",
                ["Jwt:Audience"] = "RoomToRentRadar.Client"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOutboundAdapters(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        });
    }
}
