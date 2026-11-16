using System.Net;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Infrastructure.Adapters;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class HttpAuthAdapterTests
{
    [Test]
    public async Task LoginWithGoogleAsync_WhenRoleIsNull_ShouldNotDefaultToStudentAndShouldDetectRoleError()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            var problemJson = """
                {
                  "status": 400,
                  "title": "Validation Failed",
                  "detail": "Role (STUDENT or LANDLORD) is required when registering with Google for the first time.",
                  "errors": {
                    "role": ["Role (STUDENT or LANDLORD) is required when registering with Google for the first time."]
                  }
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(problemJson, Encoding.UTF8, "application/json")
            };
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.LoginWithGoogleAsync(new LoginWithGoogleRequest("google-token-123"));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.RequiresRoleSelection, Is.True);
        Assert.That(result.PendingGoogleIdToken, Is.EqualTo("google-token-123"));

        Assert.That(capturedBody, Is.Not.Null);
        using var doc = JsonDocument.Parse(capturedBody!);
        Assert.That(doc.RootElement.GetProperty("role").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task LoginWithEmailAsync_WhenUserIdIsMissing_ShouldReturnFailedInsteadOfGeneratingGuid()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var json = """
                {
                  "email": "student@example.com",
                  "role": "STUDENT",
                  "accessToken": "jwt-token",
                  "refreshToken": "refresh-token"
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.LoginWithEmailAsync(new LoginWithEmailRequest("student@example.com", "Password123"));

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.User, Is.Null);
    }

    [Test]
    public async Task LoginWithEmailAsync_WhenResponseIsValidFlatSessionDto_ShouldReturnSuccessWithExactUserId()
    {
        var expectedId = Guid.Parse("5a04530b-cf6b-4c90-a647-34b402b31fc8");
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var json = $$"""
                {
                  "userId": "{{expectedId}}",
                  "email": "student@example.com",
                  "role": "STUDENT",
                  "accessToken": "jwt-token",
                  "refreshToken": "refresh-token"
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.LoginWithEmailAsync(new LoginWithEmailRequest("student@example.com", "Password123"));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.User, Is.Not.Null);
        Assert.That(result.User!.Id, Is.EqualTo(expectedId));
        Assert.That(result.AccessToken, Is.EqualTo("jwt-token"));
        Assert.That(result.RefreshToken, Is.EqualTo("refresh-token"));
    }

    [Test]
    public void LoginWithEmailAsync_WhenCanceled_ShouldPropagateOperationCanceledException()
    {
        var handler = new StubHttpMessageHandler((_, ct) =>
            Task.FromCanceled<HttpResponseMessage>(ct));

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await adapter.LoginWithEmailAsync(new LoginWithEmailRequest("student@example.com", "Password123"), cts.Token));
    }

    [Test]
    public async Task LoginWithEmailAsync_WhenRequestTimesOut_ShouldReturnFriendlyTimeoutFailure()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 15 seconds elapsing."));

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.LoginWithEmailAsync(
            new LoginWithEmailRequest("student@example.com", "Password123"),
            CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("timed out").IgnoreCase);
    }

    [Test]
    public async Task RefreshTokenAsync_WhenSuccessful_ShouldReturnSuccessWithNewTokensAndUser()
    {
        var userId = Guid.NewGuid();
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var json = $$"""
                {
                  "userId": "{{userId}}",
                  "email": "student@example.com",
                  "role": "STUDENT",
                  "accessToken": "new-access-token",
                  "refreshToken": "rotated-refresh-token"
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.RefreshTokenAsync("old-refresh-token");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.AccessToken, Is.EqualTo("new-access-token"));
        Assert.That(result.RefreshToken, Is.EqualTo("rotated-refresh-token"));
        Assert.That(result.User?.Id, Is.EqualTo(userId));
    }

    [Test]
    public async Task RefreshTokenAsync_WhenFails_ShouldReturnFailureMessage()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var problemJson = """
                {
                  "status": 401,
                  "title": "Invalid Refresh Token",
                  "detail": "Refresh token has expired."
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(problemJson, Encoding.UTF8, "application/json")
            });
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.RefreshTokenAsync("expired-refresh-token");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("Refresh token has expired."));
        Assert.That(result.FailureType, Is.EqualTo(AuthFailureType.RefreshTokenExpiredOrRevoked));
        Assert.That(result.IsTransientError, Is.False);
    }

    [Test]
    public async Task RefreshTokenAsync_WhenBadRequest400_ShouldReturnRefreshTokenExpiredOrRevoked()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var problemJson = """
                {
                  "status": 400,
                  "title": "Bad Request",
                  "detail": "invalid_grant: The refresh token has already been rotated or revoked."
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(problemJson, Encoding.UTF8, "application/json")
            });
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.RefreshTokenAsync("revoked-token");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.FailureType, Is.EqualTo(AuthFailureType.RefreshTokenExpiredOrRevoked));
        Assert.That(result.IsTransientError, Is.False);
    }

    [Test]
    public async Task RefreshTokenAsync_WhenBadRequest400ValidationFailed_ShouldReturnBadRequestFailureAndNotRefreshTokenExpiredOrRevoked()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var problemJson = """
                {
                  "status": 400,
                  "title": "Validation Failed",
                  "detail": "Refresh token is required.",
                  "errors": {
                    "refreshToken": ["Refresh token is required."]
                  }
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(problemJson, Encoding.UTF8, "application/json")
            });
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.RefreshTokenAsync(string.Empty);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.FailureType, Is.EqualTo(AuthFailureType.BadRequest));
        Assert.That(result.FailureType, Is.Not.EqualTo(AuthFailureType.RefreshTokenExpiredOrRevoked));
        Assert.That(result.ErrorMessage, Is.EqualTo("Refresh token is required."));
    }

    [Test]
    public async Task RefreshTokenAsync_WhenForbidden403_ShouldReturnForbiddenFailure()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var problemJson = """
                {
                  "status": 403,
                  "title": "Forbidden",
                  "detail": "Account does not have permission to refresh this session."
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(problemJson, Encoding.UTF8, "application/json")
            });
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.RefreshTokenAsync("some-refresh-token");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.FailureType, Is.EqualTo(AuthFailureType.Forbidden));
        Assert.That(result.FailureType, Is.Not.EqualTo(AuthFailureType.RefreshTokenExpiredOrRevoked));
    }

    [Test]
    public async Task RefreshTokenAsync_WhenOAuthErrorInvalidGrant_ShouldReturnRefreshTokenExpiredOrRevoked()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var oauthErrorJson = """
                {
                  "error": "invalid_grant",
                  "error_description": "The refresh token is expired, revoked, or malformed."
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(oauthErrorJson, Encoding.UTF8, "application/json")
            });
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.RefreshTokenAsync("expired-or-revoked-token");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.FailureType, Is.EqualTo(AuthFailureType.RefreshTokenExpiredOrRevoked));
        Assert.That(result.ErrorMessage, Does.Contain("The refresh token is expired, revoked, or malformed."));
    }

    [Test]
    public async Task RefreshTokenAsync_WhenNetworkFails_ShouldReturnNetworkFailure()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new HttpRequestException("Failed to connect to backend"));

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.RefreshTokenAsync("some-token");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.FailureType, Is.EqualTo(AuthFailureType.NetworkError));
        Assert.That(result.IsTransientError, Is.True);
    }

    [Test]
    public async Task RefreshTokenAsync_WhenTimeoutOccurs_ShouldReturnTimeoutFailure()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new TaskCanceledException("The request was canceled due to HttpClient.Timeout."));

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.RefreshTokenAsync("some-token", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.FailureType, Is.EqualTo(AuthFailureType.Timeout));
        Assert.That(result.IsTransientError, Is.True);
    }

    [Test]
    public async Task RefreshTokenAsync_WhenServer500Error_ShouldReturnServerFailure()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Server Error", Encoding.UTF8, "text/plain")
            }));

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.RefreshTokenAsync("some-token");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.FailureType, Is.EqualTo(AuthFailureType.ServerError));
        Assert.That(result.IsTransientError, Is.True);
    }

    [Test]
    public async Task LogoutAsync_WhenSuccessful_ShouldReturnTrue()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
        var adapter = new HttpAuthAdapter(client);

        var result = await adapter.LogoutAsync("valid-token");

        Assert.That(result, Is.True);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
