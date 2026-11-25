using RoomToRentRadar.Shared.Dtos;

namespace RoomToRentRadar.Api.Services.Moderation;

public interface IListingModerationService
{
    Task<ModerationQueueResponse> GetModerationQueueAsync(
        string? moderationStatus = null,
        string? campusId = null,
        CancellationToken cancellationToken = default);

    Task<ModerationDecisionResponse> ModerateListingAsync(
        string listingId,
        string moderatorId,
        ModerationDecisionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModerationAuditLogDto>> GetModerationHistoryAsync(
        string listingId,
        CancellationToken cancellationToken = default);

    Task<LandlordListingsResponse> GetLandlordListingsAsync(
        string landlordId,
        string? status = null,
        string? moderationStatus = null,
        CancellationToken cancellationToken = default);

    Task<UpdateListingAvailabilityResponse> UpdateLandlordAvailabilityAsync(
        string listingId,
        string landlordId,
        UpdateListingAvailabilityRequest request,
        CancellationToken cancellationToken = default);

    Task<ArchiveListingResponse> ArchiveLandlordListingAsync(
        string listingId,
        string landlordId,
        CancellationToken cancellationToken = default);
}
