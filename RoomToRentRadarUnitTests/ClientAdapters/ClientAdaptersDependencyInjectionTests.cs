using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Client.Adapters.Session;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadarUnitTests.ClientAdapters;

/// <summary>
/// T00.13 AC-4/AC-5 — AddClientAdapters registers the shared foundation and the gateway pipeline
/// (NetworkErrorHandler outermost, AuthTokenHandler inside) without any real network.
/// </summary>
[TestFixture]
public class ClientAdaptersDependencyInjectionTests
{
    private const string RefreshPath = "/api/auth/refresh";
    private const string MePath = "/api/auth/me";
    private const string LoginPath = "/api/auth/login";

    [Test]
    public void AddClientAdapters_ResolvesIAuthGatewayAndSharedSingletons()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(new InMemorySessionStore());
        services.AddClientAdapters(new ApiOptions());
        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IAuthGateway>(), Is.InstanceOf<HttpAuthGateway>());
        Assert.That(
            provider.GetRequiredService<TokenRefreshCoordinator>(),
            Is.SameAs(provider.GetRequiredService<TokenRefreshCoordinator>()));
        Assert.That(
            provider.GetRequiredService<ISessionEvents>(),
            Is.SameAs(provider.GetRequiredService<ISessionEvents>()));
        Assert.That(
            provider.GetRequiredService<ISessionEvents>(),
            Is.SameAs(provider.GetRequiredService<SessionEvents>()));
    }

    [Test]
    public void AddClientAdapters_InvalidOptions_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddClientAdapters(new ApiOptions
        {
            BaseUrl = new Uri($"{ApiOptions.LocalhostBaseUrl}/api")
        }));
        Assert.Throws<ArgumentException>(() => services.AddClientAdapters(new ApiOptions
        {
            Timeout = TimeSpan.Zero
        }));
        Assert.Throws<ArgumentNullException>(() => services.AddClientAdapters(null!));
        Assert.Throws<ArgumentNullException>(() => DependencyInjection.AddClientAdapters(null!, new ApiOptions()));
    }

    [Test]
    public async Task AuthGateway_Request_UsesBaseUrlAndAttachesBearerFromSessionStore()
    {
        var stub = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, Profile()));
        var store = new InMemorySessionStore();
        await store.SaveSessionAsync(Tokens("a1", "r1"));
        var options = new ApiOptions { BaseUrl = new Uri(ApiOptions.LocalhostBaseUrl) };
        using var provider = BuildProvider(stub, store, options);
        var gateway = provider.GetRequiredService<IAuthGateway>();

        var profile = await gateway.GetCurrentUserProfileAsync();

        Assert.That(profile.Email, Is.EqualTo("student@ictu.edu.vn"));
        var recorded = stub.Requests.Single();
        Assert.That(recorded.Uri, Is.EqualTo(new Uri($"{ApiOptions.LocalhostBaseUrl}{MePath}")));
        Assert.That(recorded.Authorization, Is.EqualTo("Bearer a1"));
        Assert.That(stub.CountForPath(RefreshPath), Is.EqualTo(0));
    }

    [Test]
    public void AuthGateway_NetworkDown_ThrowsApiExceptionNetworkError()
    {
        var stub = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var store = new InMemorySessionStore();
        store.SaveSessionAsync(Tokens("a1", "r1")).GetAwaiter().GetResult();
        using var provider = BuildProvider(stub, store);
        var gateway = provider.GetRequiredService<IAuthGateway>();

        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.GetCurrentUserProfileAsync());

        Assert.That(ex!.IsNetworkError, Is.True);
        Assert.That(ex.StatusCode, Is.EqualTo((HttpStatusCode)0));
        Assert.That(ex.InnerException, Is.InstanceOf<HttpRequestException>());
    }

    [Test]
    public void AuthGateway_Login401_IsNotRetriedViaRefresh()
    {
        var stub = new StubHttpMessageHandler(_ => Problem(HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS"));
        var store = new InMemorySessionStore();
        store.SaveSessionAsync(Tokens("a1", "r1")).GetAwaiter().GetResult();
        using var provider = BuildProvider(stub, store);
        var gateway = provider.GetRequiredService<IAuthGateway>();
        var sessionExpiredRaised = 0;
        provider.GetRequiredService<ISessionEvents>().SessionExpired += (_, _) => sessionExpiredRaised++;

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await gateway.LoginAsync(new LoginRequest("student@ictu.edu.vn", "wrong-password")));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(ex.ErrorCode, Is.EqualTo("INVALID_CREDENTIALS"));
        Assert.That(stub.CountForPath(LoginPath), Is.EqualTo(1));
        Assert.That(stub.CountForPath(RefreshPath), Is.EqualTo(0));
        Assert.That(store.GetRefreshTokenAsync().GetAwaiter().GetResult(), Is.EqualTo("r1"));
        Assert.That(sessionExpiredRaised, Is.EqualTo(0));
    }

    [Test]
    public void AuthGateway_ExplicitBearerOnMe_PassesThroughHandlerUntouched()
    {
        var stub = new StubHttpMessageHandler(_ => Problem(HttpStatusCode.Unauthorized, "UNAUTHORIZED"));
        var store = new InMemorySessionStore();
        store.SaveSessionAsync(Tokens("a1", "r1")).GetAwaiter().GetResult();
        using var provider = BuildProvider(stub, store);
        var gateway = provider.GetRequiredService<IAuthGateway>();

        var ex = Assert.ThrowsAsync<ApiException>(async () => await gateway.GetCurrentUserProfileAsync("explicit-token"));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(stub.Requests.Single().Authorization, Is.EqualTo("Bearer explicit-token"));
        Assert.That(stub.CountForPath(RefreshPath), Is.EqualTo(0));
    }

    [Test]
    public void UseMocks_DefaultFalse_AddClientAdapters_DoesNotRegisterMocks()
    {
        var options = new ApiOptions();
        Assert.That(options.UseMocks, Is.False);

        var services = new ServiceCollection();
        services.AddClientAdapters(options);
        Assert.That(
            services.Any(descriptor => descriptor.ServiceType == typeof(ISessionStore)),
            Is.False,
            "The app registers ISessionStore; AddClientAdapters must not sneak in a mock store.");

        services.AddSingleton<ISessionStore>(new InMemorySessionStore());
        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<IAuthGateway>(), Is.InstanceOf<HttpAuthGateway>());
    }

    private static ServiceProvider BuildProvider(StubHttpMessageHandler stub, InMemorySessionStore store, ApiOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(store);
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => stub));
        services.AddClientAdapters(options ?? new ApiOptions());
        return services.BuildServiceProvider();
    }

    private static UserProfileDto Profile() => new(
        "u1", "student@ictu.edu.vn", "Test Student", "Student", "Local", null, null, true, DateTime.UtcNow);

    private static AuthTokensDto Tokens(string access, string refresh) => new(
        access,
        refresh,
        DateTime.UtcNow.AddMinutes(30),
        DateTime.UtcNow.AddDays(7),
        Profile());

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, ApiResponseReader.JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage Problem(HttpStatusCode status, string errorCode) =>
        new(status)
        {
            Content = new StringContent(
                $$"""{"title":"Error","status":{{(int)status}},"errorCode":"{{errorCode}}"}""",
                Encoding.UTF8,
                "application/problem+json")
        };
}