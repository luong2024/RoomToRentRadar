using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Models.Listings;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class ListingsQueryRepository(AppDbContext dbContext) : IListingsQueryRepository
{
    private static readonly Expression<Func<Listing, ListingSummaryDto>> SummaryProjection =
        x => new ListingSummaryDto(
            x.Id,
            x.Title,
            x.Price,
            x.AreaM2,
            x.Bedrooms,
            x.Address,
            x.Lat,
            x.Lng,
            x.CreatedAt,
            x.Photos
                .OrderByDescending(photo => photo.IsCover)
                .ThenBy(photo => photo.SortOrder)
                .ThenBy(photo => photo.Id)
                .Select(photo => photo.ThumbUrl ?? photo.Url)
                .FirstOrDefault());

    public async Task<int> CountPublishedAsync(
        ListingFilters filters,
        int cap,
        CancellationToken cancellationToken)
        => await ApplyFilters(Published(), filters)
            .Take(cap + 1)
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ListingSummaryDto>> GetPublishedPageAsync(
        ListingFilters filters,
        ListingSort sort,
        ListingCursor? cursor,
        int take,
        CancellationToken cancellationToken)
    {
        var listings = ApplyFilters(Published(), filters);
        var pageQuery = cursor is null ? listings : ApplyCursor(listings, cursor);
        pageQuery = ApplyOrdering(pageQuery, sort);

        return await pageQuery
            .Take(take)
            .Select(SummaryProjection)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ListingCandidate>> GetRadiusCandidatesAsync(
        ListingFilters filters,
        GeoBoundingBox box,
        CancellationToken cancellationToken)
        => await ApplyFilters(
                Published().Where(x => x.Lat >= box.MinLat
                    && x.Lat <= box.MaxLat
                    && x.Lng >= box.MinLng
                    && x.Lng <= box.MaxLng),
                filters)
            .Select(x => new ListingCandidate(
                x.Id,
                x.Title,
                x.Price,
                x.AreaM2,
                x.Bedrooms,
                x.Address,
                x.Lat,
                x.Lng,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ListingCandidate>> GetAreaCandidatesAsync(
        ListingFilters filters,
        GeoBoundingBox bounds,
        CancellationToken cancellationToken)
        => await ApplyFilters(
                Published().Where(x => x.Lat >= bounds.MinLat
                    && x.Lat <= bounds.MaxLat
                    && x.Lng >= bounds.MinLng
                    && x.Lng <= bounds.MaxLng),
                filters)
            .Select(x => new ListingCandidate(
                x.Id,
                x.Title,
                x.Price,
                x.AreaM2,
                x.Bedrooms,
                x.Address,
                x.Lat,
                x.Lng,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetCoverPhotoUrlsAsync(
        IReadOnlyCollection<Guid> listingIds,
        CancellationToken cancellationToken)
    {
        if (listingIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var photos = await dbContext.ListingPhotos
            .AsNoTracking()
            .Where(photo => listingIds.Contains(photo.ListingId))
            .OrderByDescending(photo => photo.IsCover)
            .ThenBy(photo => photo.SortOrder)
            .ThenBy(photo => photo.Id)
            .Select(photo => new { photo.ListingId, Url = photo.ThumbUrl ?? photo.Url })
            .ToListAsync(cancellationToken);

        var coverPhotos = new Dictionary<Guid, string>(photos.Count);
        foreach (var photo in photos)
        {
            // Photos are ordered, so the first row per listing is its cover.
            coverPhotos.TryAdd(photo.ListingId, photo.Url);
        }

        return coverPhotos;
    }

    public async Task<ListingDetailSnapshot?> GetDetailAsync(
        Guid listingId,
        CancellationToken cancellationToken)
        => await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Id == listingId)
            .Select(x => new ListingDetailSnapshot(
                x.Status,
                new ListingDetailDto(
                    x.Id,
                    x.Title,
                    x.Description,
                    x.Price,
                    x.Deposit,
                    x.AreaM2,
                    x.RoomType,
                    x.Bedrooms,
                    x.Address,
                    x.Lat,
                    x.Lng,
                    x.Status,
                    x.CreatedAt,
                    x.UpdatedAt,
                    x.Amenities
                        .OrderBy(amenity => amenity.SortOrder)
                        .ThenBy(amenity => amenity.Id)
                        .Select(amenity => amenity.Code)
                        .ToList(),
                    x.Rules
                        .OrderBy(rule => rule.SortOrder)
                        .ThenBy(rule => rule.Id)
                        .Select(rule => rule.Text)
                        .ToList(),
                    x.HouseRules == null
                        ? null
                        : new HouseRulesDto(
                            x.HouseRules.QuietHours,
                            x.HouseRules.GuestPolicy,
                            x.HouseRules.CookingRules,
                            x.HouseRules.ParkingRules,
                            x.HouseRules.Version,
                            x.HouseRules.UpdatedAt),
                    x.LandlordName)))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ListingContactSnapshot?> GetContactAsync(
        Guid listingId,
        CancellationToken cancellationToken)
        => await dbContext.Listings
            .AsNoTracking()
            .Where(candidate => candidate.Id == listingId)
            .Select(candidate => new ListingContactSnapshot(
                candidate.Status,
                new ListingContactDto(candidate.LandlordName, candidate.LandlordPhone)))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ListingSimilarSubject?> GetSimilarSubjectAsync(
        Guid listingId,
        CancellationToken cancellationToken)
        => await dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Id == listingId)
            .Select(listing => new ListingSimilarSubject(
                listing.Id,
                listing.Bedrooms,
                listing.Price,
                listing.AreaM2))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ListingSummaryDto>> GetSimilarAsync(
        ListingSimilarSubject subject,
        long minPrice,
        long maxPrice,
        double minAreaM2,
        double maxAreaM2,
        int limit,
        CancellationToken cancellationToken)
        => await Published()
            .Where(listing => listing.Id != subject.Id
                && listing.Bedrooms == subject.Bedrooms
                && listing.Price >= minPrice
                && listing.Price <= maxPrice
                && listing.AreaM2 >= minAreaM2
                && listing.AreaM2 <= maxAreaM2)
            .OrderBy(listing => Math.Abs(listing.Price - subject.Price))
            .ThenBy(listing => listing.Id)
            .Take(limit)
            .Select(SummaryProjection)
            .ToListAsync(cancellationToken);

    private IQueryable<Listing> Published()
        => dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Status == ListingStatus.Published);

    private static IQueryable<Listing> ApplyFilters(
        IQueryable<Listing> listings,
        ListingFilters filters)
    {
        if (filters.MinPrice is { } minPrice)
        {
            listings = listings.Where(x => x.Price >= minPrice);
        }

        if (filters.MaxPrice is { } maxPrice)
        {
            listings = listings.Where(x => x.Price <= maxPrice);
        }

        if (filters.MinAreaM2 is { } minAreaM2)
        {
            listings = listings.Where(x => x.AreaM2 >= minAreaM2);
        }

        if (filters.MaxAreaM2 is { } maxAreaM2)
        {
            listings = listings.Where(x => x.AreaM2 <= maxAreaM2);
        }

        if (filters.Bedrooms is { } bedrooms)
        {
            listings = listings.Where(x => x.Bedrooms == bedrooms);
        }

        if (filters.RoomType is { } roomType)
        {
            listings = listings.Where(x => x.RoomType == roomType);
        }

        return listings;
    }

    private static IQueryable<Listing> ApplyOrdering(IQueryable<Listing> query, ListingSort sort) => sort switch
    {
        ListingSort.Newest => query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id),
        ListingSort.PriceAsc => query
            .OrderBy(x => x.Price)
            .ThenBy(x => x.Id),
        ListingSort.PriceDesc => query
            .OrderByDescending(x => x.Price)
            .ThenBy(x => x.Id),
        ListingSort.AreaDesc => query
            .OrderByDescending(x => x.AreaM2)
            .ThenBy(x => x.Id),
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, null)
    };

    private static IQueryable<Listing> ApplyCursor(IQueryable<Listing> query, ListingCursor cursor)
    {
        var id = cursor.Id;

        return cursor.Sort switch
        {
            ListingSort.Newest => ApplyNewestCursor(query, cursor, id),
            ListingSort.PriceAsc => ApplyPriceCursor(query, cursor, id, ascending: true),
            ListingSort.PriceDesc => ApplyPriceCursor(query, cursor, id, ascending: false),
            ListingSort.AreaDesc => ApplyAreaCursor(query, cursor, id),
            _ => throw new ArgumentOutOfRangeException(nameof(cursor), cursor.Sort, null)
        };
    }

    private static IQueryable<Listing> ApplyNewestCursor(
        IQueryable<Listing> query,
        ListingCursor cursor,
        Guid id)
    {
        var createdAt = cursor.CreatedAt!.Value;
        return query.Where(x =>
            x.CreatedAt < createdAt
            || (x.CreatedAt == createdAt && x.Id.CompareTo(id) > 0));
    }

    private static IQueryable<Listing> ApplyPriceCursor(
        IQueryable<Listing> query,
        ListingCursor cursor,
        Guid id,
        bool ascending)
    {
        var price = cursor.Price!.Value;
        return ascending
            ? query.Where(x =>
                x.Price > price
                || (x.Price == price && x.Id.CompareTo(id) > 0))
            : query.Where(x =>
                x.Price < price
                || (x.Price == price && x.Id.CompareTo(id) > 0));
    }

    private static IQueryable<Listing> ApplyAreaCursor(
        IQueryable<Listing> query,
        ListingCursor cursor,
        Guid id)
    {
        var areaM2 = cursor.AreaM2!.Value;
        return query.Where(x =>
            x.AreaM2 < areaM2
            || (x.AreaM2 == areaM2 && x.Id.CompareTo(id) > 0));
    }
}
