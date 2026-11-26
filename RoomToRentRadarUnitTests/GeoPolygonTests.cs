using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using RoomToRentRadar.Api.Filters;
using RoomToRentRadar.Api.Services;
using RoomToRentRadar.Api.Services.Geo;
using Xunit;

namespace RoomToRentRadarUnitTests;

/// <summary>Refactor US05 / T05.2 — hình học polygon tách riêng + filter 400 dùng chung.</summary>
public class GeoPolygonTests
{
    [Fact]
    public void TryNormalize_Empty_ReturnsVietnameseError()
    {
        GeoPolygon.TryNormalize([], out var ring, out var error).Should().BeFalse();

        ring.Should().BeEmpty();
        error.Should().Be("Vui lòng vẽ vùng tìm kiếm.");
    }

    [Fact]
    public void TryNormalize_TooManyPoints_ReturnsError()
    {
        var polygon = Enumerable.Range(0, GeoPolygon.MaxRingPoints + 1)
            .Select(i => new[] { 105.0 + i * 0.0001, 21.5 })
            .ToList();

        GeoPolygon.TryNormalize(polygon, out _, out var error).Should().BeFalse();
        error.Should().Contain(GeoPolygon.MaxRingPoints.ToString());
    }

    [Fact]
    public void Normalize_Invalid_ThrowsWithPolygonField()
    {
        var act = () => GeoPolygon.Normalize(null);

        act.Should().Throw<RequestValidationException>()
            .Which.Errors.Should().ContainKey(GeoPolygon.FieldName);
    }

    [Fact]
    public void Contains_PointWithoutLatitude_ReturnsFalse()
    {
        var ring = GeoPolygon.Normalize([[0, 0], [1, 0], [1, 1], [0, 1]]);

        GeoPolygon.Contains(ring, [0.5]).Should().BeFalse();
        GeoPolygon.Contains(ring, [0.5, 0.5]).Should().BeTrue();
    }

    [Fact]
    public void Filter_RequestValidationException_Returns400ValidationProblem()
    {
        var context = CreateExceptionContext(RequestValidationException.For("polygon", "Lỗi"));

        new RequestValidationExceptionFilter(new StubProblemDetailsFactory()).OnException(context);

        context.ExceptionHandled.Should().BeTrue();
        var result = context.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = result.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors["polygon"].Should().Equal("Lỗi");
    }

    [Fact]
    public void Filter_OtherException_IsIgnored()
    {
        var context = CreateExceptionContext(new InvalidOperationException());

        new RequestValidationExceptionFilter(new StubProblemDetailsFactory()).OnException(context);

        context.ExceptionHandled.Should().BeFalse();
        context.Result.Should().BeNull();
    }

    private static ExceptionContext CreateExceptionContext(Exception exception) =>
        new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), [])
        {
            Exception = exception
        };

    private sealed class StubProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(
            HttpContext httpContext, int? statusCode = null, string? title = null,
            string? type = null, string? detail = null, string? instance = null) =>
            new() { Status = statusCode, Title = title };

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext, ModelStateDictionary modelStateDictionary, int? statusCode = null,
            string? title = null, string? type = null, string? detail = null, string? instance = null) =>
            new(modelStateDictionary) { Status = statusCode };
    }
}
