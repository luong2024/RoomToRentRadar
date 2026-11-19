using System.Net;
using System.Net.Http.Json;
using RoomToRentRadar.Shared.Serialization;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Infrastructure.Api;

public sealed class TravelEstimatesClient(HttpClient httpClient) : ITravelEstimatesClient
{
    public async Task<TravelEstimatesFetchResult> GetEstimatesAsync(
        Guid listingId,
        string campusId,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/listings/{listingId}/travel?campus_id={Uri.EscapeDataString(campusId)}";

        using var response = await httpClient.GetAsync(url, cancellationToken);

        // A listing that is gone is a normal UI state, not a transport failure.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new TravelEstimatesFetchResult(TravelEstimatesFetchOutcome.NotFound, null);
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return new TravelEstimatesFetchResult(
                TravelEstimatesFetchOutcome.RateLimited,
                null,
                ReadRetryAfter(response));
        }

        response.EnsureSuccessStatusCode();

        var estimates = await response.Content.ReadFromJsonAsync<ListingTravelEstimatesDto>(
            ApiJson.Options,
            cancellationToken);

        return estimates is null
            ? throw new InvalidOperationException("The travel endpoint returned an empty response.")
            : new TravelEstimatesFetchResult(TravelEstimatesFetchOutcome.Found, estimates);
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;

        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return null;
    }
}
