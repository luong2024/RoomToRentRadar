using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Models.Moderation;

namespace RoomToRentRadar.Api.Features.Moderation;

public static class ModerationQueueQueryValidator
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public static bool TryCreate(
        ModerationQueueParameters parameters,
        out ModerationQueueQuery query,
        out ProblemDetails? problem)
    {
        query = null!;
        problem = null;

        var limit = parameters.Limit ?? DefaultLimit;
        if (limit is < 1 or > MaxLimit)
        {
            problem = ModerationProblemFactory.InvalidQuery(
                ModerationProblemFactory.InvalidLimit,
                $"limit must be between 1 and {MaxLimit}.");
            return false;
        }

        query = new ModerationQueueQuery(
            limit,
            string.IsNullOrWhiteSpace(parameters.Cursor) ? null : parameters.Cursor);
        return true;
    }
}
