using System.Text.Json.Serialization;

namespace RoomToRentRadar.Shared.Reports;

public enum ListingReportStatus
{
    [JsonStringEnumMemberName("open")]
    Open,

    [JsonStringEnumMemberName("in_review")]
    InReview,

    [JsonStringEnumMemberName("resolved")]
    Resolved,

    [JsonStringEnumMemberName("dismissed")]
    Dismissed
}

/// <summary>Wire values for <see cref="ListingReportStatus"/>.</summary>
public static class ListingReportStatusWire
{
    public const string Open = "open";
    public const string InReview = "in_review";
    public const string Resolved = "resolved";
    public const string Dismissed = "dismissed";

    public static readonly IReadOnlyList<string> All = [Open, InReview, Resolved, Dismissed];

    public static bool TryParse(string? value, out ListingReportStatus status)
    {
        switch (value)
        {
            case Open:
                status = ListingReportStatus.Open;
                return true;
            case InReview:
                status = ListingReportStatus.InReview;
                return true;
            case Resolved:
                status = ListingReportStatus.Resolved;
                return true;
            case Dismissed:
                status = ListingReportStatus.Dismissed;
                return true;
            default:
                status = default;
                return false;
        }
    }

    public static string ToWireValue(ListingReportStatus status) => status switch
    {
        ListingReportStatus.Open => Open,
        ListingReportStatus.InReview => InReview,
        ListingReportStatus.Resolved => Resolved,
        ListingReportStatus.Dismissed => Dismissed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static ListingReportStatus FromWireValue(string value)
        => TryParse(value, out var status)
            ? status
            : throw new InvalidOperationException($"Unknown listing report status '{value}'.");
}
