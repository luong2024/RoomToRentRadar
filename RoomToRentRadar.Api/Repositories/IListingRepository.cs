using RoomToRentRadar.Api.Models;

namespace RoomToRentRadar.Api.Repositories;

public interface IListingRepository
{
    Task<ListingDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingDocument>> GetSimilarCandidatesAsync(
        string campusId,
        string excludeId,
        CancellationToken cancellationToken = default);
}
