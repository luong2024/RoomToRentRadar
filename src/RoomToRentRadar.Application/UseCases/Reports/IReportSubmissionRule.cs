using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Exceptions;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Exceptions;

namespace RoomToRentRadar.Application.UseCases.Reports;

/// <summary>
/// Extensible validation rule evaluated before accepting a listing report (Open/Closed Principle).
/// New submission rules can be added via DI without modifying <see cref="SubmitListingReportUseCase"/>.
/// </summary>
public interface IReportSubmissionRule
{
    Task ValidateAsync(
        Room room,
        SubmitListingReportCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class PreventSelfReportRule : IReportSubmissionRule
{
    public Task ValidateAsync(
        Room room,
        SubmitListingReportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (room.LandlordId.HasValue && room.LandlordId.Value == command.ReporterUserId)
        {
            throw new ReportDomainException("Landlords cannot report their own listing.");
        }

        return Task.CompletedTask;
    }
}

public sealed class PreventDuplicateActiveReportRule : IReportSubmissionRule
{
    private readonly IListingReportRepository _reportRepository;

    public PreventDuplicateActiveReportRule(IListingReportRepository reportRepository)
    {
        _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
    }

    public async Task ValidateAsync(
        Room room,
        SubmitListingReportCommand command,
        CancellationToken cancellationToken = default)
    {
        var hasActiveReport = await _reportRepository.HasActiveReportByUserAsync(
            room.Id,
            command.ReporterUserId,
            cancellationToken);

        if (hasActiveReport)
        {
            throw new DuplicateReportException(
                "You have already submitted an active report for this listing that is awaiting moderation.");
        }
    }
}
