using Microsoft.AspNetCore.Mvc;
using RoomToRentRadar.Ports.Models.Moderation;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Api.Features.Moderation;

public static class ModerationListingQueueQueryValidator
{
    public const ListingStatus DefaultStatus = ListingStatus.PendingReview;

    public static bool TryCreate(
        ModerationListingQueueParameters parameters,
        out ModerationListingQueueQuery query,
        out ProblemDetails? problem)
    {
        query = null!;
        problem = null;

        var status = DefaultStatus;
        if (parameters.Status is not null
            && !ListingStatusWire.TryParse(parameters.Status.Trim(), out status))
        {
            problem = ModerationProblemFactory.InvalidQuery(
                ModerationProblemFactory.InvalidStatus,
                $"status must be one of: {string.Join(", ", ListingStatusWire.All)}.");
            return false;
        }

        var limit = parameters.Limit ?? ModerationQueueQueryValidator.DefaultLimit;
        if (limit is < 1 or > ModerationQueueQueryValidator.MaxLimit)
        {
            problem = ModerationProblemFactory.InvalidQuery(
                ModerationProblemFactory.InvalidLimit,
                $"limit must be between 1 and {ModerationQueueQueryValidator.MaxLimit}.");
            return false;
        }

        query = new ModerationListingQueueQuery(
            status,
            limit,
            string.IsNullOrWhiteSpace(parameters.Cursor) ? null : parameters.Cursor);
        return true;
    }
}
