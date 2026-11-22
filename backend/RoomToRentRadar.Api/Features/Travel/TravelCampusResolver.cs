using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Shared.Campuses;

namespace RoomToRentRadar.Api.Features.Travel;

public static class TravelCampusResolver
{
    public static bool TryResolve(
        string? campusId,
        out CampusInfo campus,
        out ProblemDetails? problem)
    {
        if (CampusCatalog.FindById(campusId) is not { } match)
        {
            campus = null!;
            problem = TravelProblemFactory.InvalidDestinationProblem(
                $"campus_id is required and must be one of: {string.Join(", ", CampusCatalog.All.Select(item => item.Id))}.");
            return false;
        }

        campus = match;
        problem = null;
        return true;
    }
}
