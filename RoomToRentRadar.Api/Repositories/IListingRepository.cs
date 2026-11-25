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

    /// <summary>US05 / T05.2 — tin Available + IsVerified của campus (tập ứng viên cho tìm theo polygon).</summary>
    Task<IReadOnlyList<ListingDocument>> GetPublicListingsAsync(
        string campusId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ListingDocument> Items, int TotalCount)> GetPagedAsync(
        ListingQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ListingDocument>> GetAllCanonicalAsync(
        CancellationToken cancellationToken = default);

    Task<ListingDocument> UpsertAsync(
        ListingDocument listing,
        CancellationToken cancellationToken = default);
}
