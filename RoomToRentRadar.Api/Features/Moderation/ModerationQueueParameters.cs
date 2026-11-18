using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Features.Moderation;

public sealed class ModerationQueueParameters
{
    [FromQuery(Name = "limit")]
    [Description("Maximum number of listing groups to return. Defaults to 20; valid range is 1-100.")]
    public int? Limit { get; init; }

    [FromQuery(Name = "cursor")]
    [Description("Opaque cursor from pagination.next_cursor.")]
    public string? Cursor { get; init; }
}
