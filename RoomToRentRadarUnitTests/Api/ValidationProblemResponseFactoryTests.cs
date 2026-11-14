using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using RoomToRentRadar.Api.Errors;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// ADR-012 / system-architecture section 7.2: model binding 400s share the problem+json
/// shape of domain validation errors, with a stable errorCode and an `errors` map.
/// </summary>
[TestFixture]
public class ValidationProblemResponseFactoryTests
{
    [Test]
    public void Create_InvalidModelState_ReturnsBadRequestProblemJsonWithValidationFailedCode()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("minRentVnd", "Rent must be a positive amount.");
        var context = CreateContext(modelState);

        var result = ValidationProblemResponseFactory.Create(context);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var badRequest = (BadRequestObjectResult)result;
        var problem = badRequest.Value as ValidationProblemDetails;
        Assert.That(problem, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(badRequest.StatusCode, Is.EqualTo(400));
            Assert.That(problem!.Status, Is.EqualTo(400));
            Assert.That(problem.Extensions["errorCode"], Is.EqualTo("VALIDATION_FAILED"));
            Assert.That(badRequest.ContentTypes, Does.Contain("application/problem+json"));
        });
    }

    [Test]
    public void Create_InvalidModelState_KeepsErrorKeysAsIs()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("minRentVnd", "Rent must be a positive amount.");
        modelState.AddModelError("Address.Street", "Street is required.");
        var context = CreateContext(modelState);

        var result = ValidationProblemResponseFactory.Create(context);

        var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
        Assert.That(problem.Errors.Keys, Is.EquivalentTo(new[] { "minRentVnd", "Address.Street" }));
    }

    [Test]
    public void Create_WhitespaceErrorMessage_UsesInvalidValuePlaceholder()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("roomTypes", string.Empty);
        modelState.AddModelError("campusId", "   ");
        var context = CreateContext(modelState);

        var result = ValidationProblemResponseFactory.Create(context);

        var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(problem.Errors["roomTypes"], Is.EqualTo(new[] { "Invalid value." }));
            Assert.That(problem.Errors["campusId"], Is.EqualTo(new[] { "Invalid value." }));
        });
    }

    [Test]
    public void Create_InvalidModelState_SetsInstanceToRequestPath()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("maxDistanceKm", "Distance must be positive.");
        var context = CreateContext(modelState, "/api/rooms/search");

        var result = ValidationProblemResponseFactory.Create(context);

        var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
        Assert.That(problem.Instance, Is.EqualTo("/api/rooms/search"));
    }

    [Test]
    public void Create_EmptyModelState_ReturnsEmptyErrors()
    {
        var context = CreateContext(new ModelStateDictionary());

        var result = ValidationProblemResponseFactory.Create(context);

        Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
        Assert.That(problem.Errors, Is.Empty);
    }

    private static ActionContext CreateContext(ModelStateDictionary modelState, string path = "/api/rooms")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        return new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), modelState);
    }
}