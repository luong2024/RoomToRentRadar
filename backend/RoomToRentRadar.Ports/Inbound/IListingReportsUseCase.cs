using RoomToRentRadar.Ports.Models.Reports;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.Ports.Inbound;

public interface IListingReportsUseCase
{
    Task<ListingReportResult> CreateAsync(
        Guid reporterId,
        Guid listingId,
        ListingReportReason reason,
        string? details,
        CancellationToken cancellationToken);
}
