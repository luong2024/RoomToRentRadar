using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Api.Features.Auth;
using RoomToRentRadar.Api.Tests.Infrastructure;
using RoomToRentRadar.Ports.Models.Auth;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Tests.Integration;

[TestFixture]
public sealed class AuthRateLimitTests
{
    [Test]
    public async Task Login_ExceedingPerIpLimit_Returns429Problem()
    {
        using var factory = CreateFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        var first = await PostLoginAsync(client);
        var second = await PostLoginAsync(client);
        var third = await PostLoginAsync(client);

        Assert.Multiple(() =>
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
        await AssertRateLimitedProblemAsync(third);
    }

    [Test]
    public async Task Register_ExceedingPerIpLimit_Returns429Problem()
    {
        using var factory = CreateFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        var first = await PostRegisterAsync(client, "first.limit@example.test");
        var second = await PostRegisterAsync(client, "second.limit@example.test");
        var third = await PostRegisterAsync(client, "third.limit@example.test");

        Assert.Multiple(() =>
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        });
        await AssertRateLimitedProblemAsync(third);
    }

    [Test]
    public async Task VerifyRegistration_ExceedingPerIpLimit_Returns429Problem()
    {
        using var factory = CreateFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        var begin = await PostRegisterAsync(client, "verify.limit@example.test");
        var first = await PostVerifyAsync(client, Guid.NewGuid());
        var second = await PostVerifyAsync(client, Guid.NewGuid());
        var third = await PostVerifyAsync(client, Guid.NewGuid());

        Assert.Multiple(() =>
        {
            Assert.That(begin.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
        await AssertRateLimitedProblemAsync(third);
    }

    [Test]
    public async Task Verify_ExceedingVerifyLimit_DoesNotBlockLogin()
    {
        using var factory = CreateFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        var first = await PostVerifyAsync(client, Guid.NewGuid());
        var second = await PostVerifyAsync(client, Guid.NewGuid());
        var third = await PostVerifyAsync(client, Guid.NewGuid());
        var login = await PostLoginAsync(client);

        Assert.Multiple(() =>
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
        await AssertRateLimitedProblemAsync(third);
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private static ListingsApiFactory CreateFactory()
        => new(services =>
            services.Configure<AuthOptions>(options =>
            {
                options.RateLimitPerMinute = 2;
                options.VerifyRateLimitPerMinute = 2;
            }));

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client)
        => client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("limited@example.test", "Wrongpass123"),
            ApiJson.Options);

    private static Task<HttpResponseMessage> PostRegisterAsync(HttpClient client, string email)
        => client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "Validpass123", "Rate Limited", UserRoleWire.Student),
            ApiJson.Options);

    private static Task<HttpResponseMessage> PostVerifyAsync(HttpClient client, Guid challengeId)
        => client.PostAsJsonAsync(
            "/api/v1/auth/register/verify",
            new VerifyRegistrationRequest(challengeId, FixedOtpGenerator.DefaultCode),
            ApiJson.Options);

    private static async Task AssertRateLimitedProblemAsync(HttpResponseMessage response)
    {
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(response.Headers.Contains("Retry-After"), Is.True);
        });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(
            document.RootElement.GetProperty("code").GetString(),
            Is.EqualTo(AuthProblemFactory.RateLimited));
    }
}
