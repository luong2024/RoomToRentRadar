using RoomToRentRadar.Models.RoomDetails;

namespace RoomToRentRadar.Services.RoomDetails;

public interface IListingReportService
{
    Task<SubmitListingReportResult> SubmitReportAsync(
        string roomId,
        ReportReasonCode reason,
        string? note,
        CancellationToken cancellationToken = default);
}
