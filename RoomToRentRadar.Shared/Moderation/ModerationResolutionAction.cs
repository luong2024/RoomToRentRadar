using System.Text.Json.Serialization;

namespace RoomToRentRadar.Shared.Moderation;

public enum ModerationResolutionAction
{
    [JsonStringEnumMemberName("dismiss")]
    Dismiss,

    [JsonStringEnumMemberName("hide_listing")]
    HideListing,

    [JsonStringEnumMemberName("remove_listing")]
    RemoveListing,

    [JsonStringEnumMemberName("warn_owner")]
    WarnOwner
}

/// <summary>Wire values for <see cref="ModerationResolutionAction"/>.</summary>
public static class ModerationResolutionActionWire
{
    public const string Dismiss = "dismiss";
    public const string HideListing = "hide_listing";
    public const string RemoveListing = "remove_listing";
    public const string WarnOwner = "warn_owner";

    public static readonly IReadOnlyList<string> All =
        [Dismiss, HideListing, RemoveListing, WarnOwner];

    public static bool TryParse(string? value, out ModerationResolutionAction action)
    {
        switch (value)
        {
            case Dismiss:
                action = ModerationResolutionAction.Dismiss;
                return true;
            case HideListing:
                action = ModerationResolutionAction.HideListing;
                return true;
            case RemoveListing:
                action = ModerationResolutionAction.RemoveListing;
                return true;
            case WarnOwner:
                action = ModerationResolutionAction.WarnOwner;
                return true;
            default:
                action = default;
                return false;
        }
    }

    public static string ToWireValue(ModerationResolutionAction action) => action switch
    {
        ModerationResolutionAction.Dismiss => Dismiss,
        ModerationResolutionAction.HideListing => HideListing,
        ModerationResolutionAction.RemoveListing => RemoveListing,
        ModerationResolutionAction.WarnOwner => WarnOwner,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    public static ModerationResolutionAction FromWireValue(string value)
        => TryParse(value, out var action)
            ? action
            : throw new InvalidOperationException($"Unknown moderation resolution action '{value}'.");
}
