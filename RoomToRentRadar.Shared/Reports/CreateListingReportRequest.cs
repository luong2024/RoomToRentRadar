using System.ComponentModel;

namespace RoomToRentRadar.Shared.Reports;

/// <summary>Body of <c>POST /api/v1/listings/{id}/reports</c>.</summary>
public sealed class CreateListingReportRequest
{
    [Description("Report reason: spam, wrong_info, scam, offensive or already_rented.")]
    public string? Reason { get; init; }

    [Description("Optional details, at most 1000 characters after sanitization. HTML is stripped.")]
    public string? Details { get; init; }
}
