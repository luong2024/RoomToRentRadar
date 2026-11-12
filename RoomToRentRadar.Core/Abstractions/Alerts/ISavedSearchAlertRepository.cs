using RoomToRentRadar.Models;

namespace RoomToRentRadar.Abstractions.Alerts
{
    /// <summary>
    /// Contract for US08 Saved Searches &amp; New-Listing Alerts. Implemented in T08.1. Consumed in T06.2, T08.2.
    /// </summary>
    public interface ISavedSearchAlertRepository
    {
        Task<List<SavedSearchAlert>> ListByUserAsync(int userId);
        Task<int> SaveAlertAsync(SavedSearchAlert alert);
        Task<bool> ToggleAlertAsync(int alertId, bool isEnabled);
        Task<bool> DeleteAlertAsync(int alertId);
    }
}
