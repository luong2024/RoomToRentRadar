using RoomToRentRadar.Api.DTOs;
using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Api.Repositories;

namespace RoomToRentRadar.Api.Services;

public class ListingService(IListingRepository repository) : IListingService
{
    public async Task<PagedResponse<ListingDto>> GetListingsAsync(ListingQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var (items, totalRecords) = await repository.GetPagedAsync(parameters, cancellationToken);

        // Map entity -> DTO chỉ trên PageSize phần tử đã được DB lọc sẵn.
        var dtos = items.Select(ToDto).ToList();

        return PagedResponse<ListingDto>.Create(dtos, totalRecords, parameters.PageNumber, parameters.PageSize);
    }

    private static ListingDto ToDto(Listing l) => new(
        l.Id,
        l.Title,
        l.Price,
        l.Area,
        l.Address,
        l.District,
        l.City,
        l.ImageUrls.FirstOrDefault(),
        l.IsAvailable,
        l.CreatedAt);
}
