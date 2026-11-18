using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.Travel;

namespace RoomToRentRadar.Api.Features.Travel;

public sealed class TravelEstimatesService(
    AppDbContext dbContext,
    IRoutingEstimatesProvider routingProvider,
    IMemoryCache cache,
    IOptions<TravelOptions> options)
{
    public async Task<TravelResult<ListingTravelEstimatesDto>> GetEstimatesAsync(
        Guid listingId,
        double toLat,
        double toLng,
        CancellationToken cancellationToken)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Id == listingId)
            .Select(x => new { x.Status, x.Lat, x.Lng })
            .SingleOrDefaultAsync(cancellationToken);

        if (listing is null || listing.Status != ListingStatus.Published)
        {
            return TravelResult<ListingTravelEstimatesDto>.Failure(ListingProblemFactory.NotFound());
        }

        var modes = await Task.WhenAll(
            Enum.GetValues<TravelMode>().Select(mode => EstimateAsync(
                listing.Lat,
                listing.Lng,
                toLat,
                toLng,
                mode,
                cancellationToken)));

        return TravelResult<ListingTravelEstimatesDto>.Success(
            new ListingTravelEstimatesDto(modes));
    }

    private async Task<TravelEstimateDto> EstimateAsync(
        double fromLat,
        double fromLng,
        double toLat,
        double toLng,
        TravelMode mode,
        CancellationToken cancellationToken)
    {
        var cacheKey = FormattableString.Invariant(
            $"travel:{fromLat:F3}:{fromLng:F3}:{toLat:F3}:{toLng:F3}:{mode}");

        if (cache.TryGetValue(cacheKey, out TravelEstimateDto? cached) && cached is not null)
        {
            return cached;
        }

        var estimate = await routingProvider.GetEstimateAsync(
            fromLat,
            fromLng,
            toLat,
            toLng,
            mode,
            cancellationToken);

        if (estimate is not null)
        {
            var routed = new TravelEstimateDto(
                mode,
                TravelSource.Route,
                estimate.DistanceM,
                estimate.DurationS);
            cache.Set(cacheKey, routed, TimeSpan.FromMinutes(options.Value.CacheMinutes));
            return routed;
        }

        var fallback = new TravelEstimateDto(
            mode,
            TravelSource.Fallback,
            (int)Math.Round(
                GeoDistance.HaversineKm(fromLat, fromLng, toLat, toLng) * 1000d,
                MidpointRounding.AwayFromZero),
            null);

        // Negative cache: keep repeated requests from hammering a failing provider.
        if (options.Value.FallbackCacheSeconds > 0)
        {
            cache.Set(cacheKey, fallback, TimeSpan.FromSeconds(options.Value.FallbackCacheSeconds));
        }

        return fallback;
    }
}
