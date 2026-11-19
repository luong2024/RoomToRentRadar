using RoomToRentRadar.Application.Mappers;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Domain.Exceptions;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Landlord;
using RoomToRentRadar.Ports.Contracts.Responses.Listings;
using RoomToRentRadar.Ports.Inbound.UseCases.Landlord;
using RoomToRentRadar.Ports.Outbound.ExternalServices;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Application.UseCases.Landlord;

/// <summary>
/// <c>operationId: listMyListings</c> (UC-31, BR-LST-11): one page of the caller's listings ordered by
/// last update, plus the per-status counters of SCR-LL-01 (always all six statuses). Cover thumbnails
/// are expanded to public URLs; a room without photos keeps <c>coverThumbUrl = null</c> (BR-MED-05).
/// </summary>
public sealed class ListMyListingsUseCase : IListMyListingsUseCase
{
    /// <summary>openapi <c>PageSize</c>: minimum 1, maximum 50.</summary>
    public const int MaxPageSize = 50;

    private readonly IRoomRepository _roomRepository;
    private readonly IPhotoStoragePort _storage;

    public ListMyListingsUseCase(IRoomRepository roomRepository, IPhotoStoragePort storage)
    {
        ArgumentNullException.ThrowIfNull(roomRepository);
        ArgumentNullException.ThrowIfNull(storage);

        _roomRepository = roomRepository;
        _storage = storage;
    }

    public async Task<PagedMyListings> ExecuteAsync(
        ListMyListingsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.LandlordId))
        {
            throw new DomainValidationException("The landlord is required.");
        }

        if (request.PageNumber < 1)
        {
            throw new DomainValidationException("pageNumber must be at least 1.", ProblemCodes.ValidationFailed);
        }

        if (request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            throw new DomainValidationException(
                $"pageSize must be between 1 and {MaxPageSize}.",
                ProblemCodes.ValidationFailed);
        }

        if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
        {
            throw new DomainValidationException(
                $"Listing status '{(int)request.Status.Value}' is not a recognized status.",
                ProblemCodes.ValidationFailed);
        }

        var page = await _roomRepository
            .ListByLandlordAsync(request.LandlordId, request.Status, request.PageNumber, request.PageSize, ct)
            .ConfigureAwait(false);

        return new PagedMyListings(
            Items: page.Items.Select(listing => LandlordListingMapper.ToSummary(listing, _storage)).ToArray(),
            TotalCount: page.TotalCount,
            PageNumber: request.PageNumber,
            PageSize: request.PageSize,
            CountsByStatus: Enum.GetValues<ListingStatus>()
                .ToDictionary(
                    status => status,
                    status => page.CountsByStatus.TryGetValue(status, out var count) ? count : 0));
    }
}
