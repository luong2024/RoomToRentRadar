using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using NUnit.Framework;
using RoomToRentRadar.Api.Errors;

namespace RoomToRentRadarUnitTests.Api;

[TestFixture]
public class ApiProblemFactoryTests
{
    [Test]
    public void FromModelState_WithFormTooLarge_Returns413FileTooLarge()
    {
        var context = CreateActionContext();
        context.ModelState.AddModelError(
            string.Empty,
            "Failed to read the request form. Request body too large. The max request body size is 11534336 bytes.");

        var problem = ApiProblemFactory.FromModelState(context);

        Assert.Multiple(() =>
        {
            Assert.That(problem.Status, Is.EqualTo(StatusCodes.Status413PayloadTooLarge));
            Assert.That(problem.Title, Is.EqualTo("Payload Too Large"));
            Assert.That(problem.Extensions["errorCode"], Is.EqualTo("FILE_TOO_LARGE"));
            Assert.That(problem.Instance, Is.EqualTo("/api/landlord/photos"));
        });
    }

    [Test]
    public void FromModelState_WithValidationErrors_Returns400ValidationFailedAndErrors()
    {
        var context = CreateActionContext();
        context.ModelState.AddModelError("file", "The file field is required.");

        var problem = ApiProblemFactory.FromModelState(context);

        Assert.Multiple(() =>
        {
            Assert.That(problem.Status, Is.EqualTo(StatusCodes.Status400BadRequest));
            Assert.That(problem.Title, Is.EqualTo("Validation Error"));
            Assert.That(problem.Extensions["errorCode"], Is.EqualTo("VALIDATION_FAILED"));
            Assert.That(problem.Extensions["errors"], Is.TypeOf<Dictionary<string, string[]>>());
        });

        var errors = (Dictionary<string, string[]>)problem.Extensions["errors"]!;
        Assert.That(errors["file"], Is.EqualTo(new[] { "The file field is required." }));
    }

    [Test]
    public void FromStatusCode_WithUnauthorized_ReturnsInvalidToken()
    {
        var problem = ApiProblemFactory.FromStatusCode(StatusCodes.Status401Unauthorized, "/api/landlord/photos");

        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(problem.Title, Is.EqualTo("Authentication Failed"));
            Assert.That(problem.Extensions["errorCode"], Is.EqualTo("INVALID_TOKEN"));
        });
    }

    [Test]
    public void FromStatusCode_WithForbidden_ReturnsForbiddenRole()
    {
        var problem = ApiProblemFactory.FromStatusCode(StatusCodes.Status403Forbidden, "/api/landlord/photos");

        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(problem.Title, Is.EqualTo("Access Forbidden"));
            Assert.That(problem.Extensions["errorCode"], Is.EqualTo("FORBIDDEN_ROLE"));
        });
    }

    [Test]
    public void FromStatusCode_WithPayloadTooLarge_ReturnsFileTooLarge()
    {
        var problem = ApiProblemFactory.FromStatusCode(StatusCodes.Status413PayloadTooLarge, "/api/landlord/photos");

        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status413PayloadTooLarge));
            Assert.That(problem.Extensions["errorCode"], Is.EqualTo("FILE_TOO_LARGE"));
        });
    }

    [Test]
    public void FromStatusCode_WithUnsupportedMediaType_ReturnsUnsupportedMediaType()
    {
        var problem = ApiProblemFactory.FromStatusCode(
            StatusCodes.Status415UnsupportedMediaType,
            "/api/landlord/photos");

        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Status, Is.EqualTo(StatusCodes.Status415UnsupportedMediaType));
            Assert.That(problem.Extensions["errorCode"], Is.EqualTo("UNSUPPORTED_MEDIA_TYPE"));
        });
    }

    [Test]
    public void FromStatusCode_WithUnmappedStatus_ReturnsNull()
    {
        var problem = ApiProblemFactory.FromStatusCode(StatusCodes.Status404NotFound, "/api/landlord/photos");

        Assert.That(problem, Is.Null);
    }

    private static ActionContext CreateActionContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/landlord/photos";

        return new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
    }
}
