using RoomToRentRadar.Application.DTOs;

namespace RoomToRentRadar.Application.Ports.Inbound;

public interface ISubmitListingReportUseCase
{
    Task<ListingReportSubmittedDto> ExecuteAsync(
        SubmitListingReportCommand command,
        CancellationToken cancellationToken = default);
}

public interface IGetModerationQueueUseCase
{
    Task<PagedResult<ModerationReportItemDto>> ExecuteAsync(
        GetModerationQueueQuery query,
        CancellationToken cancellationToken = default);
}

public interface IReviewListingReportUseCase
{
    Task<ModerationReportItemDto> ExecuteAsync(
        ReviewListingReportCommand command,
        CancellationToken cancellationToken = default);
}
