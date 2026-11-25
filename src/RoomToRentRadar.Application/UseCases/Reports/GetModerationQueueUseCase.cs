using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Domain.Entities;

namespace RoomToRentRadar.Application.UseCases.Reports;

public sealed class GetModerationQueueUseCase : IGetModerationQueueUseCase
{
    public const int MaxPageSize = 50;

    private readonly IListingReportRepository _reportRepository;

    public GetModerationQueueUseCase(IListingReportRepository reportRepository)
    {
        _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
    }

    public async Task<PagedResult<ModerationReportItemDto>> ExecuteAsync(
        GetModerationQueueQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => query.PageSize
        };

        var (items, totalCount) = await _reportRepository.GetModerationQueueAsync(
            query.Status,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(ToDto).ToList();
        return new PagedResult<ModerationReportItemDto>(dtos, totalCount, page, pageSize);
    }

    internal static ModerationReportItemDto ToDto(ListingReport report) =>
        new(
            report.Id,
            report.RoomId,
            report.RoomTitleSnapshot,
            report.RoomLandlordId,
            report.ReporterUserId,
            report.Reason,
            report.Note,
            report.Status,
            report.CreatedAtUtc,
            report.UpdatedAtUtc,
            report.ReviewedByUserId,
            report.ReviewedAtUtc,
            report.ResolutionNote,
            report.ETag);
}
