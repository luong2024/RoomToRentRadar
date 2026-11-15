using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Api.Controllers;
using RoomToRentRadar.Api.Errors;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;

namespace RoomToRentRadarUnitTests.Api;

/// <summary>
/// ADR-012: lỗi nghiệp vụ trả <c>application/problem+json</c> kèm <c>errorCode</c> ổn định.
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
        var mapping = ExceptionProblemMap.Resolve(
            new DomainValidationException("Invalid sort option.", "INVALID_SORT_OPTION"));

        Assert.Multiple(() =>
        {
            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping!.Status, Is.EqualTo(400));
            Assert.That(mapping.ErrorCode, Is.EqualTo("INVALID_SORT_OPTION"));
        });
    }

    [Test]
    public void Middleware_MapsDomainValidationWithoutCodeToValidationFailed()
    {
        var mapping = ExceptionProblemMap.Resolve(new DomainValidationException("missing"));

        Assert.Multiple(() =>
        {
            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping!.Status, Is.EqualTo(400));
            Assert.That(mapping.ErrorCode, Is.EqualTo("VALIDATION_FAILED"), "system-architecture §7.2");
        });
    }

    [Test]
    public void Middleware_MapsInvalidStateTransitionToInvalidStateTransitionCode()
    {
        var mapping = ExceptionProblemMap.Resolve(
            new InvalidStateTransitionException("Cannot Approve: listing is 'Rejected'."));

        Assert.Multiple(() =>
        {
            Assert.That(mapping, Is.Not.Null);
            Assert.That(mapping!.Status, Is.EqualTo(409));
            Assert.That(mapping.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"), "system-architecture §7.2");
        });
    }

    [Test]
    public void Middleware_MapsInvalidStateTransitionTo409()
    {
        var mapping = ExceptionProblemMap.Resolve(
            new InvalidStateTransitionException("Cannot Approve: listing is 'Rejected'."));

        Assert.Multiple(() =>
        {
            Assert.That(mapping, Is.Not.Null, "ADR-011 yêu cầu 409 chứ không phải 500");
            Assert.That(mapping!.Status, Is.EqualTo(409));
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
