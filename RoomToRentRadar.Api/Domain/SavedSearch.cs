using RoomToRentRadar.Shared.SavedSearches;

namespace RoomToRentRadar.Api.Domain;

public sealed class SavedSearch
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AreaJson { get; set; } = string.Empty;
    public SavedSearchAlertState AlertState { get; set; }
    public DateTime? AlertsActivatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<SavedSearchAlertLog> AlertLogs { get; set; } = [];
}
