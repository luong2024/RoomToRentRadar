using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Favourites;

public static class FavouriteProblemFactory
{
    public const string ListingNotFound = "listing_not_found";

    public static ProblemDetails NotFoundProblem()
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            Status = StatusCodes.Status404NotFound,
            Title = "Listing not found",
            Detail = "No published listing exists with the requested identifier."
        };
        problem.Extensions["code"] = ListingNotFound;
        return problem;
    }
}
