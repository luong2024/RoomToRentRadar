using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;

namespace RoomToRentRadar.Services.Search.Core;

public sealed record FilterValidationResult(
    string? PriceErrorCode,
    string? AreaErrorCode,
    string? DistanceErrorCode,
    string? SortErrorCode)
{
    public static FilterValidationResult Valid { get; } = new(null, null, null, null);

    public bool IsValid =>
        PriceErrorCode is null &&
        AreaErrorCode is null &&
        DistanceErrorCode is null &&
        SortErrorCode is null;
}

/// <summary>Client-side guards for BR-SRCH-02 / UC-05 E1: block invalid ranges before calling the API.</summary>
public static class FilterRangeValidator
{
    public const long MinRentBoundVnd = 0;
    public const long MaxRentBoundVnd = 10_000_000;
    public const long RentStepVnd = 100_000;

    public const double MinAreaBoundM2 = 5.0;
    public const double MaxAreaBoundM2 = 100.0;
    public const double AreaStepM2 = 1.0;

    public static IReadOnlyList<double> DistanceOptions { get; } = new[] { 1.0, 2.0, 3.0, 5.0, 10.0 };

    public static IReadOnlyList<long> RentPresets { get; } = new[] { 1_500_000L, 2_000_000L, 3_000_000L };

    public static FilterValidationResult Validate(
        long? minRent,
        long? maxRent,
        double? minArea,
        double? maxArea,
        double? maxDistance,
        string? sort)
    {
        string? priceError = null;
        if (minRent is < 0 || maxRent is < 0 || (minRent.HasValue && maxRent.HasValue && minRent > maxRent))
            priceError = ProblemCodes.InvalidPriceRange;

        string? areaError = null;
        if (IsInvalidArea(minArea) ||
            IsInvalidArea(maxArea) ||
            (minArea.HasValue && maxArea.HasValue && minArea > maxArea))
            areaError = ProblemCodes.InvalidAreaRange;

        string? distanceError = null;
        if (maxDistance.HasValue &&
            (double.IsNaN(maxDistance.Value) ||
             double.IsInfinity(maxDistance.Value) ||
             maxDistance.Value < RoomSearchRequest.MinDistanceKm ||
             maxDistance.Value > RoomSearchRequest.MaxDistanceKmAllowed))
            distanceError = ProblemCodes.InvalidDistanceRange;

        string? sortError = null;
        if (!string.IsNullOrWhiteSpace(sort) &&
            !RoomSearchRequest.AllowedSorts.Contains(sort, StringComparer.Ordinal))
            sortError = ProblemCodes.InvalidSortOption;

        return new FilterValidationResult(priceError, areaError, distanceError, sortError);
    }

    private static bool IsInvalidArea(double? area) =>
        area.HasValue &&
        (double.IsNaN(area.Value) || double.IsInfinity(area.Value) || area.Value < 0);
}
