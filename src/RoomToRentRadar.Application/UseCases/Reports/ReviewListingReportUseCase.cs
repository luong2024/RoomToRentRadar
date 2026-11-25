using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;

namespace RoomToRentRadar.Application.UseCases.Reports;

public sealed class ReviewListingReportUseCase : IReviewListingReportUseCase
{
    private readonly IListingReportRepository _reportRepository;
    private readonly TimeProvider _timeProvider;

    public ReviewListingReportUseCase(
        IListingReportRepository reportRepository,
        TimeProvider timeProvider)
    {
        _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ModerationReportItemDto> ExecuteAsync(
        ReviewListingReportCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.ReportId))
        {
            throw new RequestValidationException("reportId", "Report ID is required.");
        }

        if (string.IsNullOrWhiteSpace(command.RoomId))
        {
            throw new RequestValidationException("roomId", "Room ID is required.");
        }

        var trimmedReportId = command.ReportId.Trim();
        var trimmedRoomId = command.RoomId.Trim();

        var report = await _reportRepository.GetByIdAsync(trimmedReportId, trimmedRoomId, cancellationToken)
            ?? throw new NotFoundException($"Listing report '{trimmedReportId}' was not found.");

        if (!string.IsNullOrWhiteSpace(command.ExpectedETag) &&
            !string.Equals(report.ETag, command.ExpectedETag, StringComparison.Ordinal))
        {
            throw new ConcurrencyConflictException(
                $"Listing report '{trimmedReportId}' was modified concurrently. Please refresh and retry.");
        }

        report.ApplyReviewDecision(
            command.TargetStatus,
            command.ReviewerUserId,
            command.ResolutionNote,
            _timeProvider.GetUtcNow());

        await _reportRepository.UpdateAsync(report, cancellationToken);
        return GetModerationQueueUseCase.ToDto(report);
    }
}
