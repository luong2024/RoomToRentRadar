// ADR-012, system-architecture 7.2: the global middleware writes problem+json for every mapped row
// and a sanitized 500 INTERNAL_ERROR for unknown exceptions; pass-through cases must rethrow.
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RoomToRentRadar.Api.Middlewares;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class ProblemDetailsMiddlewareTests
{
    private static IEnumerable<TestCaseData> MappingRows()
    {
        yield return new TestCaseData(
            new DomainValidationException("Duplicate room.", ProblemCodes.DuplicateRoom),
            409, ProblemCodes.DuplicateRoom, "Conflict").SetName("R1_DuplicateRoom_409");
        yield return new TestCaseData(
            new DomainValidationException("Invalid sort option.", ProblemCodes.InvalidSortOption),
            400, ProblemCodes.InvalidSortOption, "Validation Error").SetName("R2_ValidationWithCode_400");
        yield return new TestCaseData(
            new DomainValidationException("missing"),
            400, ProblemCodes.ValidationFailed, "Validation Error").SetName("R3_ValidationWithoutCode_400");
        yield return new TestCaseData(
            new InvalidCredentialsException(),
            401, ProblemCodes.InvalidCredentials, "Authentication Failed").SetName("R4_InvalidCredentials_401");
        yield return new TestCaseData(
            new InvalidTokenException(),
            401, ProblemCodes.InvalidToken, "Authentication Failed").SetName("R5_InvalidToken_401");
        yield return new TestCaseData(
            new AccountLockedException(DateTime.UtcNow.AddMinutes(15)),
            403, ProblemCodes.AccountLocked, "Account Locked").SetName("R6_AccountLocked_403");
        yield return new TestCaseData(
            new ForbiddenRoleException("Role is not allowed."),
            403, ProblemCodes.ForbiddenRole, "Access Forbidden").SetName("R7_ForbiddenRole_403");
        yield return new TestCaseData(
            new LandlordNotApprovedException("landlord-1"),
            403, ProblemCodes.LandlordNotApproved, "Access Forbidden").SetName("R8_LandlordNotApproved_403");
        yield return new TestCaseData(
            new UserNotFoundException("u-1"),
            404, ProblemCodes.NotFound, "Not Found").SetName("R9_UserNotFound_404");
        yield return new TestCaseData(
            new EntityNotFoundException(ProblemCodes.RoomNotFound),
            404, ProblemCodes.RoomNotFound, "Not Found").SetName("R10_EntityNotFound_404");
        yield return new TestCaseData(
            new DuplicateEmailException("user@ictu.edu.vn"),
            409, ProblemCodes.EmailTaken, "Conflict").SetName("R11_DuplicateEmail_409");
        yield return new TestCaseData(
            new ConcurrencyConflictException(),
            409, ProblemCodes.ConcurrencyConflict, "Conflict").SetName("R12_ConcurrencyConflict_409");
        yield return new TestCaseData(
            new InvalidStateTransitionException("Cannot Approve: listing is 'Rejected'."),
            409, ProblemCodes.InvalidStateTransition, "Conflict").SetName("R13_InvalidStateTransition_409");
        yield return new TestCaseData(
            new LimitExceededException("Too many photos."),
            409, ProblemCodes.LimitExceeded, "Conflict").SetName("R14_LimitExceeded_409");
    }

    [TestCaseSource(nameof(MappingRows))]
    public async Task Invoke_MappedException_WritesProblemJson(
        Exception exception,
        int expectedStatus,
        string expectedErrorCode,
        string expectedTitle)
    {
        var logger = new CapturingLogger<ProblemDetailsMiddleware>();
        var middleware = new ProblemDetailsMiddleware(_ => throw exception, logger);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        using var body = await ReadBodyAsync(context);
        var root = body.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(expectedStatus));
            Assert.That(context.Response.ContentType, Is.EqualTo("application/problem+json"));
            Assert.That(root.GetProperty("status").GetInt32(), Is.EqualTo(expectedStatus));
            Assert.That(root.GetProperty("title").GetString(), Is.EqualTo(expectedTitle));
            Assert.That(root.GetProperty("errorCode").GetString(), Is.EqualTo(expectedErrorCode));
            Assert.That(root.GetProperty("instance").GetString(), Is.EqualTo("/api/test/endpoint"));
            Assert.That(root.GetProperty("detail").GetString(), Is.EqualTo(exception.Message));
            Assert.That(logger.Entries.Where(entry => entry.Level == LogLevel.Error), Is.Empty);
        });
    }

    [Test]
    public async Task Invoke_UnknownException_Returns500InternalErrorWithoutStackTraceOrMessage()
    {
        var exception = new InvalidOperationException("secret SQL: SELECT * FROM Users at C:\\secret\\path.cs");
        var logger = new CapturingLogger<ProblemDetailsMiddleware>();
        var middleware = new ProblemDetailsMiddleware(_ => throw exception, logger);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        var rawBody = ReadBodyText(context);
        using var body = JsonDocument.Parse(rawBody);
        var root = body.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(500));
            Assert.That(context.Response.ContentType, Is.EqualTo("application/problem+json"));
            Assert.That(root.GetProperty("errorCode").GetString(), Is.EqualTo(ProblemCodes.InternalError));
            Assert.That(root.GetProperty("title").GetString(), Is.EqualTo("Internal Server Error"));
            Assert.That(root.GetProperty("detail").GetString(), Is.EqualTo("An unexpected error occurred."));
            Assert.That(rawBody, Does.Not.Contain("secret"));
            Assert.That(rawBody, Does.Not.Contain("SELECT"));
            Assert.That(rawBody, Does.Not.Contain("C:\\"));
            Assert.That(rawBody, Does.Not.Contain(" at "));
            Assert.That(rawBody, Does.Not.Contain("InvalidOperationException"));
            Assert.That(rawBody, Does.Not.Contain("Exception"));
            Assert.That(logger.Entries.Count(entry => entry.Level == LogLevel.Error), Is.EqualTo(1));
            Assert.That(logger.Entries.SingleOrDefault(entry => entry.Level == LogLevel.Error).Exception, Is.SameAs(exception));
        });
    }

    [Test]
    public async Task Invoke_UnknownExceptionWithMappedInnerException_StillReturns500()
    {
        var logger = new CapturingLogger<ProblemDetailsMiddleware>();
        var middleware = new ProblemDetailsMiddleware(
            _ => throw new Exception("outer", new InvalidCredentialsException()), logger);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        using var body = await ReadBodyAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(500));
            Assert.That(body.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo(ProblemCodes.InternalError));
        });
    }

    [Test]
    public void Invoke_ResponseAlreadyStarted_Rethrows()
    {
        var exception = new InvalidCredentialsException();
        var logger = new CapturingLogger<ProblemDetailsMiddleware>();
        var middleware = new ProblemDetailsMiddleware(_ => throw exception, logger);
        var context = CreateContext();
        context.Features.Set<IHttpResponseFeature>(new StartedHttpResponseFeature());

        var ex = Assert.ThrowsAsync<InvalidCredentialsException>(async () => await middleware.InvokeAsync(context));

        Assert.Multiple(() =>
        {
            Assert.That(ex, Is.SameAs(exception));
            Assert.That(((MemoryStream)context.Response.Body).Length, Is.EqualTo(0));
            Assert.That(logger.Entries, Is.Empty);
        });
    }

    [Test]
    public void Invoke_RequestAbortedCancellation_Rethrows()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var exception = new OperationCanceledException(cts.Token);
        var logger = new CapturingLogger<ProblemDetailsMiddleware>();
        var middleware = new ProblemDetailsMiddleware(_ => throw exception, logger);
        var context = CreateContext();
        context.RequestAborted = cts.Token;

        var ex = Assert.ThrowsAsync<OperationCanceledException>(async () => await middleware.InvokeAsync(context));

        Assert.Multiple(() =>
        {
            Assert.That(ex, Is.SameAs(exception));
            Assert.That(((MemoryStream)context.Response.Body).Length, Is.EqualTo(0));
            Assert.That(logger.Entries, Is.Empty);
        });
    }

    [Test]
    public async Task Invoke_OperationCanceledWithoutRequestAborted_Returns500InternalError()
    {
        var logger = new CapturingLogger<ProblemDetailsMiddleware>();
        var middleware = new ProblemDetailsMiddleware(_ => throw new OperationCanceledException(), logger);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        using var body = await ReadBodyAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(500));
            Assert.That(body.RootElement.GetProperty("errorCode").GetString(), Is.EqualTo(ProblemCodes.InternalError));
        });
    }

    [Test]
    public void Invoke_BadHttpRequestException_Rethrows()
    {
        var exception = new BadHttpRequestException("bad body", StatusCodes.Status400BadRequest);
        var logger = new CapturingLogger<ProblemDetailsMiddleware>();
        var middleware = new ProblemDetailsMiddleware(_ => throw exception, logger);
        var context = CreateContext();

        var ex = Assert.ThrowsAsync<BadHttpRequestException>(async () => await middleware.InvokeAsync(context));

        Assert.Multiple(() =>
        {
            Assert.That(ex, Is.SameAs(exception));
            Assert.That(((MemoryStream)context.Response.Body).Length, Is.EqualTo(0));
            Assert.That(logger.Entries, Is.Empty);
        });
    }

    [Test]
    public async Task Invoke_NoException_PassesThroughUntouched()
    {
        var logger = new CapturingLogger<ProblemDetailsMiddleware>();
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };
        var middleware = new ProblemDetailsMiddleware(next, logger);
        var context = CreateContext();

        await middleware.InvokeAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(200));
            Assert.That(((MemoryStream)context.Response.Body).Length, Is.EqualTo(0));
            Assert.That(context.Response.ContentType, Is.Null.Or.Empty);
            Assert.That(logger.Entries, Is.Empty);
        });
    }

    [Test]
    public async Task Invoke_DomainExceptionMessage_IsDetailForMappedRowsOnly()
    {
        var logger = new CapturingLogger<ProblemDetailsMiddleware>();
        var mappedContext = CreateContext();
        var mappedMiddleware = new ProblemDetailsMiddleware(
            _ => throw new InvalidCredentialsException("mapped detail message"), logger);
        var unexpectedContext = CreateContext();
        var unexpectedMiddleware = new ProblemDetailsMiddleware(
            _ => throw new InvalidOperationException("unexpected detail message"), logger);

        await mappedMiddleware.InvokeAsync(mappedContext);
        await unexpectedMiddleware.InvokeAsync(unexpectedContext);

        using var mappedBody = await ReadBodyAsync(mappedContext);
        using var unexpectedBody = await ReadBodyAsync(unexpectedContext);

        Assert.Multiple(() =>
        {
            Assert.That(mappedBody.RootElement.GetProperty("detail").GetString(), Is.EqualTo("mapped detail message"));
            Assert.That(unexpectedBody.RootElement.GetProperty("detail").GetString(), Is.EqualTo("An unexpected error occurred."));
        });
    }

    [Test]
    public void Ctor_NullArguments_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ProblemDetailsMiddleware(null!, NullLogger<ProblemDetailsMiddleware>.Instance));
            Assert.Throws<ArgumentNullException>(() =>
                new ProblemDetailsMiddleware(_ => Task.CompletedTask, null!));
        });
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/test/endpoint";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    private static string ReadBodyText(HttpContext context) =>
        Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());

    private sealed class StartedHttpResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, exception, formatter(state, exception)));
        }
    }
}