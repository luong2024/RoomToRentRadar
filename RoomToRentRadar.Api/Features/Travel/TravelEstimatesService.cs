using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FetchLocks = new();

    public async Task<TravelResult<ListingTravelEstimatesDto>> GetEstimatesAsync(
        Guid listingId,
        string campusId,
        double campusLat,
        double campusLng,
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
                campusId,
                campusLat,
                campusLng,
                mode,
                cancellationToken)));

        return TravelResult<ListingTravelEstimatesDto>.Success(
            new ListingTravelEstimatesDto(modes));
    }

    private async Task<TravelEstimateDto> EstimateAsync(
        double fromLat,
        double fromLng,
        string campusId,
        double toLat,
        double toLng,
        TravelMode mode,
        CancellationToken cancellationToken)
    {
        // Coordinates are rounded to three decimals (~110 m) on purpose: nearby
        // requests share an estimate and the coarse key keeps the cache small.
        var cacheKey = FormattableString.Invariant(
            $"travel:{campusId}:{fromLat:F3}:{fromLng:F3}:{mode}");

        if (cache.TryGetValue(cacheKey, out TravelEstimateDto? cached) && cached is not null)
        {
            return cached;
        }

        // GetOrCreateAsync does not deduplicate concurrent async factories, so a
        // per-key gate keeps N simultaneous requests down to one provider call.
        var gate = FetchLocks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            if (cache.TryGetValue(cacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            var routed = await routingProvider.GetEstimateAsync(
                fromLat,
                fromLng,
                toLat,
                toLng,
                mode,
                cancellationToken)
                .ConfigureAwait(false);

            TravelEstimateDto estimate;
            TimeSpan lifetime;

            if (routed is not null)
            {
                estimate = new TravelEstimateDto(
                    mode,
                    TravelSource.Route,
                    routed.DistanceM,
                    routed.DurationS);
                lifetime = TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheMinutes));
            }
            else
            {
                // Short-lived negative cache: a failing provider is not retried on every request.
                estimate = new TravelEstimateDto(
                    mode,
                    TravelSource.Fallback,
                    (int)Math.Round(
                        GeoDistance.HaversineKm(fromLat, fromLng, toLat, toLng) * 1000d,
                        MidpointRounding.AwayFromZero),
                    null);
                lifetime = TimeSpan.FromSeconds(Math.Max(1, options.Value.FallbackCacheSeconds));
            }

            cache.Set(cacheKey, estimate, new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = lifetime
            });

            return estimate;
        }
        finally
        {
            gate.Release();
        }
    }
}
