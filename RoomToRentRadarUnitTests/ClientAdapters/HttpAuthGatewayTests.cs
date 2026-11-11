using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Ports.Contracts.Requests.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;

namespace RoomToRentRadarUnitTests.ClientAdapters;

[TestFixture]
public class HttpAuthGatewayTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Test]
    public async Task LoginAsync_ValidCredentials_ReturnsTokensDto()
    {
        // Arrange
        var expectedTokens = new AuthTokensDto(
            AccessToken: "jwt-access-token",
            RefreshToken: "refresh-token-123",
            AccessTokenExpiresAtUtc: DateTime.UtcNow.AddMinutes(30),
            RefreshTokenExpiresAtUtc: DateTime.UtcNow.AddDays(7),
            User: new UserProfileDto(
                Id: "user-123",
                Email: "test@ictu.edu.vn",
                FullName: "Nguyễn Văn A",
                Role: "Student",
                AuthProvider: "Local",
                PhoneNumber: null,
                StudentCode: "DTC123",
                IsVerified: true,
                CreatedAtUtc: DateTime.UtcNow
            )
        );

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.That(req.RequestUri!.AbsolutePath, Is.EqualTo("/api/auth/login"));
            Assert.That(req.Method, Is.EqualTo(HttpMethod.Post));

            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedTokens)
            };
            return res;
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var gateway = new HttpAuthGateway(client);

        // Act
        var result = await gateway.LoginAsync(new LoginRequest("test@ictu.edu.vn", "Password123!"));

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.AccessToken, Is.EqualTo("jwt-access-token"));
        Assert.That(result.User?.Email, Is.EqualTo("test@ictu.edu.vn"));
    }

    [Test]
    public void LoginAsync_InvalidCredentials_ThrowsApiExceptionWithErrorCode()
    {
        // Arrange
        var problemDetails = new
        {
            type = "https://httpstatuses.com/401",
            title = "Unauthorized",
            status = 401,
            detail = "Email hoặc mật khẩu không chính xác.",
            errorCode = "AUTH_INVALID_CREDENTIALS"
        };

        var handler = new MockHttpMessageHandler(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(problemDetails)
            };
            return res;
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var gateway = new HttpAuthGateway(client);

        // Act & Assert
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await gateway.LoginAsync(new LoginRequest("wrong@ictu.edu.vn", "WrongPassword!")));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(ex.ErrorCode, Is.EqualTo("AUTH_INVALID_CREDENTIALS"));
        Assert.That(ex.Message, Is.EqualTo("Email hoặc mật khẩu không chính xác."));
    }

    [Test]
    public async Task RegisterAsync_ValidRequest_ReturnsTokensDto()
    {
        // Arrange
        var expectedTokens = new AuthTokensDto(
            AccessToken: "new-access-token",
            RefreshToken: "new-refresh-token",
            AccessTokenExpiresAtUtc: DateTime.UtcNow.AddMinutes(30),
            RefreshTokenExpiresAtUtc: DateTime.UtcNow.AddDays(7),
            User: new UserProfileDto(
                Id: "user-new",
                Email: "student@ictu.edu.vn",
                FullName: "Lê Văn B",
                Role: "Student",
                AuthProvider: "Local",
                PhoneNumber: null,
                StudentCode: "DTC456",
                IsVerified: false,
                CreatedAtUtc: DateTime.UtcNow
            )
        );

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.That(req.RequestUri!.AbsolutePath, Is.EqualTo("/api/auth/register"));
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(expectedTokens)
            };
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var gateway = new HttpAuthGateway(client);

        // Act
        var result = await gateway.RegisterAsync(new RegisterUserRequest(
            Email: "student@ictu.edu.vn",
            Password: "Password123!",
            FullName: "Lê Văn B",
            Role: "Student",
            PhoneNumber: null,
            StudentCode: "DTC456"
        ));

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.AccessToken, Is.EqualTo("new-access-token"));
    }

    [Test]
    public void RegisterAsync_EmailAlreadyExists_ThrowsApiExceptionWith409()
    {
        // Arrange
        var problemDetails = new
        {
            type = "https://httpstatuses.com/409",
            title = "Conflict",
            status = 409,
            detail = "Email này đã được sử dụng.",
            errorCode = "USER_EMAIL_EXISTS"
        };

        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent.Create(problemDetails)
            };
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var gateway = new HttpAuthGateway(client);

        // Act & Assert
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await gateway.RegisterAsync(new RegisterUserRequest(
                Email: "existing@ictu.edu.vn",
                Password: "Password123!",
                FullName: "Đã Tồn Tại",
                Role: "Student",
                PhoneNumber: null,
                StudentCode: null
            )));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(ex.ErrorCode, Is.EqualTo("USER_EMAIL_EXISTS"));
    }

    [Test]
    public async Task RevokeTokenAsync_WithBearerToken_AttachesAuthorizationHeader()
    {
        // Arrange
        string? capturedAuthHeader = null;

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.That(req.RequestUri!.AbsolutePath, Is.EqualTo("/api/auth/revoke"));
            capturedAuthHeader = req.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var gateway = new HttpAuthGateway(client);

        // Act
        await gateway.RevokeTokenAsync(new RevokeTokenRequest("refresh-token-to-revoke"), bearerToken: "sample-jwt-token");

        // Assert
        Assert.That(capturedAuthHeader, Is.EqualTo("Bearer sample-jwt-token"));
    }
}
