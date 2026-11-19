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
        // Coordinates are rounded to three decimals (~110 m) on purpose: nearby
        // requests share an estimate and the coarse key keeps the cache small.
        var cacheKey = FormattableString.Invariant(
            $"travel:{fromLat:F3}:{fromLng:F3}:{toLat:F3}:{toLng:F3}:{mode}");

        var estimate = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.Size = 1;

            var routed = await routingProvider.GetEstimateAsync(
                fromLat,
                fromLng,
                toLat,
                toLng,
                mode,
                cancellationToken);

            if (routed is not null)
            {
                entry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheMinutes));
                return new TravelEstimateDto(
                    mode,
                    TravelSource.Route,
                    routed.DistanceM,
                    routed.DurationS);
            }

            // Short-lived negative cache: a failing provider is not retried on every request.
            entry.AbsoluteExpirationRelativeToNow =
                TimeSpan.FromSeconds(Math.Max(1, options.Value.FallbackCacheSeconds));

            return new TravelEstimateDto(
                mode,
                TravelSource.Fallback,
                (int)Math.Round(
                    GeoDistance.HaversineKm(fromLat, fromLng, toLat, toLng) * 1000d,
                    MidpointRounding.AwayFromZero),
                null);
        });

        return estimate!;
    }
}
