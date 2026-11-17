using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RoomToRentRadar.Adapters.Inbound.WebApi.Middleware;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Adapters.Tests.Inbound;

public class GlobalExceptionHandlerTests
{
    private readonly GlobalExceptionHandler _handler = new(NullLogger<GlobalExceptionHandler>.Instance);

    private static IEnumerable<TestCaseData> ExceptionMappingCases()
    {
        yield return new TestCaseData(
            new RequestValidationException("email", "Email is required."),
            StatusCodes.Status400BadRequest);
        yield return new TestCaseData(
            new InvalidEmailException("Invalid email."),
            StatusCodes.Status400BadRequest);
        yield return new TestCaseData(
            new InvalidRoleException("Invalid role."),
            StatusCodes.Status400BadRequest);
        yield return new TestCaseData(
            new AdminSelfRegistrationForbiddenException(),
            StatusCodes.Status400BadRequest);
        yield return new TestCaseData(
            new DuplicateEmailException(),
            StatusCodes.Status409Conflict);
        yield return new TestCaseData(
            new ExternalIdentityConflictException(),
            StatusCodes.Status409Conflict);
        yield return new TestCaseData(
            new AuthenticationFailedException(),
            StatusCodes.Status401Unauthorized);
        yield return new TestCaseData(
            new InvalidGoogleTokenException(),
            StatusCodes.Status401Unauthorized);
        yield return new TestCaseData(
            new InvalidRefreshTokenException(),
            StatusCodes.Status401Unauthorized);
        yield return new TestCaseData(
            new UserInactiveException(),
            StatusCodes.Status401Unauthorized);
        yield return new TestCaseData(
            new InvalidOperationException("Unexpected failure"),
            StatusCodes.Status500InternalServerError);
    }

    [TestCaseSource(nameof(ExceptionMappingCases))]
    public async Task TryHandleAsync_ShouldMapExceptionsToExpectedProblemDetailsStatusCode(
        Exception exception,
        int expectedStatusCode)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/auth/login";
        using var bodyStream = new MemoryStream();
        context.Response.Body = bodyStream;

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        bodyStream.Position = 0;
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            bodyStream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(expectedStatusCode));
            Assert.That(problemDetails, Is.Not.Null);
            Assert.That(problemDetails!.Status, Is.EqualTo(expectedStatusCode));
            Assert.That(problemDetails.Instance, Is.EqualTo("/api/v1/auth/login"));
        });
    }
}
