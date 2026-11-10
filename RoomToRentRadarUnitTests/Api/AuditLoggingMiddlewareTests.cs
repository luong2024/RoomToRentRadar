using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using RoomToRentRadar.Api.Middlewares;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class AuditLoggingMiddlewareTests
{
    private IAuditLogRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IAuditLogRepository>();
    }

    [Test]
    public async Task InvokeAsync_MutatingPostRequest_InterceptsAndInsertsAuditLogEntry()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/rooms";
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        context.Request.Headers.UserAgent = "MAUI-Test-Client";

        var claims = new[]
        {
            new Claim("sub", "user-landlord-01"),
            new Claim("email", "landlord@gmail.com"),
            new Claim("role", "Landlord")
        };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            return Task.CompletedTask;
        };

        var middleware = new AuditLoggingMiddleware(next);
        await middleware.InvokeAsync(context, _repository);

        await _repository.Received(1).InsertAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.UserId == "user-landlord-01" &&
                e.UserEmail == "landlord@gmail.com" &&
                e.Role == "Landlord" &&
                e.Action == "POST" &&
                e.StatusCode == StatusCodes.Status201Created &&
                e.IsSuccess == true &&
                e.IpAddress == "127.0.0.1"),
            Arg.Any<CancellationToken>());
    }

    [TestCase("GET")]
    [TestCase("HEAD")]
    [TestCase("OPTIONS")]
    public async Task InvokeAsync_SafeRequests_DoNotInsertAuditLogEntry(string safeMethod)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = safeMethod;
        context.Request.Path = "/api/rooms/123";

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new AuditLoggingMiddleware(next);
        await middleware.InvokeAsync(context, _repository);

        await _repository.DidNotReceiveWithAnyArgs().InsertAsync(null!, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InvokeAsync_AuthEndpoint_RedactsSensitiveBody()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/auth/login";
        var rawJson = "{\"email\":\"student@ictu.edu.vn\",\"password\":\"SuperSecret123!\"}";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new AuditLoggingMiddleware(next);
        await middleware.InvokeAsync(context, _repository);

        await _repository.Received(1).InsertAsync(
            Arg.Is<AuditLogEntry>(e =>
                e.Action == "POST" &&
                (e.DetailsJson == null || !e.DetailsJson.Contains("SuperSecret123!"))),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InvokeAsync_WhenRepositoryThrowsException_DoesNotCrashRequestPipeline()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/rooms";

        _repository.When(r => r.InsertAsync(Arg.Any<AuditLogEntry>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("DB connection error"));

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new AuditLoggingMiddleware(next);

        Assert.DoesNotThrowAsync(async () =>
        {
            await middleware.InvokeAsync(context, _repository);
        });

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }
}

