namespace RoomToRentRadar.Shared.Listings;

/// <summary>Wire values for <see cref="ListingStatus"/>.</summary>
public static class ListingStatusWire
{
    public const string Draft = "draft";
    public const string PendingReview = "pending_review";
    public const string Published = "published";
    public const string Hidden = "hidden";
    public const string Removed = "removed";

    public static readonly IReadOnlyList<string> All =
        [Draft, PendingReview, Published, Hidden, Removed];

    public static bool TryParse(string? value, out ListingStatus status)
    {
        switch (value)
        {
            case Draft:
                status = ListingStatus.Draft;
                return true;
            case PendingReview:
                status = ListingStatus.PendingReview;
                return true;
            case Published:
                status = ListingStatus.Published;
                return true;
            case Hidden:
                status = ListingStatus.Hidden;
                return true;
            case Removed:
                status = ListingStatus.Removed;
                return true;
            default:
                status = default;
                return false;
        }
    }

    public static string ToWireValue(ListingStatus status) => status switch
    {
        ListingStatus.Draft => Draft,
        ListingStatus.PendingReview => PendingReview,
        ListingStatus.Published => Published,
        ListingStatus.Hidden => Hidden,
        ListingStatus.Removed => Removed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static ListingStatus FromWireValue(string value)
        => TryParse(value, out var status)
            ? status
            : throw new InvalidOperationException($"Unknown listing status '{value}'.");
}
