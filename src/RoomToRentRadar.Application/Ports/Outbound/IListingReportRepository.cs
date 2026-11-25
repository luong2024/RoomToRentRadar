using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Application.Ports.Outbound;

/// <summary>
/// Outbound persistence port for <see cref="ListingReport"/> aggregates.
/// </summary>
public interface IListingReportRepository
{
    Task<ListingReport?> GetByIdAsync(string reportId, string roomId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveReportByUserAsync(string roomId, Guid reporterUserId, CancellationToken cancellationToken = default);

    Task AddAsync(ListingReport report, CancellationToken cancellationToken = default);

    Task UpdateAsync(ListingReport report, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ListingReport> Items, int TotalCount)> GetModerationQueueAsync(
        ReportStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
