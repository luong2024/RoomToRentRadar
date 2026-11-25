namespace RoomToRentRadar.Services.Navigation.Core;

/// <summary>
/// Canonical Shell route names (docs/ui-ux-spec.md §2.1). Pure C# so the role/tab logic
/// can be unit-tested without a MAUI host (T00.15).
/// </summary>
public static partial class AppRoutes
{
    // Bottom tabs (§2.1). "main" was renamed to "explore".
    public const string Explore = "explore";
    public const string Map = "map";
    public const string Saved = "saved";
    public const string MyListings = "my-listings";
    public const string Messages = "messages";
    public const string Moderation = "moderation";
    public const string Account = "account";

    // Auth flow.
    public const string Login = "login";
    public const string Register = "register";
    public const string RoleSelection = "roleselection";

    // Detail routes (12). ROOM-03/ROOM-04/LL-03 are out of scope for T00.15.
    public const string RoomDetail = "room/detail";
    public const string RoomGallery = "room/gallery";
    public const string SearchFilters = "search/filters";
    public const string Compare = "compare";
    public const string ChatConversation = "chat/conversation";
    public const string SavedSearches = "saved-searches";
    public const string Notifications = "notifications";
    // Must not start with a tab route: MAUI reads "account/campus" as a path into the Account tab and throws.
    public const string AccountCampus = "account-campus";
    public const string AccountLanguage = "account/language";
    public const string ListingEditor = "listings/editor";
    public const string AdminReview = "admin/review";
    public const string AdminReport = "admin/report";

    public static readonly IReadOnlyList<string> DetailRoutes = new[]
    {
        RoomDetail,
        RoomGallery,
        SearchFilters,
        Compare,
        ChatConversation,
        SavedSearches,
        Notifications,
        AccountCampus,
        AccountLanguage,
        ListingEditor,
        AdminReview,
        AdminReport
    };
}