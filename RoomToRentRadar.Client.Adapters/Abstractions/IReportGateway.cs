using RoomToRentRadar.Ports.Contracts.Requests.Reports;
using RoomToRentRadar.Ports.Contracts.Responses.Reports;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>Listing reports submitted by students (reportRoom of <c>docs/api/openapi.yaml</c>, US12, BR-RPT-01..03).</summary>
public interface IReportGateway
{
    [ApiOperation("reportRoom")]
    Task<ReportCreatedDto> ReportRoomAsync(string roomId, CreateReportRequest request, CancellationToken ct = default);
}