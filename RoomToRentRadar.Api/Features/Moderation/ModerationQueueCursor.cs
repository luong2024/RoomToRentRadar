namespace RoomToRentRadar.Api.Features.Moderation;

/// <summary>
/// Position in the queue order: report count descending, then oldest report
/// ascending, then listing id ascending as the unique tie-breaker.
/// </summary>
public sealed record ModerationQueueCursor(int TotalReports, DateTime OldestReportAt, Guid ListingId);
