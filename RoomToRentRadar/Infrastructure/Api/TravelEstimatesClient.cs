using System.Net;
using System.Net.Http.Json;
using RoomToRentRadar.Shared.Serialization;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Infrastructure.Api;

public sealed class TravelEstimatesClient(HttpClient httpClient) : ITravelEstimatesClient
{
    public async Task<TravelEstimatesFetchResult> GetEstimatesAsync(
        Guid listingId,
        double toLat,
        double toLng,
        CancellationToken cancellationToken = default)
    {
        var url = FormattableString.Invariant(
            $"api/v1/listings/{listingId}/travel?to_lat={toLat:R}&to_lng={toLng:R}");

        using var response = await httpClient.GetAsync(url, cancellationToken);

        // A listing that is gone is a normal UI state, not a transport failure.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new TravelEstimatesFetchResult(TravelEstimatesFetchOutcome.NotFound, null);
        }

        response.EnsureSuccessStatusCode();

        var estimates = await response.Content.ReadFromJsonAsync<ListingTravelEstimatesDto>(
            ApiJson.Options,
            cancellationToken);

        return estimates is null
            ? throw new InvalidOperationException("The travel endpoint returned an empty response.")
            : new TravelEstimatesFetchResult(TravelEstimatesFetchOutcome.Found, estimates);
    }
}
