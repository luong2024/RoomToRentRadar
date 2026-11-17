using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Api.Data;
using RoomToRentRadar.Api.Domain;
using RoomToRentRadar.Api.Features.Listings;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.SavedSearches;
using RoomToRentRadar.Shared.Serialization;

namespace RoomToRentRadar.Api.Features.SavedSearches;

/// <summary>
/// One alert sweep: matches newly published listings against active saved
/// searches, then writes an inbox notification per match while honouring the
/// per-search daily cap and the (saved search, listing) idempotency rule.
/// </summary>
public sealed class SavedSearchAlertEvaluator(
    AppDbContext dbContext,
    IOptions<AlertsOptions> options,
    ILogger<SavedSearchAlertEvaluator> logger)
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public async Task<int> SweepAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var alertsOptions = options.Value;
        if (alertsOptions.DailyCap <= 0)
        {
            return 0;
        }

        var lookbackCutoff = utcNow.AddMinutes(-alertsOptions.LookbackMinutes);

        var searches = await dbContext.SavedSearches
            .AsNoTracking()
            .Where(search => search.AlertState == SavedSearchAlertState.Active)
            .Select(search => new
            {
                search.Id,
                search.UserId,
                search.Name,
                search.AreaJson,
                search.AlertsActivatedAt,
                search.CreatedAt
            })
            .ToListAsync(cancellationToken);
        if (searches.Count == 0)
        {
            return 0;
        }

        // The repo has no publish endpoint yet; a listing counts as newly
        // published when it is Published and its UpdatedAt falls in the window.
        var listings = await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == ListingStatus.Published && listing.UpdatedAt >= lookbackCutoff)
            .OrderBy(listing => listing.UpdatedAt)
            .ThenBy(listing => listing.Id)
            .Select(listing => new
            {
                listing.Id,
                listing.Title,
                listing.Price,
                listing.AreaM2,
                listing.Lat,
                listing.Lng,
                listing.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        if (listings.Count == 0)
        {
            return 0;
        }

        var searchIds = searches.Select(search => search.Id).ToList();
        var listingIds = listings.Select(listing => listing.Id).ToList();

        var alreadyAlerted = (await dbContext.SavedSearchAlertLogs
                .AsNoTracking()
                .Where(log => searchIds.Contains(log.SavedSearchId) && listingIds.Contains(log.ListingId))
                .Select(log => new { log.SavedSearchId, log.ListingId })
                .ToListAsync(cancellationToken))
            .Select(log => (log.SavedSearchId, log.ListingId))
            .ToHashSet();

        var (dayStartUtc, dayEndUtc) = AlertDay.GetUtcRange(utcNow);
        var deliveredToday = (await dbContext.SavedSearchAlertLogs
                .AsNoTracking()
                .Where(log => log.Delivered
                    && log.CreatedAt >= dayStartUtc
                    && log.CreatedAt < dayEndUtc
                    && searchIds.Contains(log.SavedSearchId))
                .GroupBy(log => log.SavedSearchId)
                .Select(group => new { SavedSearchId = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.SavedSearchId, row => row.Count);

        var created = 0;
        var changed = false;

        foreach (var search in searches)
        {
            var areaPolygon = JsonSerializer.Deserialize<AreaPolygon>(search.AreaJson, ApiJson.Options);
            if (!GeoSearch.TryCreateSearchArea(areaPolygon, out var area, out _))
            {
                logger.LogWarning(
                    "Saved search {SavedSearchId} has an unreadable area and was skipped.",
                    search.Id);
                continue;
            }

            var activatedAt = search.AlertsActivatedAt ?? search.CreatedAt;
            var delivered = deliveredToday.GetValueOrDefault(search.Id);

            foreach (var listing in listings)
            {
                if (listing.UpdatedAt < activatedAt || !area.Intersects(listing.Lng, listing.Lat))
                {
                    continue;
                }

                var key = (search.Id, listing.Id);
                if (alreadyAlerted.Contains(key))
                {
                    continue;
                }

                var deliver = delivered < alertsOptions.DailyCap;
                dbContext.SavedSearchAlertLogs.Add(new SavedSearchAlertLog
                {
                    Id = Guid.NewGuid(),
                    SavedSearchId = search.Id,
                    ListingId = listing.Id,
                    CreatedAt = utcNow,
                    Delivered = deliver
                });
                alreadyAlerted.Add(key);
                changed = true;

                if (!deliver)
                {
                    continue;
                }

                delivered++;
                created++;
                dbContext.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = search.UserId,
                    SavedSearchId = search.Id,
                    ListingId = listing.Id,
                    Title = $"Phòng mới trong “{search.Name}”",
                    Body = $"{listing.Title} · {listing.Price.ToString("N0", Vietnamese)} ₫ · "
                        + $"{listing.AreaM2.ToString("0.#", Vietnamese)} m²",
                    CreatedAt = utcNow
                });
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return created;
    }
}
