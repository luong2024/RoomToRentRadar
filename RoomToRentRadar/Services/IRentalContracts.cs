using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services
{
    // ============================================================================
    // SPRINT 1 SHARED CONTRACTS (MANAGED BY LEAD DEV QUANG / @architect)
    // Pure interfaces & lightweight shared session state so Frontend and Backend
    // tasks can be developed in parallel and connect automatically upon Git merge.
    // ============================================================================

    /// <summary>
    /// Contract for US01 Authentication & User Session (Implemented in T01.1, Consumed in T01.2, T03.2, T09.1).
    /// </summary>
    public interface IAuthService
    {
        UserAccount? CurrentUser { get; }
        bool IsSignedIn { get; }
        Task<UserAccount?> LoginAsync(string email, string password);
        Task<UserAccount?> RegisterAsync(string fullName, string email, string password, string phone, string role);
        void Logout();
    }

    /// <summary>
    /// Contract for US02, US03, US05, US06, US09 Room Listings Data (Implemented in T02.1, T03.1, T05.1, T09.2).
    /// </summary>
    public interface IRoomListingRepository
    {
        Task<List<RoomListing>> ListVerifiedRoomsAsync(int pageIndex = 0, int pageSize = 20, string sortBy = "DistanceAsc");
        Task<RoomListing?> GetByIdAsync(int roomId);
        Task<List<RoomListing>> GetSimilarRoomsAsync(int excludeRoomId, string ward, decimal targetRent, int limit = 3);
        Task<List<RoomListing>> SearchAsync(RoomFilterCriteria criteria);
        Task<int> SaveListingAsync(RoomListing listing);
        Task<bool> UpdateStatusAsync(int roomId, string status, bool isVerified);
    }

    /// <summary>
    /// Contract for US07 Favorite Rooms (Implemented in T07.1, Consumed in T02.2, T03.2, T07.2).
    /// </summary>
    public interface IFavoriteRepository
    {
        Task<List<RoomListing>> GetFavoriteRoomsAsync(int userId);
        Task<bool> ToggleFavoriteAsync(int userId, int roomId);
        Task<bool> AddFavoriteAsync(int userId, int roomId);
        Task<bool> RemoveFavoriteAsync(int userId, int roomId);
    }

    /// <summary>
    /// Contract for US08 Saved Searches & New-Listing Alerts (Implemented in T08.1, Consumed in T06.2, T08.2).
    /// </summary>
    public interface ISavedSearchAlertRepository
    {
        Task<List<SavedSearchAlert>> ListByUserAsync(int userId);
        Task<int> SaveAlertAsync(SavedSearchAlert alert);
        Task<bool> ToggleAlertAsync(int alertId, bool isEnabled);
        Task<bool> DeleteAlertAsync(int alertId);
    }

    /// <summary>
    /// Contract for US12 Versioned House Rules (Implemented in T12.1, Consumed in T12.2).
    /// </summary>
    public interface IHouseRuleRepository
    {
        Task<List<HouseRuleVersion>> GetHistoryByRoomAsync(int roomId);
        Task<int> SaveNewVersionAsync(HouseRuleVersion ruleVersion);
    }

    /// <summary>
    /// Contract for US13 Listing Reports & Moderation Queue (Implemented in T13.1, Consumed in T13.2).
    /// </summary>
    public interface IListingReportRepository
    {
        Task<int> SubmitReportAsync(ListingReport report);
        Task<List<ListingReport>> ListPendingReportsAsync();
    }

    /// <summary>
    /// Shared state holder for US11 Side-by-Side Room Comparison (up to 3 rooms).
    /// Allows RoomListPage (T02.2) and RoomDetailPage (T03.2) to add rooms to compare in RoomComparePage (T11.1).
    /// </summary>
    public class ComparisonBasketService
    {
        public const int MaxComparisonRooms = 3;
        private readonly List<RoomListing> _selectedRooms = new();

        public IReadOnlyList<RoomListing> SelectedRooms => _selectedRooms;

        public bool TryAddRoom(RoomListing room, out string message)
        {
            if (_selectedRooms.Any(r => r.Id == room.Id))
            {
                message = "Phòng này đã có trong danh sách so sánh.";
                return false;
            }

            if (_selectedRooms.Count >= MaxComparisonRooms)
            {
                message = $"Chỉ được so sánh tối đa {MaxComparisonRooms} phòng cùng lúc.";
                return false;
            }

            _selectedRooms.Add(room);
            message = $"Đã thêm vào so sánh ({_selectedRooms.Count}/{MaxComparisonRooms}).";
            return true;
        }

        public void RemoveRoom(int roomId)
        {
            _selectedRooms.RemoveAll(r => r.Id == roomId);
        }

        public void Clear() => _selectedRooms.Clear();
    }
}
