using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Moderation;

public sealed class ModerationListingQueueParameters
{
    [FromQuery(Name = "status")]
    [Description("Listing status to review. Defaults to pending_review; valid values are draft, pending_review, published, hidden and removed.")]
    public string? Status { get; init; }

    [FromQuery(Name = "limit")]
    [Description("Maximum number of listings to return. Defaults to 20; valid range is 1-100.")]
    public int? Limit { get; init; }

    [FromQuery(Name = "cursor")]
    [Description("Opaque cursor from pagination.next_cursor.")]
    public string? Cursor { get; init; }
}
