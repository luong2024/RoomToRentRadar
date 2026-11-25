using System.Globalization;
using RoomToRentRadar.Shared.Moderation;

namespace RoomToRentRadar.Models;

public sealed class ModerationQueueItem(ModerationListingDto listing)
{
    public Guid Id { get; } = listing.Id;

    public string Title { get; } = listing.Title;

    public string StatusText { get; } = "Chờ duyệt";

    public string CreatedText { get; } = string.Create(
        CultureInfo.InvariantCulture,
        $"Gửi lúc {listing.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}");
}
