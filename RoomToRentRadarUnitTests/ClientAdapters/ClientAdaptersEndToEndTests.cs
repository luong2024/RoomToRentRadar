using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadarUnitTests.ClientAdapters;

/// <summary>
/// T00.13 AC-2 — single-flight refresh against the real API in-process (WebApplicationFactory),
/// because the emulator UI cannot trigger refresh yet (no authenticated call after login).
/// The server revokes the whole token chain on refresh-token reuse, so a double refresh would
/// sign the user out — these tests prove exactly one rotation with the real server.
/// Factory configuration mirrors RoomsEndpointIntegrationTests (T00.12 sẽ gom).
/// </summary>
[TestFixture]
public class ClientAdaptersEndToEndTests
{
    private const string JwtSigningKey = "integration-test-signing-key-at-least-32-bytes-long!!";
    private const string Email = "e2e-client@ictu.edu.vn";
    private const string Password = "StrongPass123";
    private const string CorruptedAccessToken = "expired-or-corrupted-access-token";

    private WebApplicationFactory<Program> _factory = null!;
    private InMemorySessionStore _store = null!;
    private ServiceProvider _provider = null!;
    private IAuthGateway _gateway = null!;
    private int _refreshCalls;
    private int _sessionExpiredRaised;

    [SetUp]
    public void SetUp()
    {
        _factory = CreateFactory();
        _store = new InMemorySessionStore();
        _refreshCalls = 0;
        _sessionExpiredRaised = 0;

        var services = new ServiceCollection();
        services.AddSingleton<ISessionStore>(_store);
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() =>
                new RefreshCountingHandler(_factory.Server.CreateHandler(), () => Interlocked.Increment(ref _refreshCalls))));
        services.AddClientAdapters(new ApiOptions { BaseUrl = new Uri("http://localhost") });
        _provider = services.BuildServiceProvider();
        _gateway = _provider.GetRequiredService<IAuthGateway>();
        _provider.GetRequiredService<ISessionEvents>().SessionExpired += (_, _) => Interlocked.Increment(ref _sessionExpiredRaised);
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();
        _factory.Dispose();
    }

    [Test]
    [CancelAfter(30000)]
    public async Task Login_Then_ExpiredAccessToken_AuthMeRefreshesWithRealServerAndSucceeds()
    {
        var tokens = await RegisterAndLoginAsync();
        await _store.SaveSessionAsync(tokens with { AccessToken = CorruptedAccessToken });

        var profile = await _gateway.GetCurrentUserProfileAsync();

        Assert.That(profile.Email, Is.EqualTo(Email));
        Assert.That(_refreshCalls, Is.EqualTo(1));
        Assert.That(await _store.GetRefreshTokenAsync(), Is.Not.EqualTo(tokens.RefreshToken), "Refresh must rotate the token");
        Assert.That(await _store.GetAccessTokenAsync(), Is.Not.EqualTo(CorruptedAccessToken));
        Assert.That(_sessionExpiredRaised, Is.EqualTo(0));

        // The old refresh token is now dead: reuse detection rejects it (and revokes the chain).
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await _gateway.RefreshTokenAsync(new RefreshTokenRequest(tokens.RefreshToken)));
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(ex.ErrorCode, Is.EqualTo("INVALID_TOKEN"));
    }

    [Test]
    [CancelAfter(30000)]
    public async Task ThreeConcurrentCallsWithBadAccessToken_AllSucceedAndRotateExactlyOnce()
    {
        var tokens = await RegisterAndLoginAsync();
        await _store.SaveSessionAsync(tokens with { AccessToken = CorruptedAccessToken });

        var profiles = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => _gateway.GetCurrentUserProfileAsync()));

        Assert.That(profiles.Select(p => p.Email), Is.All.EqualTo(Email));
        Assert.That(_refreshCalls, Is.EqualTo(1), "A second refresh would reuse the rotated token and the server would revoke the chain");
        Assert.That(_sessionExpiredRaised, Is.EqualTo(0));

        // Exactly one rotation: the refresh token now in the store is still accepted by the server.
        var currentRefreshToken = await _store.GetRefreshTokenAsync();
        Assert.That(currentRefreshToken, Is.Not.EqualTo(tokens.RefreshToken));
        var rotated = await _gateway.RefreshTokenAsync(new RefreshTokenRequest(currentRefreshToken!));
        Assert.That(rotated.AccessToken, Is.Not.Empty);
    }

    [Test]
    [CancelAfter(30000)]
    public async Task RevokedRefreshToken_AuthMe_SignsOutAndRaisesSessionExpired()
    {
        var tokens = await RegisterAndLoginAsync();
        await _gateway.RevokeTokenAsync(new RevokeTokenRequest(tokens.RefreshToken), tokens.AccessToken);
        await _store.SaveSessionAsync(tokens with { AccessToken = CorruptedAccessToken });

        var ex = Assert.ThrowsAsync<ApiException>(async () => await _gateway.GetCurrentUserProfileAsync());

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(_refreshCalls, Is.EqualTo(1));
        Assert.That(_sessionExpiredRaised, Is.EqualTo(1));
        Assert.That(await _store.HasValidTokenAsync(), Is.False);
        Assert.That(await _store.GetAccessTokenAsync(), Is.Null);
        Assert.That(await _store.GetRefreshTokenAsync(), Is.Null);
    }

    [Test]
    [CancelAfter(30000)]
    public async Task WrongPasswordLogin_ThrowsInvalidCredentialsAndNeverCallsRefresh()
    {
        var tokens = await RegisterAndLoginAsync();

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await _gateway.LoginAsync(new LoginRequest(Email, "WrongPass456")));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(ex.ErrorCode, Is.EqualTo("INVALID_CREDENTIALS"));
        Assert.That(_refreshCalls, Is.EqualTo(0));
        Assert.That(_sessionExpiredRaised, Is.EqualTo(0));
        Assert.That(await _store.GetRefreshTokenAsync(), Is.EqualTo(tokens.RefreshToken));
    }

    private async Task<AuthTokensDto> RegisterAndLoginAsync()
    {
        await _gateway.RegisterAsync(new RegisterUserRequest(Email, Password, "E2E Client", "Student"));
        var tokens = await _gateway.LoginAsync(new LoginRequest(Email, Password));
        await _store.SaveSessionAsync(tokens);
        return tokens;
    }

    // Same UseSetting pattern as RoomsEndpointIntegrationTests: host configuration reaches builder.Configuration.
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.UseSetting("DatabaseProvider", "Sqlite");
            webHost.UseSetting("Sqlite:ConnectionString", "Data Source=:memory:");
            webHost.UseSetting("Sqlite:SeedDemoRooms", "false");
            webHost.UseSetting("Jwt:SigningKey", JwtSigningKey);
            webHost.UseSetting("Jwt:Issuer", "RoomToRentRadar.Api");
            webHost.UseSetting("Jwt:Audience", "RoomToRentRadar.Client");
        });

    private sealed class RefreshCountingHandler : DelegatingHandler
    {
        private readonly Action _onRefresh;

        public RefreshCountingHandler(HttpMessageHandler innerHandler, Action onRefresh)
            : base(innerHandler)
        {
            _onRefresh = onRefresh;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/api/auth/refresh")
            {
                _onRefresh();
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}