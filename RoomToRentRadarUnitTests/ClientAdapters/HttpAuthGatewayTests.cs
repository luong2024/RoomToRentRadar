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
    public async Task LoginAsync_InvalidCredentials_ThrowsApiExceptionWithErrorCode()
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
        await Task.CompletedTask;

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(ex.ErrorCode, Is.EqualTo("AUTH_INVALID_CREDENTIALS"));
        Assert.That(ex.Message, Is.EqualTo("Email hoặc mật khẩu không chính xác."));
    }

    [Test]
    public async Task LoginAsync_ProblemDetailsWithExtensionsErrorCode_ParsesErrorCodeCorrectly()
    {
        // Arrange
        var rawJson = "{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.2\",\"title\":\"Authentication Failed\",\"status\":401,\"detail\":\"Invalid email or password.\",\"instance\":\"/api/auth/login\",\"extensions\":{\"errorCode\":\"AUTH_INVALID_CREDENTIALS\"}}";

        var handler = new MockHttpMessageHandler(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(rawJson, System.Text.Encoding.UTF8, "application/problem+json")
            };
            return res;
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var gateway = new HttpAuthGateway(client);

        // Act & Assert
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await gateway.LoginAsync(new LoginRequest("test@ictu.edu.vn", "WrongPassword!")));
        await Task.CompletedTask;

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.ErrorCode, Is.EqualTo("AUTH_INVALID_CREDENTIALS"));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
    public async Task RegisterAsync_EmailAlreadyExists_ThrowsApiExceptionWith409()
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
        await Task.CompletedTask;

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

    [Test]
    public async Task LoginWithIctuSsoAsync_ValidToken_ReturnsTokensDto()
    {
        // Arrange
        var expectedTokens = new AuthTokensDto(
            AccessToken: "sso-access-token",
            RefreshToken: "sso-refresh-token",
            AccessTokenExpiresAtUtc: DateTime.UtcNow.AddMinutes(30),
            RefreshTokenExpiresAtUtc: DateTime.UtcNow.AddDays(7),
            User: new UserProfileDto(
                Id: "sso-user-1",
                Email: "sso@ictu.edu.vn",
                FullName: "SSO User",
                Role: "Student",
                AuthProvider: "Google",
                PhoneNumber: null,
                StudentCode: "DTC999",
                IsVerified: true,
                CreatedAtUtc: DateTime.UtcNow
            )
        );

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.That(req.RequestUri!.AbsolutePath, Is.EqualTo("/api/auth/sso/ictu"));
            Assert.That(req.Method, Is.EqualTo(HttpMethod.Post));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedTokens)
            };
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var gateway = new HttpAuthGateway(client);

        // Act
        var result = await gateway.LoginWithIctuSsoAsync(new IctuSsoLoginRequest("google-id-token", "Pixel 8"));

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.AccessToken, Is.EqualTo("sso-access-token"));
        Assert.That(result.User.AuthProvider, Is.EqualTo("Google"));
    }

    [Test]
    public async Task LoginWithIctuSsoAsync_InvalidToken_ThrowsApiException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(req =>
        {
            var problem = new
            {
                type = "https://httpstatuses.com/401",
                title = "Unauthorized",
                status = 401,
                detail = "Invalid Google ID token.",
                errorCode = "INVALID_SSO_TOKEN"
            };
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(problem)
            };
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var gateway = new HttpAuthGateway(client);

        // Act & Assert
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await gateway.LoginWithIctuSsoAsync(new IctuSsoLoginRequest("invalid-token", "Device")));
        await Task.CompletedTask;

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(ex.ErrorCode, Is.EqualTo("INVALID_SSO_TOKEN"));
    }

    [Test]
    public async Task RefreshTokenAsync_ValidRefreshToken_ReturnsNewTokensDto()
    {
        // Arrange
        var expectedTokens = new AuthTokensDto(
            AccessToken: "new-access-token",
            RefreshToken: "rotated-refresh-token",
            AccessTokenExpiresAtUtc: DateTime.UtcNow.AddMinutes(30),
            RefreshTokenExpiresAtUtc: DateTime.UtcNow.AddDays(7),
            User: new UserProfileDto(
                Id: "user-1",
                Email: "student@ictu.edu.vn",
                FullName: "Student",
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
            Assert.That(req.RequestUri!.AbsolutePath, Is.EqualTo("/api/auth/refresh"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedTokens)
            };
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var gateway = new HttpAuthGateway(client);

        // Act
        var result = await gateway.RefreshTokenAsync(new RefreshTokenRequest("valid-refresh-token"));

        // Assert
        Assert.That(result.AccessToken, Is.EqualTo("new-access-token"));
        Assert.That(result.RefreshToken, Is.EqualTo("rotated-refresh-token"));
    }

    [Test]
    public async Task RefreshTokenAsync_RevokedOrExpiredToken_ThrowsApiException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(req =>
        {
            var problem = new
            {
                type = "https://httpstatuses.com/401",
                title = "Unauthorized",
                status = 401,
                detail = "Refresh token has expired or was revoked.",
                errorCode = "REFRESH_TOKEN_EXPIRED"
            };
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(problem)
            };
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var gateway = new HttpAuthGateway(client);

        // Act & Assert
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await gateway.RefreshTokenAsync(new RefreshTokenRequest("expired-token")));
        await Task.CompletedTask;

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(ex.ErrorCode, Is.EqualTo("REFRESH_TOKEN_EXPIRED"));
    }

    [Test]
    public async Task GetCurrentUserProfileAsync_WithBearerToken_ReturnsUserProfileDto()
    {
        // Arrange
        var expectedProfile = new UserProfileDto(
            Id: "user-me-1",
            Email: "me@ictu.edu.vn",
            FullName: "Me Myself",
            Role: "Landlord",
            AuthProvider: "Local",
            PhoneNumber: "0912345678",
            StudentCode: null,
            IsVerified: true,
            CreatedAtUtc: DateTime.UtcNow
        );

        string? capturedAuth = null;
        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.That(req.RequestUri!.AbsolutePath, Is.EqualTo("/api/auth/me"));
            capturedAuth = req.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedProfile)
            };
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var gateway = new HttpAuthGateway(client);

        // Act
        var result = await gateway.GetCurrentUserProfileAsync("my-jwt-token");

        // Assert
        Assert.That(capturedAuth, Is.EqualTo("Bearer my-jwt-token"));
        Assert.That(result.Email, Is.EqualTo("me@ictu.edu.vn"));
        Assert.That(result.Role, Is.EqualTo("Landlord"));
    }

    [Test]
    public async Task GetCurrentUserProfileAsync_Unauthorized_ThrowsApiException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var gateway = new HttpAuthGateway(client);

        // Act & Assert
        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await gateway.GetCurrentUserProfileAsync("expired-or-missing-token"));
        await Task.CompletedTask;

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
