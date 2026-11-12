using RoomToRentRadar.Api.Models;
using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Repositories;

public interface IListingRepository
{
    Task<ListingDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingDocument>> GetSimilarCandidatesAsync(
        string campusId,
        string excludeId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ListingDocument> Items, int TotalCount)> SearchAsync(
        ListingSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ListingDocument> Items, int TotalCount)> GetPagedAsync(
        ListingQueryRequest request,
        CancellationToken cancellationToken = default);
}
