namespace RoomToRentRadar.Services.Navigation.Core;

/// <summary>
/// Builds the role-aware bottom tab set (docs/ui-ux-spec.md §2.1, max 5 tabs). Guest and Student
/// share the same set; an unknown role falls back to Student. Pure C# for unit testing (T00.15).
/// </summary>
public static class RoleTabsProvider
{
    private static readonly TabDescriptor ExploreTab =
        new(AppRoutes.Explore, "Nav_Explore", "Nav_Hint_Explore", TabIcon.Explore, RequiresSignIn: false);

    private static readonly TabDescriptor MapTab =
        new(AppRoutes.Map, "Nav_Map", "Nav_Hint_Map", TabIcon.Map, RequiresSignIn: false);

    private static readonly TabDescriptor SavedTab =
        new(AppRoutes.Saved, "Nav_Saved", "Nav_Hint_Saved", TabIcon.Saved, RequiresSignIn: true);

    private static readonly TabDescriptor MyListingsTab =
        new(AppRoutes.MyListings, "Nav_Listings", "Nav_Hint_Listings", TabIcon.Listings, RequiresSignIn: false);

    private static readonly TabDescriptor MessagesTab =
        new(AppRoutes.Messages, "Nav_Messages", "Nav_Hint_Messages", TabIcon.Messages, RequiresSignIn: true);

    private static readonly TabDescriptor ModerationTab =
        new(AppRoutes.Moderation, "Nav_Moderation", "Nav_Hint_Moderation", TabIcon.Moderation, RequiresSignIn: false);

    private static readonly TabDescriptor AccountTab =
        new(AppRoutes.Account, "Nav_Account", "Nav_Hint_Account", TabIcon.Account, RequiresSignIn: false);

    private static readonly IReadOnlyList<TabDescriptor> GuestStudentTabs =
        new[] { ExploreTab, MapTab, SavedTab, MessagesTab, AccountTab };

    private static readonly IReadOnlyList<TabDescriptor> LandlordTabs =
        new[] { ExploreTab, MyListingsTab, MessagesTab, AccountTab };

    private static readonly IReadOnlyList<TabDescriptor> AdminTabs =
        new[] { ExploreTab, MapTab, ModerationTab, MessagesTab, AccountTab };

    public static IReadOnlyList<TabDescriptor> GetTabs(string? role) =>
        NavigationPolicy.NormalizeRole(role) switch
        {
            "Landlord" => LandlordTabs,
            "Admin" => AdminTabs,
            _ => GuestStudentTabs
        };
}