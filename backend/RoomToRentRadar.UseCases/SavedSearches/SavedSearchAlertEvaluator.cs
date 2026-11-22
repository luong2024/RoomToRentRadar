using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Inbound;
using RoomToRentRadar.Ports.Models.SavedSearches;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.Shared.SavedSearches;
using RoomToRentRadar.Shared.Serialization;
using RoomToRentRadar.UseCases.Listings;

namespace RoomToRentRadar.UseCases.SavedSearches;

/// <summary>
/// One alert sweep: matches newly published listings against active saved
/// searches, then writes an inbox notification per match while honouring the
/// per-search daily cap and the (saved search, listing) idempotency rule.
/// </summary>
public sealed class SavedSearchAlertEvaluator(
    ISavedSearchRepository savedSearchRepository,
    IListingLookupRepository listingRepository,
    ISavedSearchAlertLogRepository alertLogRepository,
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork,
    IOptions<AlertsOptions> options,
    ILogger<SavedSearchAlertEvaluator> logger) : ISavedSearchAlertUseCase
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

        var searches = await savedSearchRepository.GetActiveAsync(cancellationToken);
        if (searches.Count == 0)
        {
            return 0;
        }

        // The repo has no publish endpoint yet; a listing counts as newly
        // published when it is Published and its UpdatedAt falls in the window.
        var listings = await listingRepository.GetPublishedUpdatedSinceAsync(
            lookbackCutoff,
            cancellationToken);
        if (listings.Count == 0)
        {
            return 0;
        }

        var searchIds = searches.Select(search => search.Id).ToList();
        var listingIds = listings.Select(listing => listing.Id).ToList();

        var alreadyAlerted = await alertLogRepository.GetAlertedPairsAsync(
            searchIds,
            listingIds,
            cancellationToken);

        var (dayStartUtc, dayEndUtc) = AlertDay.GetUtcRange(utcNow);
        var deliveredToday = await alertLogRepository.GetDeliveredCountsAsync(
            dayStartUtc,
            dayEndUtc,
            searchIds,
            cancellationToken);

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
                alertLogRepository.Add(new SavedSearchAlertLog
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
                notificationRepository.Add(new Notification
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
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return created;
    }
}
