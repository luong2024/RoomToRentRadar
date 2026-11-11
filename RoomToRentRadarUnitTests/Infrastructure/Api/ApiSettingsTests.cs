using RoomToRentRadar.Infrastructure.Api;

namespace RoomToRentRadarUnitTests.Infrastructure.Api;

[TestFixture]
public class ApiSettingsTests
{
    [Test]
    public void CreateHttpClient_WhenBaseUrlHasTrailingSlash_ResolvesRelativeRoomsEndpoint()
    {
        // Arrange & Act
        using var client = ApiSettings.CreateHttpClient("https://api.example.test/");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(client.BaseAddress, Is.EqualTo(new Uri("https://api.example.test/")));
            Assert.That(new Uri(client.BaseAddress!, "api/rooms?page=1").ToString(),
                Is.EqualTo("https://api.example.test/api/rooms?page=1"));
            Assert.That(client.Timeout, Is.EqualTo(ApiSettings.DefaultTimeout));
        });
    }

    [Test]
    public void CreateHttpClient_WhenBaseUrlHasPathWithoutTrailingSlash_KeepsPathSegment()
    {
        // Arrange & Act
        using var client = ApiSettings.CreateHttpClient("https://host.example.test/backend");

        // Assert
        Assert.That(new Uri(client.BaseAddress!, "api/rooms").ToString(),
            Is.EqualTo("https://host.example.test/backend/api/rooms"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not-a-url")]
    public void CreateHttpClient_WhenBaseUrlInvalid_ThrowsArgumentException(string baseUrl)
    {
        Assert.Throws(Is.InstanceOf<ArgumentException>(), () => ApiSettings.CreateHttpClient(baseUrl));
    }

    [Test]
    public void BaseUrl_ForCurrentConfiguration_IsAbsoluteWithTrailingSlash()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Uri.IsWellFormedUriString(ApiSettings.BaseUrl, UriKind.Absolute), Is.True);
            Assert.That(ApiSettings.BaseUrl, Does.EndWith("/"));
        });
    }
}
