using System.Net;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Client.Adapters.Session;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadarUnitTests.ClientAdapters;

[TestFixture]
public class TokenRefreshCoordinatorTests
{
    private const string BaseUrl = "http://localhost:5000";

    [Test]
    public async Task RefreshAsync_StaleTokenDiffersFromStore_ReturnsCurrentTokenWithoutHttp()
    {
        var store = new InMemorySessionStore();
        await store.SaveSessionAsync(Tokens("a2", "r2"));
        var stub = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var coordinator = CreateCoordinator(store, stub, out var factory);

        var outcome = await coordinator.RefreshAsync("a1", CancellationToken.None);

        Assert.That(outcome.Kind, Is.EqualTo(RefreshOutcomeKind.Refreshed));
        Assert.That(outcome.AccessToken, Is.EqualTo("a2"));
        Assert.That(stub.CountForPath("/api/auth/refresh"), Is.EqualTo(0));
        Assert.That(factory.RequestedNames, Is.Empty);
    }

    [Test]
    public async Task RefreshAsync_SameToken_CallsRefreshOnceAndSavesSession()
    {
        var store = new InMemorySessionStore();
        await store.SaveSessionAsync(Tokens("a1", "r1"));
        var stub = new StubHttpMessageHandler(_ => TokensResponse("a2", "r2"));
        var coordinator = CreateCoordinator(store, stub, out _);

        var outcome = await coordinator.RefreshAsync("a1", CancellationToken.None);

        Assert.That(outcome.Kind, Is.EqualTo(RefreshOutcomeKind.Refreshed));
        Assert.That(outcome.AccessToken, Is.EqualTo("a2"));
        Assert.That(stub.CountForPath("/api/auth/refresh"), Is.EqualTo(1));
        var refreshRequest = stub.RequestsForPath("/api/auth/refresh").Single();
        Assert.That(refreshRequest.BodyAsString, Is.EqualTo("""{"refreshToken":"r1"}"""));
        Assert.That(await store.GetAccessTokenAsync(), Is.EqualTo("a2"));
        Assert.That(await store.GetRefreshTokenAsync(), Is.EqualTo("r2"));
    }

    [Test]
    public async Task RefreshAsync_NoRefreshToken_ReturnsNoSession()
    {
        var store = new InMemorySessionStore();
        var stub = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var coordinator = CreateCoordinator(store, stub, out _);

        var outcome = await coordinator.RefreshAsync("a1", CancellationToken.None);

        Assert.That(outcome.Kind, Is.EqualTo(RefreshOutcomeKind.NoSession));
        Assert.That(stub.CountForPath("/api/auth/refresh"), Is.EqualTo(0));
    }

    [Test]
    public async Task RefreshAsync_UsesDedicatedClientWithoutAuthTokenHandler()
    {
        var store = new InMemorySessionStore();
        await store.SaveSessionAsync(Tokens("a1", "r1"));
        var stub = new StubHttpMessageHandler(_ => TokensResponse("a2", "r2"));
        var coordinator = CreateCoordinator(store, stub, out var factory);

        var outcome = await coordinator.RefreshAsync("a1", CancellationToken.None);

        Assert.That(outcome.Kind, Is.EqualTo(RefreshOutcomeKind.Refreshed));
        Assert.That(factory.RequestedNames, Is.EqualTo(new[] { "RoomToRentRadar.Refresh" }));
        var refreshRequest = stub.RequestsForPath("/api/auth/refresh").Single();
        Assert.That(refreshRequest.Authorization, Is.Null);
    }

    [Test]
    public async Task RefreshAsync_Rejected_ClearsSessionBeforeRaisingEvent()
    {
        var store = new InMemorySessionStore();
        await store.SaveSessionAsync(Tokens("a1", "r1"));
        var stub = new StubHttpMessageHandler(_ => ProblemResponse(HttpStatusCode.Unauthorized, "INVALID_TOKEN"));
        var events = new SessionEvents();
        string? refreshTokenAtRaise = "not-raised";
        var raisedCount = 0;
        events.SessionExpired += (_, _) =>
        {
            raisedCount++;
            refreshTokenAtRaise = store.GetRefreshTokenAsync().GetAwaiter().GetResult();
        };
        var coordinator = CreateCoordinator(store, stub, out _, events);

        var outcome = await coordinator.RefreshAsync("a1", CancellationToken.None);

        Assert.That(outcome.Kind, Is.EqualTo(RefreshOutcomeKind.SignedOut));
        Assert.That(raisedCount, Is.EqualTo(1));
        Assert.That(refreshTokenAtRaise, Is.Null);
        Assert.That(await store.GetRefreshTokenAsync(), Is.Null);
        Assert.That(await store.HasValidTokenAsync(), Is.False);
    }

    private static TokenRefreshCoordinator CreateCoordinator(
        InMemorySessionStore store,
        StubHttpMessageHandler stub,
        out StubHttpClientFactory factory,
        ISessionEvents? events = null)
    {
        var refreshClient = new HttpClient(stub) { BaseAddress = new Uri(BaseUrl) };
        factory = new StubHttpClientFactory(refreshClient);
        return new TokenRefreshCoordinator(factory, store, events ?? new SessionEvents(), new ApiOptions());
    }

    private static AuthTokensDto Tokens(string access, string refresh) => new(
        access,
        refresh,
        DateTime.UtcNow.AddMinutes(30),
        DateTime.UtcNow.AddDays(7),
        new UserProfileDto("u1", "test@ictu.edu.vn", "Test User", "Student", "Local", null, null, true, DateTime.UtcNow));

    private static HttpResponseMessage TokensResponse(string access, string refresh) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(Tokens(access, refresh), ApiResponseReader.JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage ProblemResponse(HttpStatusCode status, string errorCode) =>
        new(status)
        {
            Content = new StringContent(
                $$"""{"title":"Unauthorized","status":{{(int)status}},"errorCode":"{{errorCode}}"}""",
                Encoding.UTF8,
                "application/problem+json")
        };
}