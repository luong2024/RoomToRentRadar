using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Adapters.Outbound.Authentication;
using RoomToRentRadar.Adapters.Outbound.Routing;
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

    [Test]
    public void AddOutboundAdapters_WhenRoutingBaseUrlUsesUnsupportedScheme_ShouldFailOptionsValidation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Routing:BaseUrl"] = "ftp://osrm.test/"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOutboundAdapters(configuration);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            _ = provider.GetRequiredService<IOptions<RoutingOptions>>().Value);

        Assert.That(exception!.Failures, Does.Contain("Routing:BaseUrl must be an absolute HTTP(S) URL."));
    }

    [Test]
    public void AddOutboundAdapters_WhenRoutingBaseUrlHasPathWithoutTrailingSlash_ShouldPreservePathForRelativeRequests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Routing:BaseUrl"] = "https://osrm.test/custom-path"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOutboundAdapters(configuration);
        using var provider = services.BuildServiceProvider();
        var clientFactory = provider.GetRequiredService<IHttpClientFactory>();

        using var client = clientFactory.CreateClient("IRoutingProvider");

        Assert.That(client.BaseAddress, Is.EqualTo(new Uri("https://osrm.test/custom-path/")));
    }
}
