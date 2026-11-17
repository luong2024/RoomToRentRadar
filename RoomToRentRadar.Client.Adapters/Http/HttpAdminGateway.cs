using Microsoft.Extensions.Logging;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Admin;
using RoomToRentRadar.Ports.Contracts.Requests.Reports;
using RoomToRentRadar.Ports.Contracts.Responses.Admin;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Listings;
using RoomToRentRadar.Ports.Contracts.Responses.Reports;

namespace RoomToRentRadar.Client.Adapters.Http;

/// <summary>
/// Admin stubs (PLAN §5.4): moderation T08.4, reports T12.3, pending landlords T13.5.
/// </summary>
public sealed class HttpAdminGateway : IAdminGateway
{
    public HttpAdminGateway(HttpClient httpClient, ILogger<HttpAdminGateway>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    public Task<PagedResult<PendingLandlordDto>> ListPendingLandlordsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default) =>
        Pending.For<PagedResult<PendingLandlordDto>>("T13.5", "listPendingLandlords");

    public Task<UserProfileDto> ApproveLandlordAsync(string userId, CancellationToken ct = default) =>
        Pending.For<UserProfileDto>("T13.5", "approveLandlord");

    public Task<PagedResult<ModerationRoomSummaryDto>> ListModerationRoomsAsync(ModerationQueue queue, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default) =>
        Pending.For<PagedResult<ModerationRoomSummaryDto>>("T08.4", "listModerationRooms");

    public Task<ModerationRoomDto> GetModerationRoomAsync(string roomId, CancellationToken ct = default) =>
        Pending.For<ModerationRoomDto>("T08.4", "getModerationRoom");

    public Task<MyListingDetailDto> ApproveListingAsync(string roomId, ApproveListingRequest request, long expectedVersion, CancellationToken ct = default) =>
        Pending.For<MyListingDetailDto>("T08.4", "approveListing");

    public Task<MyListingDetailDto> RejectListingAsync(string roomId, ReasonRequest request, long expectedVersion, CancellationToken ct = default) =>
        Pending.For<MyListingDetailDto>("T08.4", "rejectListing");

    public Task<MyListingDetailDto> SuspendListingAsync(string roomId, ReasonRequest request, long expectedVersion, CancellationToken ct = default) =>
        Pending.For<MyListingDetailDto>("T08.4", "suspendListing");

    public Task<MyListingDetailDto> AcknowledgeListingChangesAsync(string roomId, long expectedVersion, CancellationToken ct = default) =>
        Pending.For<MyListingDetailDto>("T08.4", "acknowledgeListingChanges");

    public Task<PagedResult<ReportDto>> ListReportsAsync(ReportStatus? status = null, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default) =>
        Pending.For<PagedResult<ReportDto>>("T12.3", "listReports");

    public Task<ReportDto> ResolveReportAsync(string reportId, ResolveReportRequest request, CancellationToken ct = default) =>
        Pending.For<ReportDto>("T12.3", "resolveReport");
}