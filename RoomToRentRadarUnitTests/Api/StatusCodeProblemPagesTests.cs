using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using RoomToRentRadar.Api.Errors;
using RoomToRentRadar.Ports.Contracts.Common;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// ADR-012 / system-architecture section 7.2: bodyless status responses are rewritten as
/// problem+json with a stable errorCode (405 and other 4xx without a code stay bare).
/// </summary>
[TestFixture]
public class StatusCodeProblemPagesTests
{
    [Test]
    public async Task WriteAsync_PayloadTooLarge_Writes413FileTooLargeProblemJson()
    {
        // Medium M2 / BR-MED-02: a bodyless 413 must carry FILE_TOO_LARGE like the upload use case.
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/landlord/photos";
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        context.Response.Body = new MemoryStream();

        await StatusCodeProblemPages.WriteAsync(CreateContext(context));

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        var root = body.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(context.Response.ContentType, Is.EqualTo("application/problem+json"));
            Assert.That(root.GetProperty("status").GetInt32(), Is.EqualTo(StatusCodes.Status413PayloadTooLarge));
            Assert.That(root.GetProperty("title").GetString(), Is.EqualTo("Payload Too Large"));
            Assert.That(root.GetProperty("errorCode").GetString(), Is.EqualTo(ProblemCodes.FileTooLarge));
        });
    }

    private static StatusCodeContext CreateContext(HttpContext context) =>
        new(context, new StatusCodePagesOptions(), _ => Task.CompletedTask);
}
