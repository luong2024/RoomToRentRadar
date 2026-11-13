using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Api.Middlewares;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// ADR-012: lỗi nghiệp vụ trả <c>application/problem+json</c> kèm <c>errorCode</c> ổn định.
/// RED ở Task 1 cho việc map <see cref="InvalidStateTransitionException"/> → 409 (ADR-011).
/// </summary>
[TestFixture]
public class SearchErrorContractTests
{
    [Test]
    public void DomainValidationException_CarriesStableErrorCode()
    {
        var exception = new DomainValidationException("Invalid sort option.", "INVALID_SORT_OPTION");

        Assert.Multiple(() =>
        {
            Assert.That(exception.ErrorCode, Is.EqualTo("INVALID_SORT_OPTION"));
            Assert.That(exception.Message, Is.EqualTo("Invalid sort option."));
        });
    }

    [Test]
    public void DomainValidationException_ErrorCodeIsOptional()
    {
        Assert.That(new DomainValidationException("missing").ErrorCode, Is.Null);
    }

    [Test]
    public void Middleware_MapsDomainValidationExceptionTo400WithErrorCode()
    {
        var mapped = AuthExceptionHandlingMiddleware.TryMapException(
            new DomainValidationException("Invalid sort option.", "INVALID_SORT_OPTION"),
            out var statusCode,
            out _);

        Assert.Multiple(() =>
        {
            Assert.That(mapped, Is.True);
            Assert.That(statusCode, Is.EqualTo(400));
        });
    }

    [Test]
    public void Middleware_MapsDomainValidationWithoutCodeToValidationFailed()
    {
        var mapped = AuthExceptionHandlingMiddleware.TryMapException(
            new DomainValidationException("missing"),
            out var statusCode,
            out _,
            out var errorCode);

        Assert.Multiple(() =>
        {
            Assert.That(mapped, Is.True);
            Assert.That(statusCode, Is.EqualTo(400));
            Assert.That(errorCode, Is.EqualTo("VALIDATION_FAILED"), "system-architecture §7.2");
        });
    }

    [Test]
    public void Middleware_MapsInvalidStateTransitionToInvalidStateTransitionCode()
    {
        var mapped = AuthExceptionHandlingMiddleware.TryMapException(
            new InvalidStateTransitionException("Cannot Approve: listing is 'Rejected'."),
            out var statusCode,
            out _,
            out var errorCode);

        Assert.Multiple(() =>
        {
            Assert.That(mapped, Is.True);
            Assert.That(statusCode, Is.EqualTo(409));
            Assert.That(errorCode, Is.EqualTo("INVALID_STATE_TRANSITION"), "system-architecture §7.2");
        });
    }

    [Test]
    public void Middleware_MapsInvalidStateTransitionTo409()
    {
        var mapped = AuthExceptionHandlingMiddleware.TryMapException(
            new InvalidStateTransitionException("Cannot Approve: listing is 'Rejected'."),
            out var statusCode,
            out _);

        Assert.Multiple(() =>
        {
            Assert.That(mapped, Is.True, "ADR-011 yêu cầu 409 chứ không phải 500");
            Assert.That(statusCode, Is.EqualTo(409));
        });
    }

    [Test]
    public void SearchRooms_QueryContractHasNoServerControlledFields()
    {
        var propertyNames = typeof(RoomSearchRequest).GetProperties().Select(property => property.Name).ToList();

        Assert.That(propertyNames, Is.Not.SupersetOf(new[] { "Status", "IsVerified", "LandlordId", "Role", "CreatedAtUtc" }),
            "GUARDRAILS 1.5.1 request DTO whitelisting");
    }

    [Test]
    public void SearchRooms_QueryContractExposesOnlyOpenApiParameters()
    {
        var propertyNames = typeof(RoomSearchRequest).GetProperties()
            .Where(property => property.GetMethod?.IsStatic != true)
            .Select(property => property.Name)
            .ToList();

        Assert.That(propertyNames, Is.EquivalentTo(new[]
        {
            "CampusId", "MinRentVnd", "MaxRentVnd", "MinAreaM2", "MaxAreaM2", "RoomTypes",
            "MaxDistanceKm", "Sort", "PageNumber", "PageSize"
        }));
    }

    [Test]
    public void SearchRoomsAction_BindsFromQuery()
    {
        var method = typeof(RoomsController).GetMethod(nameof(RoomsController.SearchRooms));
        var requestParameter = method!.GetParameters().Single(p => p.ParameterType == typeof(RoomSearchRequest));

        Assert.Multiple(() =>
        {
            Assert.That(requestParameter.GetCustomAttribute<FromQueryAttribute>(), Is.Not.Null);
            Assert.That(requestParameter.ParameterType, Is.EqualTo(typeof(RoomSearchRequest)));
        });
    }
}
