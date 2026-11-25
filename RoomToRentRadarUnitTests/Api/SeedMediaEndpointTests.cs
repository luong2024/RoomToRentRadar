using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>T00.10: the committed demo photos are served in Development only.</summary>
[TestFixture]
public class SeedMediaEndpointTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";
    private const string ImagePath = "/media/seed/rooms/house-01-thumb.jpg";

    private static WebApplicationFactory<Program> CreateFactory(string environment, bool? seedPhotos = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment(environment);
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            if (seedPhotos.HasValue)
            {
                webHost.UseSetting("DemoData:SeedPhotos", seedPhotos.Value ? "true" : "false");
            }

            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });

    [Test]
    public async Task Development_ServesSeedPhotoAsJpeg()
    {
        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ImagePath);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/jpeg"));
        });
    }

    [TestCase("/media/seed/manifest.json")]
    [TestCase("/media/seed/CREDITS.md")]
    [TestCase("/media/seed/rooms/..%2Fmanifest.json")]
    [TestCase("/media/seed/rooms/missing.jpg")]
    public async Task Development_PublishesOnlyTheImages(string path)
    {
        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase("Production", null)]
    [TestCase("Production", true)]
    [TestCase("Development", false)]
    public async Task OutsideDevelopmentOrSwitchedOff_SeedPhotosAreNotServed(string environment, bool? seedPhotos)
    {
        using var factory = CreateFactory(environment, seedPhotos);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ImagePath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
