namespace RoomToRentRadar.Api.Models;

/// <summary>
/// Bản ghi lịch sử kiểm duyệt trong container ModerationReports của Cosmos DB (Partition Key: /listingId).
/// </summary>
public sealed class ModerationAuditDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string ListingId { get; set; } = string.Empty;

    public string DocumentType { get; set; } = "ModerationAudit";

    public string ModeratorId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string PreviousModerationStatus { get; set; } = string.Empty;

    public string NewModerationStatus { get; set; } = string.Empty;

    public string PreviousStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
