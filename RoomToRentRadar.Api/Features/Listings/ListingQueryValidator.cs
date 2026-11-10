using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Listings;

public static class ListingQueryValidator
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;
    public const int MaxBedrooms = 20;

    public static bool TryCreate(
        ListingQueryParameters parameters,
        out ListingQuery query,
        out ProblemDetails? problem)
    {
        query = null!;
        problem = null;

        var limit = parameters.Limit ?? DefaultLimit;
        if (limit is < 1 or > MaxLimit)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidLimit,
                $"limit must be between 1 and {MaxLimit}.");
            return false;
        }

        var sort = ListingSort.Newest;
        if (parameters.Sort is not null && !ListingSortWire.TryParse(parameters.Sort, out sort))
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidSort,
                "sort must be one of: newest, price_asc, price_desc, area_desc.");
            return false;
        }

        if (parameters.MinPrice is < 0 || parameters.MaxPrice is < 0)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidPriceRange,
                "min_price and max_price must be non-negative.");
            return false;
        }

        if (parameters.MinPrice is { } minPrice
            && parameters.MaxPrice is { } maxPrice
            && minPrice > maxPrice)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidPriceRange,
                "min_price cannot be greater than max_price.");
            return false;
        }

        if (parameters.Bedrooms is < 0 or > MaxBedrooms)
        {
            problem = ListingProblemFactory.InvalidQuery(
                ListingProblemFactory.InvalidBedrooms,
                $"bedrooms must be between 0 and {MaxBedrooms}.");
            return false;
        }

        query = new ListingQuery(
            limit,
            string.IsNullOrWhiteSpace(parameters.Cursor) ? null : parameters.Cursor,
            sort,
            parameters.MinPrice,
            parameters.MaxPrice,
            parameters.Bedrooms);
        return true;
    }
}
