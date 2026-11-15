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
    public void Create_InvalidModelState_ConvertsErrorKeysToCamelCase()
    {
        // T00.5 / ADR-012: the old expectation (keys kept as-is) was replaced by the camelCase requirement.
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("MinRentVnd", "Rent must be a positive amount.");
        modelState.AddModelError("Address.Street", "Street is required.");
        var context = CreateContext(modelState);

        var result = ValidationProblemResponseFactory.Create(context);

        var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
        Assert.That(problem.Errors.Keys, Is.EquivalentTo(new[] { "minRentVnd", "address.street" }));
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

    [Test]
    public void Create_PascalCaseKeys_AreConvertedToCamelCase()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("MinRentVnd", "Rent must be a positive amount.");
        modelState.AddModelError("RoomTypes", "Room type is invalid.");
        var context = CreateContext(modelState);

        var result = ValidationProblemResponseFactory.Create(context);

        var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
        Assert.That(problem.Errors.Keys, Is.EquivalentTo(new[] { "minRentVnd", "roomTypes" }));
    }

    [Test]
    public void Create_NestedPath_ConvertsEachSegment()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Address.Street", "Street is required.");
        modelState.AddModelError("Owner.Contact.PhoneNumber", "Phone number is invalid.");
        var context = CreateContext(modelState);

        var result = ValidationProblemResponseFactory.Create(context);

        var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
        Assert.That(problem.Errors.Keys, Is.EquivalentTo(new[] { "address.street", "owner.contact.phoneNumber" }));
    }

    [Test]
    public void Create_JsonPathKeys_KeepDollarPrefix()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$", "Body is invalid.");
        modelState.AddModelError("$.MinRent", "Rent must be a positive amount.");
        var context = CreateContext(modelState);

        var result = ValidationProblemResponseFactory.Create(context);

        var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
        Assert.That(problem.Errors.Keys, Is.EquivalentTo(new[] { "$", "$.minRent" }));
    }

    [Test]
    public void Create_IndexedKeys_KeepIndex()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Items[0].Name", "Name is required.");
        var context = CreateContext(modelState);

        var result = ValidationProblemResponseFactory.Create(context);

        var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
        Assert.That(problem.Errors.Keys, Is.EquivalentTo(new[] { "items[0].name" }));
    }

    [Test]
    public void Create_KeysCollidingAfterCamelCase_AreMergedNotThrown()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Name", "A");
        modelState.AddModelError("NAME", "B");
        var context = CreateContext(modelState);

        var result = ValidationProblemResponseFactory.Create(context);

        var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(problem.Errors.Keys, Is.EquivalentTo(new[] { "name" }));
            Assert.That(problem.Errors["name"], Is.EquivalentTo(new[] { "A", "B" }));
        });
    }

    [Test]
    public void Create_EmptyKey_IsKept()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(string.Empty, "Body is invalid.");
        var context = CreateContext(modelState);

        var result = ValidationProblemResponseFactory.Create(context);

        var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
        Assert.That(problem.Errors.ContainsKey(string.Empty), Is.True);
    }

    [Test]
    public void Create_AlreadyCamelCaseKey_IsUnchanged()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("request", "Body is invalid.");
        var context = CreateContext(modelState);

        var result = ValidationProblemResponseFactory.Create(context);

        var problem = (ValidationProblemDetails)((BadRequestObjectResult)result).Value!;
        Assert.That(problem.Errors.Keys, Is.EquivalentTo(new[] { "request" }));
    }

    private static ActionContext CreateContext(ModelStateDictionary modelState, string path = "/api/rooms")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        return new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), modelState);
    }
}