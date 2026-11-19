namespace RoomToRentRadar.Services.Navigation.Core;

/// <summary>Icon glyph family for a bottom tab (mapped to FluentUI glyphs by the control).</summary>
public enum TabIcon
{
    Explore,
    Map,
    Saved,
    Listings,
    Messages,
    Moderation,
    Account
}

/// <summary>
/// One role-aware bottom tab. <paramref name="TitleKey"/> and <paramref name="HintKey"/> are
/// AppResources keys (en-US + vi-VN); <paramref name="RequiresSignIn"/> marks tabs that show a
/// sign-in prompt to guests (Saved, Messages — BR-AUTH-07).
/// </summary>
public sealed record TabDescriptor(
    string Route,
    string TitleKey,
    string HintKey,
    TabIcon Icon,
    bool RequiresSignIn);