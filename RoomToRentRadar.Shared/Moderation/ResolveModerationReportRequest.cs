using System.ComponentModel;

namespace RoomToRentRadar.Shared.Moderation;

/// <summary>Body of <c>POST /api/v1/moderation/reports/{id}/resolve</c>.</summary>
public sealed class ResolveModerationReportRequest
{
    [Description("Resolution: dismiss, hide_listing, remove_listing or warn_owner.")]
    public string? Action { get; init; }

    [Description("Required moderation note, at most 1000 characters.")]
    public string? Note { get; init; }
}
