using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Client.Adapters.Mocks;
using RoomToRentRadar.Client.Adapters.Session;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadarUnitTests.ClientAdapters;

[TestFixture]
public class AuthTokenHandlerTests
{
    private const string BaseUrl = "http://localhost:5000";
    private const string MainPath = "/api/rooms";
    private const string RefreshPath = "/api/auth/refresh";

    [Test]
    public async Task Send_200_AttachesBearerAndDoesNotRefresh()
    {
        var harness = new Harness(_ => Ok());
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));

        var response = await harness.Client.GetAsync(MainPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(harness.Stub.Requests.Single().Authorization, Is.EqualTo("Bearer a1"));
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(0));
    }

    [Test]
    public async Task Send_NoAccessToken_SendsWithoutAuthorizationHeader()
    {
        var harness = new Harness(_ => Ok());

        var response = await harness.Client.GetAsync(MainPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(harness.Stub.Requests.Single().Authorization, Is.Null);
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(0));
    }

    [Test]
    public async Task Send_401ThenRefreshOk_RetriesWithNewBearerAndReturns200()
    {
        var harness = new Harness(RefreshToA2);
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));

        var response = await harness.Client.GetAsync(MainPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(1));
        Assert.That(harness.Stub.RequestsForPath(RefreshPath).Single().BodyAsString, Is.EqualTo("""{"refreshToken":"r1"}"""));
        var mainSends = harness.Stub.RequestsForPath(MainPath);
        Assert.That(mainSends.Count, Is.EqualTo(2));
        Assert.That(mainSends[0].Authorization, Is.EqualTo("Bearer a1"));
        Assert.That(mainSends[1].Authorization, Is.EqualTo("Bearer a2"));
        Assert.That(await harness.Store.GetAccessTokenAsync(), Is.EqualTo("a2"));
        Assert.That(await harness.Store.GetRefreshTokenAsync(), Is.EqualTo("r2"));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
    }

    [Test]
    public async Task Send_401RefreshRejected401InvalidToken_ClearsSessionRaisesSessionExpiredOnceReturns401()
    {
        var harness = new Harness(req => req.RequestUri!.AbsolutePath == RefreshPath
            ? Problem(HttpStatusCode.Unauthorized, "INVALID_TOKEN")
            : Unauthorized());
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));

        var response = await harness.Client.GetAsync(MainPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await harness.Store.HasValidTokenAsync(), Is.False);
        Assert.That(await harness.Store.GetRefreshTokenAsync(), Is.Null);
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(1));
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(1));
        Assert.That(harness.Stub.CountForPath(MainPath), Is.EqualTo(1));
    }

    [TestCase(400)]
    [TestCase(403)]
    public async Task Send_401RefreshRejected400Or403_AlsoSignsOut(int statusCode)
    {
        var harness = new Harness(req => req.RequestUri!.AbsolutePath == RefreshPath
            ? Problem((HttpStatusCode)statusCode, "INVALID_TOKEN")
            : Unauthorized());
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));

        var response = await harness.Client.GetAsync(MainPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await harness.Store.HasValidTokenAsync(), Is.False);
        Assert.That(await harness.Store.GetRefreshTokenAsync(), Is.Null);
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(1));
    }

    [Test]
    public void Send_401RefreshReturns500_KeepsSessionAndThrowsApiException()
    {
        var harness = new Harness(req => req.RequestUri!.AbsolutePath == RefreshPath
            ? Problem(HttpStatusCode.InternalServerError, "INTERNAL_ERROR")
            : Unauthorized());
        harness.Store.SaveSessionAsync(Tokens("a1", "r1")).GetAwaiter().GetResult();

        var ex = Assert.ThrowsAsync<ApiException>(async () => await harness.Client.GetAsync(MainPath));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(harness.Store.GetAccessTokenAsync().GetAwaiter().GetResult(), Is.EqualTo("a1"));
        Assert.That(harness.Store.GetRefreshTokenAsync().GetAwaiter().GetResult(), Is.EqualTo("r1"));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
    }

    [TestCase(429)]
    [TestCase(408)]
    public void Send_401Refresh429Or408_KeepsSession(int statusCode)
    {
        var harness = new Harness(req => req.RequestUri!.AbsolutePath == RefreshPath
            ? Problem((HttpStatusCode)statusCode, "TEMPORARY")
            : Unauthorized());
        harness.Store.SaveSessionAsync(Tokens("a1", "r1")).GetAwaiter().GetResult();

        var ex = Assert.ThrowsAsync<ApiException>(async () => await harness.Client.GetAsync(MainPath));

        Assert.That(ex!.StatusCode, Is.EqualTo((HttpStatusCode)statusCode));
        Assert.That(harness.Store.GetRefreshTokenAsync().GetAwaiter().GetResult(), Is.EqualTo("r1"));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
    }

    [Test]
    public void Send_401RefreshNetworkFailure_KeepsSessionThrowsNetworkError()
    {
        var harness = new Harness(req => req.RequestUri!.AbsolutePath == RefreshPath
            ? throw new HttpRequestException("connection refused")
            : Unauthorized());
        harness.Store.SaveSessionAsync(Tokens("a1", "r1")).GetAwaiter().GetResult();

        var ex = Assert.ThrowsAsync<ApiException>(async () => await harness.Client.GetAsync(MainPath));

        Assert.That(ex!.IsNetworkError, Is.True);
        Assert.That(ex.StatusCode, Is.EqualTo((HttpStatusCode)0));
        Assert.That(harness.Store.GetRefreshTokenAsync().GetAwaiter().GetResult(), Is.EqualTo("r1"));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
    }

    [Test]
    public void Send_401Refresh200WithGarbageBody_KeepsSessionThrowsApiException()
    {
        var harness = new Harness(req => req.RequestUri!.AbsolutePath == RefreshPath
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("this is not json", Encoding.UTF8, "application/json")
            }
            : Unauthorized());
        harness.Store.SaveSessionAsync(Tokens("a1", "r1")).GetAwaiter().GetResult();

        var ex = Assert.ThrowsAsync<ApiException>(async () => await harness.Client.GetAsync(MainPath));

        Assert.That(ex, Is.Not.Null);
        Assert.That(harness.Store.GetRefreshTokenAsync().GetAwaiter().GetResult(), Is.EqualTo("r1"));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
    }

    [Test]
    [CancelAfter(15000)]
    public async Task Send_ConcurrentFiveRequestsAll401_ExactlyOneRefresh()
    {
        var refreshGate = NewGate();
        var refreshStarted = NewGate();
        var allFirstRequestsSent = NewGate();
        var mainCount = 0;
        var harness = new Harness(async (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == RefreshPath)
            {
                refreshStarted.TrySetResult();
                await refreshGate.Task;
                return TokensOk("a2", "r2");
            }

            if (Interlocked.Increment(ref mainCount) == 5)
            {
                allFirstRequestsSent.TrySetResult();
            }

            return HasBearer(req, "a2") ? Ok() : Unauthorized();
        });
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));

        var tasks = Enumerable.Range(0, 5).Select(_ => harness.Client.GetAsync(MainPath)).ToArray();
        await refreshStarted.Task;
        await allFirstRequestsSent.Task;
        refreshGate.SetResult();
        var responses = await Task.WhenAll(tasks);

        Assert.That(responses.Select(r => r.StatusCode), Is.All.EqualTo(HttpStatusCode.OK));
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(1));
        Assert.That(harness.Stub.RequestsForPath(MainPath).Count(r => r.Authorization == "Bearer a2"), Is.EqualTo(5));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
    }

    [Test]
    [CancelAfter(15000)]
    public async Task Send_ConcurrentFiveRequestsRefreshRejected_OneRefreshOneSessionExpiredAllReturn401()
    {
        var refreshGate = NewGate();
        var refreshStarted = NewGate();
        var allFirstRequestsSent = NewGate();
        var mainCount = 0;
        var harness = new Harness(async (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == RefreshPath)
            {
                refreshStarted.TrySetResult();
                await refreshGate.Task;
                return Problem(HttpStatusCode.Unauthorized, "INVALID_TOKEN");
            }

            if (Interlocked.Increment(ref mainCount) == 5)
            {
                allFirstRequestsSent.TrySetResult();
            }

            return Unauthorized();
        });
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));

        var tasks = Enumerable.Range(0, 5).Select(_ => harness.Client.GetAsync(MainPath)).ToArray();
        await refreshStarted.Task;
        await allFirstRequestsSent.Task;
        refreshGate.SetResult();
        var responses = await Task.WhenAll(tasks);

        Assert.That(responses.Select(r => r.StatusCode), Is.All.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(1));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(1));
        Assert.That(await harness.Store.HasValidTokenAsync(), Is.False);
    }

    [Test]
    [CancelAfter(15000)]
    public async Task Send_TwoHandlerInstancesSharingCoordinator_StillOneRefresh()
    {
        var refreshGate = NewGate();
        var refreshStarted = NewGate();
        var bothFirstRequestsSent = NewGate();
        var mainCount = 0;
        var harness = new Harness(async (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == RefreshPath)
            {
                refreshStarted.TrySetResult();
                await refreshGate.Task;
                return TokensOk("a2", "r2");
            }

            if (Interlocked.Increment(ref mainCount) == 2)
            {
                bothFirstRequestsSent.TrySetResult();
            }

            return HasBearer(req, "a2") ? Ok() : Unauthorized();
        });
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));
        using var secondClient = harness.CreateSecondClient();

        var taskA = harness.Client.GetAsync(MainPath);
        var taskB = secondClient.GetAsync(MainPath);
        await refreshStarted.Task;
        await bothFirstRequestsSent.Task;
        refreshGate.SetResult();
        var responses = await Task.WhenAll(taskA, taskB);

        Assert.That(responses.Select(r => r.StatusCode), Is.All.EqualTo(HttpStatusCode.OK));
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(1));
    }

    [Test]
    public async Task Send_RetryAlso401_ReturnsSecond401WithoutSecondRefreshOrSignOut()
    {
        var harness = new Harness(req => req.RequestUri!.AbsolutePath == RefreshPath
            ? TokensOk("a2", "r2")
            : Unauthorized());
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));

        var response = await harness.Client.GetAsync(MainPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(1));
        Assert.That(harness.Stub.CountForPath(MainPath), Is.EqualTo(2));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
        Assert.That(await harness.Store.GetRefreshTokenAsync(), Is.EqualTo("r2"));
    }

    [TestCase(400)]
    [TestCase(403)]
    [TestCase(404)]
    [TestCase(409)]
    [TestCase(412)]
    [TestCase(429)]
    [TestCase(500)]
    public async Task Send_NonUnauthorizedErrors_PassThroughUntouched(int statusCode)
    {
        var harness = new Harness(_ => new HttpResponseMessage((HttpStatusCode)statusCode));
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));

        var response = await harness.Client.GetAsync(MainPath);

        Assert.That((int)response.StatusCode, Is.EqualTo(statusCode));
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(0));
        Assert.That(harness.Stub.CountForPath(MainPath), Is.EqualTo(1));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
    }

    [Test]
    public async Task Send_PostWithJsonBody_RetryReplaysIdenticalBodyAndContentTypeAndHeaders()
    {
        var harness = new Harness(RefreshToA2);
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));
        using var request = new HttpRequestMessage(HttpMethod.Post, MainPath)
        {
            Content = JsonContent.Create(
                new { title = "Phòng trọ gần ICTU", monthlyRentVnd = 2500000 },
                options: ApiResponseReader.JsonOptions)
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"v1\""));
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("vi-VN"));
        ((IDictionary<string, object?>)request.Options)["test-key"] = "test-value";

        var response = await harness.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var sends = harness.Stub.RequestsForPath(MainPath);
        Assert.That(sends.Count, Is.EqualTo(2));
        Assert.That(sends[1].Body, Is.EqualTo(sends[0].Body));
        Assert.That(sends[1].ContentType, Is.EqualTo(sends[0].ContentType));
        Assert.That(sends[1].Headers["If-Match"], Is.EqualTo(sends[0].Headers["If-Match"]));
        Assert.That(sends[1].Headers["Accept-Language"], Is.EqualTo(sends[0].Headers["Accept-Language"]));
        Assert.That(sends[1].Options["test-key"], Is.EqualTo(sends[0].Options["test-key"]));
        Assert.That(sends[1].Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(sends[1].Uri, Is.EqualTo(sends[0].Uri));
        Assert.That(sends[1].Authorization, Is.EqualTo("Bearer a2"));
    }

    [TestCase("/api/auth/login")]
    [TestCase("/api/auth/register")]
    [TestCase("/api/auth/sso/ictu")]
    [TestCase("/api/auth/google")]
    [TestCase("/api/auth/refresh")]
    [TestCase("/api/auth/revoke")]
    public async Task Send_AuthEndpoints_BypassBearerAndRefresh(string path)
    {
        var harness = new Harness(_ => Unauthorized());
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));

        var response = await harness.Client.PostAsync(path, new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(harness.Stub.Requests.Single().Authorization, Is.Null);
        Assert.That(harness.Stub.Requests, Has.Count.EqualTo(1));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
    }

    [Test]
    public async Task Send_AuthMe_GoesThroughRefresh()
    {
        var harness = new Harness(req => req.RequestUri!.AbsolutePath == RefreshPath
            ? TokensOk("a2", "r2")
            : HasBearer(req, "a2") ? Ok() : Unauthorized());
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));

        var response = await harness.Client.GetAsync("/api/auth/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(1));
        var meSends = harness.Stub.RequestsForPath("/api/auth/me");
        Assert.That(meSends.Count, Is.EqualTo(2));
        Assert.That(meSends[0].Authorization, Is.EqualTo("Bearer a1"));
        Assert.That(meSends[1].Authorization, Is.EqualTo("Bearer a2"));
    }

    [Test]
    public async Task Send_ExplicitAuthorizationHeader_NotOverwrittenNoRefresh()
    {
        var harness = new Harness(_ => Unauthorized());
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));
        using var request = new HttpRequestMessage(HttpMethod.Get, MainPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "explicit-token");

        var response = await harness.Client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(harness.Stub.Requests.Single().Authorization, Is.EqualTo("Bearer explicit-token"));
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(0));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
    }

    [Test]
    public async Task Send_ColdStartNoAccessTokenButRefreshToken_401Refresh_Retry200()
    {
        var harness = new Harness(RefreshToA2);
        // ADR-005: access token lives in RAM only, so a restarted app has just the refresh token.
        await harness.Store.SaveSessionAsync(Tokens(null!, "r1"));

        var response = await harness.Client.GetAsync(MainPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var mainSends = harness.Stub.RequestsForPath(MainPath);
        Assert.That(mainSends[0].Authorization, Is.Null);
        Assert.That(mainSends[1].Authorization, Is.EqualTo("Bearer a2"));
        Assert.That(harness.Stub.RequestsForPath(RefreshPath).Single().BodyAsString, Is.EqualTo("""{"refreshToken":"r1"}"""));
        Assert.That(await harness.Store.GetAccessTokenAsync(), Is.EqualTo("a2"));
        Assert.That(await harness.Store.GetRefreshTokenAsync(), Is.EqualTo("r2"));
    }

    [Test]
    public async Task Send_NoRefreshToken401_ReturnsOriginal401NoRefreshNoEvent()
    {
        var harness = new Harness(_ => Unauthorized());

        var response = await harness.Client.GetAsync(MainPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(harness.Stub.Requests.Single().Authorization, Is.Null);
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(0));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
    }

    [Test]
    [CancelAfter(15000)]
    public async Task Send_CallerCancelsWhileWaitingForLock_ThrowsOperationCanceledAndOtherWaiterStillSucceeds()
    {
        var refreshGate = NewGate();
        var refreshStarted = NewGate();
        var secondFirstRequestSent = NewGate();
        var mainCount = 0;
        var harness = new Harness(async (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == RefreshPath)
            {
                refreshStarted.TrySetResult();
                await refreshGate.Task;
                return TokensOk("a2", "r2");
            }

            if (Interlocked.Increment(ref mainCount) == 2)
            {
                secondFirstRequestSent.TrySetResult();
            }

            return HasBearer(req, "a2") ? Ok() : Unauthorized();
        });
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));
        using var callerCts = new CancellationTokenSource();

        var taskA = harness.Client.GetAsync(MainPath);
        await refreshStarted.Task;
        var taskB = harness.Client.GetAsync(MainPath, callerCts.Token);
        await secondFirstRequestSent.Task;
        callerCts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await taskB);

        refreshGate.SetResult();
        var responseA = await taskA;

        Assert.That(responseA.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(1));
        Assert.That(await harness.Store.GetAccessTokenAsync(), Is.EqualTo("a2"));
    }

    [Test]
    [CancelAfter(15000)]
    public async Task Send_CallerCancelsDuringRefreshInFlight_RefreshStillCompletesAndSessionUpdated()
    {
        var refreshGate = NewGate();
        var refreshStarted = NewGate();
        var harness = new Harness(async (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == RefreshPath)
            {
                refreshStarted.TrySetResult();
                await refreshGate.Task;
                return TokensOk("a2", "r2");
            }

            return HasBearer(req, "a2") ? Ok() : Unauthorized();
        });
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));
        using var callerCts = new CancellationTokenSource();

        var task = harness.Client.GetAsync(MainPath, callerCts.Token);
        await refreshStarted.Task;
        callerCts.Cancel();
        refreshGate.SetResult();

        Assert.CatchAsync<OperationCanceledException>(async () => await task);

        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(1));
        Assert.That(await harness.Store.GetAccessTokenAsync(), Is.EqualTo("a2"));
        Assert.That(await harness.Store.GetRefreshTokenAsync(), Is.EqualTo("r2"));
    }

    [Test]
    [CancelAfter(15000)]
    public async Task Send_SignOutDuringRefresh_DoesNotResurrectSession()
    {
        var refreshGate = NewGate();
        var refreshStarted = NewGate();
        var harness = new Harness(async (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == RefreshPath)
            {
                refreshStarted.TrySetResult();
                await refreshGate.Task;
                return TokensOk("a2", "r2");
            }

            return HasBearer(req, "a2") ? Ok() : Unauthorized();
        });
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));

        var task = harness.Client.GetAsync(MainPath);
        await refreshStarted.Task;
        await harness.Store.ClearSessionAsync();
        refreshGate.SetResult();
        var response = await task;

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(await harness.Store.GetAccessTokenAsync(), Is.Null);
        Assert.That(await harness.Store.GetRefreshTokenAsync(), Is.Null);
        Assert.That(await harness.Store.HasValidTokenAsync(), Is.False);
        Assert.That(harness.Stub.CountForPath(RefreshPath), Is.EqualTo(1));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
    }

    [Test]
    public async Task Send_SaveSessionReturnsFalse_StillRetriesWithNewTokenNoSignOut()
    {
        var harness = new Harness(RefreshToA2);
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));
        harness.Store.SimulateSaveFailure = true;

        var response = await harness.Client.GetAsync(MainPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(harness.Stub.RequestsForPath(MainPath)[1].Authorization, Is.EqualTo("Bearer a2"));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(0));
        Assert.That(await harness.Store.GetRefreshTokenAsync(), Is.EqualTo("r1"));
    }

    [Test]
    public async Task Send_SessionExpiredListenerThrows_DoesNotBreakResponse()
    {
        var harness = new Harness(req => req.RequestUri!.AbsolutePath == RefreshPath
            ? Problem(HttpStatusCode.Unauthorized, "INVALID_TOKEN")
            : Unauthorized());
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));
        harness.Events.SessionExpired += (_, _) => throw new InvalidOperationException("listener boom");

        var response = await harness.Client.GetAsync(MainPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(harness.SessionExpiredRaised, Is.EqualTo(1));
    }

    [Test]
    public async Task Send_NeverLogsTokens()
    {
        var logger = new CapturingLogger<TokenRefreshCoordinator>();
        var harness = new Harness(
            req => req.RequestUri!.AbsolutePath == RefreshPath
                ? Problem(HttpStatusCode.Unauthorized, "INVALID_TOKEN")
                : Unauthorized(),
            logger);
        await harness.Store.SaveSessionAsync(Tokens("a1", "r1"));

        var response = await harness.Client.GetAsync(MainPath);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(logger.Messages, Is.Not.Empty);
        foreach (var message in logger.Messages)
        {
            Assert.That(message, Does.Not.Contain("a1"));
            Assert.That(message, Does.Not.Contain("r1"));
            Assert.That(message, Does.Not.Contain("a2"));
            Assert.That(message, Does.Not.Contain("r2"));
        }
    }

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static bool HasBearer(HttpRequestMessage request, string token) =>
        request.Headers.Authorization?.ToString() == $"Bearer {token}";

    private static Task<HttpResponseMessage> RefreshToA2(HttpRequestMessage request, CancellationToken _) =>
        Task.FromResult(request.RequestUri!.AbsolutePath == RefreshPath
            ? TokensOk("a2", "r2")
            : HasBearer(request, "a2") ? Ok() : Unauthorized());

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK);

    private static HttpResponseMessage Unauthorized() => new(HttpStatusCode.Unauthorized);

    private static AuthTokensDto Tokens(string access, string refresh) => new(
        access,
        refresh,
        DateTime.UtcNow.AddMinutes(30),
        DateTime.UtcNow.AddDays(7),
        new UserProfileDto("u1", "test@ictu.edu.vn", "Test User", "Student", "Local", null, null, true, DateTime.UtcNow));

    private static HttpResponseMessage TokensOk(string access, string refresh) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(Tokens(access, refresh), ApiResponseReader.JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage Problem(HttpStatusCode status, string errorCode) =>
        new(status)
        {
            Content = new StringContent(
                $$"""{"title":"Unauthorized","status":{{(int)status}},"errorCode":"{{errorCode}}"}""",
                Encoding.UTF8,
                "application/problem+json")
        };

    private sealed class Harness
    {
        public InMemorySessionStore Store { get; } = new();
        public StubHttpMessageHandler Stub { get; }
        public SessionEvents Events { get; } = new();
        public TokenRefreshCoordinator Coordinator { get; }
        public HttpClient Client { get; }
        public int SessionExpiredRaised { get; private set; }

        public Harness(Func<HttpRequestMessage, HttpResponseMessage> responder, ILogger<TokenRefreshCoordinator>? logger = null)
            : this((request, _) => Task.FromResult(responder(request)), logger)
        {
        }

        public Harness(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder, ILogger<TokenRefreshCoordinator>? logger = null)
        {
            Stub = new StubHttpMessageHandler(responder);
            Events.SessionExpired += (_, _) => SessionExpiredRaised++;
            var refreshClient = new HttpClient(Stub) { BaseAddress = new Uri(BaseUrl) };
            Coordinator = new TokenRefreshCoordinator(
                new StubHttpClientFactory(refreshClient), Store, Events, new ApiOptions(), logger);
            Client = new HttpClient(new AuthTokenHandler(Store, Coordinator) { InnerHandler = Stub })
            {
                BaseAddress = new Uri(BaseUrl)
            };
        }

        public HttpClient CreateSecondClient() =>
            new(new AuthTokenHandler(Store, Coordinator) { InnerHandler = Stub })
            {
                BaseAddress = new Uri(BaseUrl)
            };
    }
}