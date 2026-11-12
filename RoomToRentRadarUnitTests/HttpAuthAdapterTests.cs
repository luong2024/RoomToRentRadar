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

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
