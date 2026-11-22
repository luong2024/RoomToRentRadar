namespace RoomToRentRadar.Application.UseCases.RoomListings;

using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.Validators;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Domain.ValueObjects;

public sealed class SearchRoomsInPolygonUseCase : ISearchRoomsInPolygonUseCase
{
    private readonly IRoomListingRepository _roomListingRepository;

    public SearchRoomsInPolygonUseCase(IRoomListingRepository roomListingRepository)
    {
        _roomListingRepository = roomListingRepository ?? throw new ArgumentNullException(nameof(roomListingRepository));
    }

    public async Task<PagedResult<SpatialRoomSearchResultDto>> ExecuteAsync(
        SpatialRoomSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // 1. Structural request validation
        SearchRoomsInPolygonQueryValidator.Validate(request);

        // 2. Build and validate Domain GeoPolygon (handles CCW winding, closure, non-degeneracy >= 400 m²)
        GeoPolygon polygon;
        try
        {
            var domainCoordinates = request.PolygonCoordinates
                .Select(c => new GeoLocation(c.Latitude, c.Longitude))
                .ToList();

            polygon = new GeoPolygon(domainCoordinates);
        }
        catch (InvalidPolygonException ex)
        {
            throw new ValidationException(nameof(request.PolygonCoordinates), ex.Message);
        }

        // 3. Formulate repository filter
        var filter = new RoomSearchFilter(
            MinPrice: request.MinPrice,
            MaxPrice: request.MaxPrice,
            RoomType: request.RoomType,
            Amenities: request.Amenities,
            SortBy: request.SortBy,
            Descending: request.Descending);

        // 4. Query persistence via outbound port
        var pagedListings = await _roomListingRepository.SearchByPolygonAsync(
            polygon,
            filter,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        // 5. Map to presentation DTO with coordinates for map pin display
        var dtos = pagedListings.Items.Select(item => new SpatialRoomSearchResultDto(
            Id: item.Id,
            Title: item.Title,
            MonthlyRent: item.MonthlyRent.Amount,
            Currency: item.MonthlyRent.Currency,
            AreaSquareMeters: item.AreaSquareMeters,
            Address: item.Address,
            Latitude: item.Location.Latitude,
            Longitude: item.Location.Longitude,
            IsVerified: item.VerificationStatus == Domain.Enums.VerificationStatus.Verified,
            ThumbnailUrl: item.ThumbnailUrl,
            CampusId: item.CampusId
        )).ToList();

        return new PagedResult<SpatialRoomSearchResultDto>(
            Items: dtos,
            Page: pagedListings.Page,
            PageSize: pagedListings.PageSize,
            TotalCount: pagedListings.TotalCount);
    }
}
