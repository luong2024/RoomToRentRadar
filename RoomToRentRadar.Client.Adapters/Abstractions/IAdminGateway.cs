using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Admin;
using RoomToRentRadar.Ports.Contracts.Requests.Reports;
using RoomToRentRadar.Ports.Contracts.Responses.Admin;
using RoomToRentRadar.Ports.Contracts.Responses.Auth;
using RoomToRentRadar.Ports.Contracts.Responses.Listings;
using RoomToRentRadar.Ports.Contracts.Responses.Reports;

namespace RoomToRentRadar.Client.Adapters.Abstractions;

/// <summary>Admin moderation (Admin tag of <c>docs/api/openapi.yaml</c>, UC-40..42, BR-LST-06, BR-RPT-04..05).</summary>
public interface IAdminGateway
{
    [ApiOperation("listPendingLandlords")]
    Task<PagedResult<PendingLandlordDto>> ListPendingLandlordsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken ct = default);

    [ApiOperation("approveLandlord")]
    Task<UserProfileDto> ApproveLandlordAsync(string userId, CancellationToken ct = default);

    [ApiOperation("listModerationRooms")]
    Task<PagedResult<ModerationRoomSummaryDto>> ListModerationRoomsAsync(ModerationQueue queue, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default);

    [ApiOperation("getModerationRoom")]
    Task<ModerationRoomDto> GetModerationRoomAsync(string roomId, CancellationToken ct = default);

    [ApiOperation("approveListing")]
    Task<MyListingDetailDto> ApproveListingAsync(string roomId, ApproveListingRequest request, long expectedVersion, CancellationToken ct = default);

    [ApiOperation("rejectListing")]
    Task<MyListingDetailDto> RejectListingAsync(string roomId, ReasonRequest request, long expectedVersion, CancellationToken ct = default);

    [ApiOperation("suspendListing")]
    Task<MyListingDetailDto> SuspendListingAsync(string roomId, ReasonRequest request, long expectedVersion, CancellationToken ct = default);

    [ApiOperation("acknowledgeListingChanges")]
    Task<MyListingDetailDto> AcknowledgeListingChangesAsync(string roomId, long expectedVersion, CancellationToken ct = default);

    [ApiOperation("listReports")]
    Task<PagedResult<ReportDto>> ListReportsAsync(ReportStatus? status = null, int pageNumber = 1, int pageSize = 20, CancellationToken ct = default);

    [ApiOperation("resolveReport")]
    Task<ReportDto> ResolveReportAsync(string reportId, ResolveReportRequest request, CancellationToken ct = default);
}