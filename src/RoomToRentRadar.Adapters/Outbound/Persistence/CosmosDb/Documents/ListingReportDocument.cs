using Newtonsoft.Json;
using RoomToRentRadar.Domain.Entities;
using RoomToRentRadar.Domain.Enums;

namespace RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Documents;

public sealed class ListingReportDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [JsonProperty("roomTitleSnapshot")]
    public string RoomTitleSnapshot { get; set; } = string.Empty;

    [JsonProperty("roomLandlordId")]
    public Guid? RoomLandlordId { get; set; }

    [JsonProperty("reporterUserId")]
    public Guid ReporterUserId { get; set; }

    [JsonProperty("reason")]
    public int Reason { get; set; }

    [JsonProperty("note")]
    public string? Note { get; set; }

    [JsonProperty("status")]
    public int Status { get; set; }

    [JsonProperty("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [JsonProperty("updatedAtUtc")]
    public DateTimeOffset UpdatedAtUtc { get; set; }

    [JsonProperty("reviewedByUserId")]
    public Guid? ReviewedByUserId { get; set; }

    [JsonProperty("reviewedAtUtc")]
    public DateTimeOffset? ReviewedAtUtc { get; set; }

    [JsonProperty("resolutionNote")]
    public string? ResolutionNote { get; set; }

    [JsonProperty("_etag")]
    public string? ETag { get; set; }

    public static ListingReportDocument FromDomain(ListingReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new ListingReportDocument
        {
            Id = report.Id,
            RoomId = report.RoomId,
            RoomTitleSnapshot = report.RoomTitleSnapshot,
            RoomLandlordId = report.RoomLandlordId,
            ReporterUserId = report.ReporterUserId,
            Reason = (int)report.Reason,
            Note = report.Note,
            Status = (int)report.Status,
            CreatedAtUtc = report.CreatedAtUtc,
            UpdatedAtUtc = report.UpdatedAtUtc,
            ReviewedByUserId = report.ReviewedByUserId,
            ReviewedAtUtc = report.ReviewedAtUtc,
            ResolutionNote = report.ResolutionNote,
            ETag = report.ETag
        };
    }

    public ListingReport ToDomain() =>
        ListingReport.Rehydrate(
            Id,
            RoomId,
            RoomTitleSnapshot,
            RoomLandlordId,
            ReporterUserId,
            (ReportReason)Reason,
            Note,
            (ReportStatus)Status,
            CreatedAtUtc,
            UpdatedAtUtc,
            ReviewedByUserId,
            ReviewedAtUtc,
            ResolutionNote,
            ETag);
}
