using RoomToRentRadar.Api.DTOs;
using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Repositories;

public interface IListingRepository
{
    /// <summary>Lấy 1 trang phòng trọ đã sắp xếp và tổng số bản ghi (để tính metadata).</summary>
    Task<(IReadOnlyList<Listing> Items, int TotalRecords)> GetPagedAsync(
        ListingQueryParameters parameters,
        CancellationToken cancellationToken = default);
}
