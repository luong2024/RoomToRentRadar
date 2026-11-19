namespace RoomToRentRadar.Services.Navigation.Core;

/// <summary>
/// One placeholder destination: the screen id (docs/ui-ux-spec.md), the feature task that will
/// replace it, and the AppResources title key. Shared by every not-yet-built route (T00.15).
/// </summary>
public sealed record PlaceholderEntry(string Route, string ScrId, string TaskId, string TitleKey);

/// <summary>
/// Maps the 12 detail routes plus the my-listings and moderation tabs to their placeholder metadata.
/// Adding a route is one line here + two resx keys; a feature task swaps in a real page by changing
/// only its route registration. Pure C# for unit testing.
/// </summary>
public static class PlaceholderCatalog
{
    private static readonly Dictionary<string, PlaceholderEntry> Entries =
        new Dictionary<string, PlaceholderEntry>(StringComparer.Ordinal)
        {
            [AppRoutes.RoomDetail] = Entry(AppRoutes.RoomDetail, "SCR-ROOM-01", "T02.1"),
            [AppRoutes.RoomGallery] = Entry(AppRoutes.RoomGallery, "SCR-ROOM-02", "T03.1"),
            [AppRoutes.SearchFilters] = Entry(AppRoutes.SearchFilters, "SCR-SRCH-01", "T04.1"),
            [AppRoutes.Compare] = Entry(AppRoutes.Compare, "SCR-CMP-01", "T10.1"),
            [AppRoutes.ChatConversation] = Entry(AppRoutes.ChatConversation, "SCR-CHAT-02", "T14.1"),
            [AppRoutes.SavedSearches] = Entry(AppRoutes.SavedSearches, "SCR-SAVED-01", "T07.1"),
            [AppRoutes.Notifications] = Entry(AppRoutes.Notifications, "SCR-NOTI-01", "T07.2"),
            [AppRoutes.AccountCampus] = Entry(AppRoutes.AccountCampus, "SCR-ACC-02", "T01.1"),
            [AppRoutes.AccountLanguage] = Entry(AppRoutes.AccountLanguage, "SCR-ACC-03", "T15.1"),
            [AppRoutes.ListingEditor] = Entry(AppRoutes.ListingEditor, "SCR-LL-02", "T08.1"),
            [AppRoutes.AdminReview] = Entry(AppRoutes.AdminReview, "SCR-ADM-02", "T08.4"),
            [AppRoutes.AdminReport] = Entry(AppRoutes.AdminReport, "SCR-ADM-03", "T12.3"),
            [AppRoutes.MyListings] = Entry(AppRoutes.MyListings, "SCR-LL-01", "T08.1"),
            [AppRoutes.Moderation] = Entry(AppRoutes.Moderation, "SCR-ADM-01", "T13.5")
        };

    /// <summary>All catalogued destinations (12 detail routes + 2 tabs).</summary>
    public static IReadOnlyCollection<PlaceholderEntry> All => Entries.Values;

    /// <summary>The entry for a route, or null when the route is not a placeholder destination.</summary>
    public static PlaceholderEntry? Find(string route) =>
        Entries.TryGetValue(route, out var entry) ? entry : null;

    private static PlaceholderEntry Entry(string route, string scrId, string taskId) =>
        new(route, scrId, taskId, $"Placeholder_{scrId}_Title");
}