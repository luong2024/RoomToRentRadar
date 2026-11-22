using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Shared.Reports;

namespace RoomToRentRadar.SqliteDataAccess.Repositories;

public sealed class ListingReportRepository(AppDbContext dbContext) : IListingReportRepository
{
    public Task<bool> HasActiveReportAsync(
        Guid listingId,
        Guid reporterId,
        CancellationToken cancellationToken)
        => dbContext.ListingReports.AnyAsync(
            report => report.ListingId == listingId
                && report.ReporterId == reporterId
                && (report.Status == ListingReportStatus.Open
                    || report.Status == ListingReportStatus.InReview),
            cancellationToken);

    public async Task<IReadOnlyList<DateTime>> GetRecentReportTimesAsync(
        Guid reporterId,
        DateTime windowStart,
        CancellationToken cancellationToken)
        => await dbContext.ListingReports
            .AsNoTracking()
            .Where(report => report.ReporterId == reporterId && report.CreatedAt >= windowStart)
            .OrderBy(report => report.CreatedAt)
            .Select(report => report.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> TryAddAsync(ListingReport report, CancellationToken cancellationToken)
    {
        dbContext.ListingReports.Add(report);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            dbContext.Entry(report).State = EntityState.Detached;
            return false;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19, // SQLITE_CONSTRAINT
            SqliteExtendedErrorCode: 2067 // SQLITE_CONSTRAINT_UNIQUE
        };
}
