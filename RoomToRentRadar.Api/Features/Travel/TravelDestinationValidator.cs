using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Travel;

public static class TravelDestinationValidator
{
    public static bool TryValidate(
        double? toLat,
        double? toLng,
        out double lat,
        out double lng,
        out ProblemDetails? problem)
    {
        lat = 0d;
        lng = 0d;

        if (toLat is not { } latitude || toLng is not { } longitude)
        {
            problem = TravelProblemFactory.InvalidDestinationProblem(
                "Both to_lat and to_lng are required.");
            return false;
        }

        if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
        {
            problem = TravelProblemFactory.InvalidDestinationProblem(
                "to_lat and to_lng must be finite numbers.");
            return false;
        }

        if (latitude is < -90d or > 90d || longitude is < -180d or > 180d)
        {
            problem = TravelProblemFactory.InvalidDestinationProblem(
                "to_lat must be between -90 and 90 and to_lng between -180 and 180.");
            return false;
        }

        lat = latitude;
        lng = longitude;
        problem = null;
        return true;
    }
}
