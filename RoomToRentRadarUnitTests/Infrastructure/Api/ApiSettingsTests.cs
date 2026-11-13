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
    [TestCase("ftp://api.example.test/")]
    public void CreateHttpClient_WhenBaseUrlInvalid_ThrowsArgumentException(string baseUrl)
    {
        Assert.Throws(Is.InstanceOf<ArgumentException>(), () => ApiSettings.CreateHttpClient(baseUrl));
    }

    [Test]
    public void ReleaseBaseUrl_UsesHttpsAndNonLoopbackHost()
    {
        // Arrange & Act
        using var client = ApiSettings.CreateHttpClient(ApiSettings.ReleaseBaseUrl, requireHttps: true);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(client.BaseAddress!.Scheme, Is.EqualTo(Uri.UriSchemeHttps));
            Assert.That(ApiSettings.IsLoopbackOrEmulatorHost(client.BaseAddress.Host), Is.False);
            Assert.That(ApiSettings.ReleaseBaseUrl, Is.EqualTo("https://api.roomtorentradar.com/"));
        });
    }

    [TestCase("http://api.roomtorentradar.com/")]
    [TestCase("https://localhost:5000/")]
    [TestCase("https://127.0.0.1:5000/")]
    [TestCase("https://10.0.2.2:5000/")]
    public void CreateHttpClient_WhenRequireHttpsIsTrueAndUrlInsecureOrLocal_ThrowsArgumentException(string baseUrl)
    {
        Assert.Throws<ArgumentException>(() => ApiSettings.CreateHttpClient(baseUrl, requireHttps: true));
    }

    [TestCase(ApiSettings.DebugBaseUrl)]
    [TestCase(ApiSettings.AndroidEmulatorDebugBaseUrl)]
    public void CreateHttpClient_WhenRequireHttpsIsFalse_AllowsLocalDebugHttpEndpoints(string baseUrl)
    {
        using var client = ApiSettings.CreateHttpClient(baseUrl, requireHttps: false);
        Assert.That(client.BaseAddress, Is.Not.Null);
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
