using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Http;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;

namespace RoomToRentRadarUnitTests.ClientAdapters;

[TestFixture]
public class ApiResponseReaderTests
{
    [Test]
    public async Task ReadAsync_Success200_DeserializesCamelCaseDto()
    {
        const string json = """
            {
              "accessToken": "a1",
              "refreshToken": "r1",
              "accessTokenExpiresAtUtc": "2026-11-16T10:00:00Z",
              "refreshTokenExpiresAtUtc": "2026-11-23T10:00:00Z",
              "user": {
                "id": "u1",
                "email": "test@ictu.edu.vn",
                "fullName": "Nguyễn Văn A",
                "role": "Student",
                "authProvider": "Local",
                "phoneNumber": null,
                "studentCode": "DTC123",
                "isVerified": true,
                "createdAtUtc": "2026-11-01T00:00:00Z"
              }
            }
            """;
        using var response = JsonResponse(HttpStatusCode.OK, json);

        var result = await ApiResponseReader.ReadAsync<AuthTokensDto>(response);

        Assert.That(result.AccessToken, Is.EqualTo("a1"));
        Assert.That(result.RefreshToken, Is.EqualTo("r1"));
        Assert.That(result.AccessTokenExpiresAtUtc, Is.EqualTo(new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc)));
        Assert.That(result.User.Email, Is.EqualTo("test@ictu.edu.vn"));
        Assert.That(result.User.FullName, Is.EqualTo("Nguyễn Văn A"));
        Assert.That(result.User.IsVerified, Is.True);
    }

    [Test]
    public async Task ReadAsync_EnumsAsStrings_Deserialize()
    {
        const string json = """
            {
              "id": "room-1",
              "title": "Phòng trọ gần ICTU",
              "monthlyRentVnd": 2500000,
              "areaM2": 25.5,
              "roomType": "MiniApartment",
              "distanceKm": 0.8,
              "isVerified": true,
              "status": "Available",
              "coverThumbUrl": null,
              "latitude": 21.5851,
              "longitude": 105.8066,
              "publishedAtUtc": "2026-11-10T00:00:00Z",
              "isFavourite": false
            }
            """;
        using var response = JsonResponse(HttpStatusCode.OK, json);

        var result = await ApiResponseReader.ReadAsync<RoomCardDto>(response);

        Assert.That(result.Status, Is.EqualTo(ListingStatus.Available));
        Assert.That(result.RoomType, Is.EqualTo(RoomType.MiniApartment));
        Assert.That(result.MonthlyRentVnd, Is.EqualTo(2500000));
        Assert.That(result.Title, Is.EqualTo("Phòng trọ gần ICTU"));
    }

    [Test]
    public void ReadAsync_ProblemJson_ThrowsApiExceptionWithStatusErrorCodeAndErrors()
    {
        const string json = """
            {
              "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
              "title": "Validation Failed",
              "status": 400,
              "errorCode": "VALIDATION_FAILED",
              "errors": { "minRentVnd": ["x"] }
            }
            """;
        using var response = ProblemResponse(HttpStatusCode.BadRequest, json);

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await ApiResponseReader.ReadAsync<AuthTokensDto>(response));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorCode, Is.EqualTo("VALIDATION_FAILED"));
        Assert.That(ex.ValidationErrors, Is.Not.Null);
        Assert.That(ex.ValidationErrors!["minRentVnd"], Is.EqualTo(new[] { "x" }));
    }

    [Test]
    public void ReadAsync_LegacyExtensionsErrorCode_ParsesErrorCode()
    {
        const string json = """
            {
              "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
              "title": "Authentication Failed",
              "status": 401,
              "detail": "Invalid email or password.",
              "extensions": { "errorCode": "INVALID_CREDENTIALS" }
            }
            """;
        using var response = ProblemResponse(HttpStatusCode.Unauthorized, json);

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await ApiResponseReader.ReadAsync<AuthTokensDto>(response));

        Assert.That(ex!.ErrorCode, Is.EqualTo("INVALID_CREDENTIALS"));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(ex.Message, Is.EqualTo("Invalid email or password."));
    }

    [TestCase("""{"detail":"D","title":"T","errorCode":"E"}""", "D")]
    [TestCase("""{"title":"T","errorCode":"E"}""", "T")]
    [TestCase("""{"errorCode":"E"}""", "E")]
    [TestCase("{}", "HTTP error 500 (Internal Server Error)")]
    public void ReadAsync_MessageFallsBackDetailThenTitleThenErrorCodeThenReason(string body, string expectedMessage)
    {
        using var response = ProblemResponse(HttpStatusCode.InternalServerError, body, "Internal Server Error");

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await ApiResponseReader.ReadAsync<AuthTokensDto>(response));

        Assert.That(ex!.Message, Is.EqualTo(expectedMessage));
    }

    [Test]
    public void ReadAsync_NonJsonBody502Html_ThrowsApiExceptionWithNullErrorCode()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            ReasonPhrase = "Bad Gateway",
            Content = new StringContent("<html><body>502 Bad Gateway</body></html>", Encoding.UTF8, "text/html")
        };

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await ApiResponseReader.ReadAsync<AuthTokensDto>(response));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        Assert.That(ex.ErrorCode, Is.Null);
        Assert.That(ex.Message, Is.EqualTo("HTTP error 502 (Bad Gateway)"));
    }

    [Test]
    public void ReadAsync_EmptyBodyOnError_ThrowsApiException()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await ApiResponseReader.ReadAsync<AuthTokensDto>(response));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(ex.ErrorCode, Is.Null);
    }

    [Test]
    public void ReadAsync_MalformedJsonWithProblemContentType_DoesNotThrowJsonException()
    {
        using var response = ProblemResponse(HttpStatusCode.BadRequest, "{ this is not valid json");

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await ApiResponseReader.ReadAsync<AuthTokensDto>(response));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorCode, Is.Null);
    }

    [Test]
    public void ReadAsync_ErrorCodeNotAString_IgnoredNotThrown()
    {
        const string json = """{"errorCode":123,"detail":"Something went wrong."}""";
        using var response = ProblemResponse(HttpStatusCode.BadRequest, json);

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await ApiResponseReader.ReadAsync<AuthTokensDto>(response));

        Assert.That(ex!.ErrorCode, Is.Null);
        Assert.That(ex.Message, Is.EqualTo("Something went wrong."));
    }

    [Test]
    public void ReadAsync_ErrorsWithNonArrayValues_SkipsThoseKeys()
    {
        const string json = """
            {"errorCode":"VALIDATION_FAILED","errors":{"minRentVnd":["x"],"areaM2":"not an array","maxRentVnd":42}}
            """;
        using var response = ProblemResponse(HttpStatusCode.BadRequest, json);

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await ApiResponseReader.ReadAsync<AuthTokensDto>(response));

        Assert.That(ex!.ValidationErrors, Is.Not.Null);
        Assert.That(ex.ValidationErrors!.Count, Is.EqualTo(1));
        Assert.That(ex.ValidationErrors.ContainsKey("minRentVnd"), Is.True);
        Assert.That(ex.ValidationErrors["minRentVnd"], Is.EqualTo(new[] { "x" }));
        Assert.That(ex.ValidationErrors.ContainsKey("areaM2"), Is.False);
        Assert.That(ex.ValidationErrors.ContainsKey("maxRentVnd"), Is.False);
    }

    [Test]
    public void ReadAsync_ErrorsKeyEmptyString_Preserved()
    {
        const string json = """
            {"errorCode":"VALIDATION_FAILED","errors":{"":["The request field is required."],"minRentVnd":["x"]}}
            """;
        using var response = ProblemResponse(HttpStatusCode.BadRequest, json);

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await ApiResponseReader.ReadAsync<AuthTokensDto>(response));

        Assert.That(ex!.ValidationErrors, Is.Not.Null);
        Assert.That(ex.ValidationErrors!.ContainsKey(""), Is.True);
        Assert.That(ex.ValidationErrors[""], Is.EqualTo(new[] { "The request field is required." }));
    }

    [TestCase("")]
    [TestCase("null")]
    public void ReadAsync_Success200WithNullOrEmptyBody_ThrowsEmptyBodyApiException(string body)
    {
        using var response = JsonResponse(HttpStatusCode.OK, body);

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await ApiResponseReader.ReadAsync<AuthTokensDto>(response));

        Assert.That(ex!.Message, Is.EqualTo("Server returned an empty body."));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public void EnsureSuccessAsync_204_DoesNotThrow()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NoContent);

        Assert.DoesNotThrowAsync(async () => await ApiResponseReader.EnsureSuccessAsync(response));
    }

    [Test]
    public void EnsureSuccessAsync_500_Throws()
    {
        const string json = """{"title":"Server Error","status":500,"errorCode":"INTERNAL_ERROR"}""";
        using var response = ProblemResponse(HttpStatusCode.InternalServerError, json);

        var ex = Assert.ThrowsAsync<ApiException>(async () =>
            await ApiResponseReader.EnsureSuccessAsync(response));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That(ex.ErrorCode, Is.EqualTo("INTERNAL_ERROR"));
    }

    [Test]
    public void FromNetworkException_HttpRequestException_ReturnsNetworkError()
    {
        var inner = new HttpRequestException("connection refused");

        var ex = ApiResponseReader.FromNetworkException(inner);

        Assert.That(ex.IsNetworkError, Is.True);
        Assert.That(ex.ErrorCode, Is.EqualTo("NETWORK_ERROR"));
        Assert.That(ex.StatusCode, Is.EqualTo((HttpStatusCode)0));
        Assert.That(ex.InnerException, Is.SameAs(inner));
        Assert.That(ex.Message, Is.EqualTo("Unable to connect to the server. Please check your internet connection."));
        Assert.That(ex.ValidationErrors, Is.Null);
    }

    [Test]
    public async Task CreateExceptionAsync_DoesNotLogBodyOrTokens()
    {
        const string json = """
            {
              "title": "Unauthorized",
              "status": 401,
              "errorCode": "UNAUTHORIZED",
              "accessToken": "a1.super-secret-access",
              "refreshToken": "r1.super-secret-refresh"
            }
            """;
        using var response = ProblemResponse(HttpStatusCode.Unauthorized, json);
        var logger = new CapturingLogger();

        var ex = await ApiResponseReader.CreateExceptionAsync(response, logger);

        Assert.That(ex.ErrorCode, Is.EqualTo("UNAUTHORIZED"));
        Assert.That(logger.Messages, Is.Not.Empty);
        foreach (var message in logger.Messages)
        {
            Assert.That(message, Does.Not.Contain("super-secret"));
            Assert.That(message, Does.Not.Contain("accessToken"));
            Assert.That(message, Does.Not.Contain("refreshToken"));
        }
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage ProblemResponse(HttpStatusCode status, string json, string? reasonPhrase = null) =>
        new(status)
        {
            ReasonPhrase = reasonPhrase,
            Content = new StringContent(json, Encoding.UTF8, "application/problem+json")
        };

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
