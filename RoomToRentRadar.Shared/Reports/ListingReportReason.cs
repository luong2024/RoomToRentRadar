using System.Text.Json.Serialization;

namespace RoomToRentRadar.Shared.Reports;

public enum ListingReportReason
{
    [JsonStringEnumMemberName("spam")]
    Spam,

    [JsonStringEnumMemberName("wrong_info")]
    WrongInfo,

    [JsonStringEnumMemberName("scam")]
    Scam,

    [JsonStringEnumMemberName("offensive")]
    Offensive,

    [JsonStringEnumMemberName("already_rented")]
    AlreadyRented
}

/// <summary>Wire values for <see cref="ListingReportReason"/>.</summary>
public static class ListingReportReasonWire
{
    public const string Spam = "spam";
    public const string WrongInfo = "wrong_info";
    public const string Scam = "scam";
    public const string Offensive = "offensive";
    public const string AlreadyRented = "already_rented";

    public static readonly IReadOnlyList<string> All =
        [Spam, WrongInfo, Scam, Offensive, AlreadyRented];

    public static bool TryParse(string? value, out ListingReportReason reason)
    {
        switch (value)
        {
            case Spam:
                reason = ListingReportReason.Spam;
                return true;
            case WrongInfo:
                reason = ListingReportReason.WrongInfo;
                return true;
            case Scam:
                reason = ListingReportReason.Scam;
                return true;
            case Offensive:
                reason = ListingReportReason.Offensive;
                return true;
            case AlreadyRented:
                reason = ListingReportReason.AlreadyRented;
                return true;
            default:
                reason = default;
                return false;
        }
    }

    public static string ToWireValue(ListingReportReason reason) => reason switch
    {
        ListingReportReason.Spam => Spam,
        ListingReportReason.WrongInfo => WrongInfo,
        ListingReportReason.Scam => Scam,
        ListingReportReason.Offensive => Offensive,
        ListingReportReason.AlreadyRented => AlreadyRented,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };

    public static ListingReportReason FromWireValue(string value)
        => TryParse(value, out var reason)
            ? reason
            : throw new InvalidOperationException($"Unknown listing report reason '{value}'.");
}
