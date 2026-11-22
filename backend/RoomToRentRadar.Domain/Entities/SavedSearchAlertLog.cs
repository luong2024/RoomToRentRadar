namespace RoomToRentRadar.Domain.Entities;

public sealed class SavedSearchAlertLog
{
    public Guid Id { get; set; }
    public Guid SavedSearchId { get; set; }
    public SavedSearch? SavedSearch { get; set; }
    public Guid ListingId { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// False when the match exceeded the daily alert cap; the row still blocks
    /// duplicate alerts for the same saved search and listing.
    /// </summary>
    public bool Delivered { get; set; }
}
