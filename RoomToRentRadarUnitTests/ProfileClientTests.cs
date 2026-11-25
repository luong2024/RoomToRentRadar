using System.Net;
using System.Text;
using System.Text.Json;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Auth;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class ProfileClientTests
{
    private static readonly DateTime CreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetProfileAsync_UsesMeEndpointAndReadsProfile()
    {
        var userId = Guid.NewGuid();
        var handler = new StubHandler(request =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/api/v1/auth/me"));
            return Task.FromResult(JsonResponse(
                new UserProfileDto(userId, "student@example.test", "Minh Anh", "student", CreatedAt)));
        });

        var profile = await Client(handler).GetProfileAsync();

        Assert.Multiple(() =>
        {
            Assert.That(profile.UserId, Is.EqualTo(userId));
            Assert.That(profile.Email, Is.EqualTo("student@example.test"));
            Assert.That(profile.DisplayName, Is.EqualTo("Minh Anh"));
            Assert.That(profile.Role, Is.EqualTo("student"));
            Assert.That(profile.CreatedAt, Is.EqualTo(CreatedAt));
        });
    }

    [Test]
    public async Task UpdateDisplayNameAsync_PutsCamelCaseBodyAndReadsUpdatedProfile()
    {
        string? body = null;
        var handler = new StubHandler(async request =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Put));
            Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/api/v1/auth/me"));
            body = await request.Content!.ReadAsStringAsync();
            return JsonResponse(new UserProfileDto(
                Guid.NewGuid(), "student@example.test", "Tên Mới", "student", CreatedAt));
        });

        var profile = await Client(handler).UpdateDisplayNameAsync("Tên Mới");

        Assert.Multiple(() =>
        {
            using var document = JsonDocument.Parse(body!);
            Assert.That(document.RootElement.GetProperty("displayName").GetString(), Is.EqualTo("Tên Mới"));
            Assert.That(profile.DisplayName, Is.EqualTo("Tên Mới"));
        });
    }

    [Test]
    public void GetProfileAsync_Unauthorized_ThrowsApiExceptionWithCode()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                """{"title":"Sign in required","detail":"Authentication is required.","code":"unauthorized"}""",
                Encoding.UTF8,
                "application/problem+json")
        }));

        var exception = Assert.ThrowsAsync<ApiException>(() => Client(handler).GetProfileAsync())!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(exception.Code, Is.EqualTo("unauthorized"));
            Assert.That(exception.Message, Is.EqualTo("Authentication is required."));
        });
    }

    [Test]
    public void UpdateDisplayNameAsync_ValidationProblem_ThrowsInvalidDisplayName()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"title":"Invalid display name","detail":"display_name is required and must contain at most 120 characters.","code":"invalid_display_name"}""",
                Encoding.UTF8,
                "application/problem+json")
        }));

        var exception = Assert.ThrowsAsync<ApiException>(() => Client(handler).UpdateDisplayNameAsync(""))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(exception.Code, Is.EqualTo("invalid_display_name"));
        });
    }

    private static ProfileClient Client(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

    private static HttpResponseMessage JsonResponse<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value, ApiJson.Options), Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request);
    }
}
