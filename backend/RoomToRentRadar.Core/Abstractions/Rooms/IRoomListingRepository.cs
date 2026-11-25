using RoomToRentRadar.Models;

namespace RoomToRentRadar.Abstractions.Rooms
{
    /// <summary>
    /// Contract for US02, US03, US05, US06, US09 Room Listings Data.
    /// Implemented in T02.1, T03.1, T05.1, T09.2 (Infrastructure/Features/Rooms — one partial file per task).
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
}
