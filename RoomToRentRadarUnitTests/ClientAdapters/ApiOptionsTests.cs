using RoomToRentRadar.Client.Adapters;

namespace RoomToRentRadarUnitTests.ClientAdapters;

[TestFixture]
public class ApiOptionsTests
{
    [Test]
    public void Defaults_UseMocksFalse_Timeout15s()
    {
        var options = new ApiOptions();

        Assert.That(options.UseMocks, Is.False);
        Assert.That(options.Timeout, Is.EqualTo(TimeSpan.FromSeconds(15)));
    }

    [Test]
    public void Validate_AndroidEmulatorUrl_Ok()
    {
        var options = new ApiOptions { BaseUrl = new Uri(ApiOptions.AndroidEmulatorBaseUrl) };

        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void Validate_Localhost_Ok()
    {
        var options = new ApiOptions { BaseUrl = new Uri(ApiOptions.LocalhostBaseUrl) };

        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void Validate_RelativeUrl_Throws()
    {
        var options = new ApiOptions { BaseUrl = new Uri("/api/auth/login", UriKind.Relative) };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Test]
    public void Validate_FtpScheme_Throws()
    {
        var options = new ApiOptions { BaseUrl = new Uri("ftp://localhost:5000") };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Test]
    public void Validate_UrlWithPath_Throws()
    {
        var options = new ApiOptions { BaseUrl = new Uri("http://localhost:5000/api") };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Test]
    public void Validate_RequireHttpsWithHttp_Throws()
    {
        var options = new ApiOptions { BaseUrl = new Uri(ApiOptions.LocalhostBaseUrl) };

        Assert.Throws<InvalidOperationException>(() => options.Validate(requireHttps: true));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Validate_ZeroOrNegativeTimeout_Throws(int seconds)
    {
        var options = new ApiOptions { Timeout = TimeSpan.FromSeconds(seconds) };

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Test]
    public void TryNormalizeBaseUrl_DevTunnelsHttps_TrimsAndStripsTrailingSlash()
    {
        var ok = ApiOptions.TryNormalizeBaseUrl(
            "  https://abc123-5000.asse.devtunnels.ms/  ", requireHttps: true, out var url);

        Assert.That(ok, Is.True);
        Assert.That(url, Is.Not.Null);
        Assert.That(url!.AbsoluteUri, Is.EqualTo("https://abc123-5000.asse.devtunnels.ms/"));
        Assert.That(url.AbsolutePath, Is.EqualTo("/"));
        Assert.That(url.Scheme, Is.EqualTo(Uri.UriSchemeHttps));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not a url")]
    [TestCase("/relative/path")]
    [TestCase("ftp://localhost:5000")]
    [TestCase("https://x.dev/api")]
    [TestCase("https://x.dev/?q=1")]
    [TestCase("https://x.dev/#frag")]
    public void TryNormalizeBaseUrl_NullEmptyGarbage_ReturnsFalse(string? raw)
    {
        var ok = ApiOptions.TryNormalizeBaseUrl(raw, requireHttps: false, out var url);

        Assert.That(ok, Is.False);
        Assert.That(url, Is.Null);
    }

    [Test]
    public void TryNormalizeBaseUrl_HttpWhenRequireHttps_ReturnsFalse()
    {
        var ok = ApiOptions.TryNormalizeBaseUrl(
            ApiOptions.AndroidEmulatorBaseUrl, requireHttps: true, out var url);

        Assert.That(ok, Is.False);
        Assert.That(url, Is.Null);
    }
}
