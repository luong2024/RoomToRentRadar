using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.UseCases.Reports;

public sealed class SubmitListingReportUseCase : ISubmitListingReportUseCase
{
    private readonly IRoomRepository _roomRepository;
    private readonly IListingReportRepository _reportRepository;
    private readonly IEnumerable<IReportSubmissionRule> _submissionRules;
    private readonly TimeProvider _timeProvider;

    public SubmitListingReportUseCase(
        IRoomRepository roomRepository,
        IListingReportRepository reportRepository,
        IEnumerable<IReportSubmissionRule> submissionRules,
        TimeProvider timeProvider)
    {
        _roomRepository = roomRepository ?? throw new ArgumentNullException(nameof(roomRepository));
        _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
        _submissionRules = submissionRules ?? throw new ArgumentNullException(nameof(submissionRules));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ListingReportSubmittedDto> ExecuteAsync(
        SubmitListingReportCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RoomId))
        {
            throw new RequestValidationException("roomId", "Room ID is required.");
        }

        var trimmedRoomId = command.RoomId.Trim();
        var room = await _roomRepository.GetByIdAsync(trimmedRoomId, cancellationToken)
            ?? throw new NotFoundException($"Room '{trimmedRoomId}' was not found.");

        foreach (var rule in _submissionRules)
        {
            await rule.ValidateAsync(room, command, cancellationToken);
        }

        var report = ListingReport.Create(
            roomId: room.Id,
            roomTitleSnapshot: room.Title,
            roomLandlordId: room.LandlordId,
            reporterUserId: command.ReporterUserId,
            reason: command.Reason,
            note: command.Note,
            utcNow: _timeProvider.GetUtcNow());

        await _reportRepository.AddAsync(report, cancellationToken);

        return new ListingReportSubmittedDto(
            report.Id,
            report.RoomId,
            report.Status,
            report.CreatedAtUtc);
    }
}
