using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Ports.Contracts.Requests.Reports;
using RoomToRentRadar.Ports.Contracts.Responses.Reports;

namespace RoomToRentRadar.Client.Adapters.Http;

/// <summary>reportRoom arrives with T12.1; task-id stub until then (PLAN §5.4).</summary>
public sealed class HttpReportGateway : IReportGateway
{
    public HttpReportGateway(HttpClient httpClient, ILogger<HttpReportGateway>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    public Task<ReportCreatedDto> ReportRoomAsync(string roomId, CreateReportRequest request, CancellationToken ct = default) =>
        Pending.For<ReportCreatedDto>("T12.1", "reportRoom");
}