using RoomToRentRadar.Models;

namespace RoomToRentRadar.Abstractions.Favorites
{
    /// <summary>
    /// Contract for US07 Favorite Rooms. Implemented in T07.1. Consumed in T02.2, T03.2, T07.2.
    /// </summary>
    public interface IFavoriteRepository
    {
        Task<List<RoomListing>> GetFavoriteRoomsAsync(int userId);
        Task<bool> ToggleFavoriteAsync(int userId, int roomId);
        Task<bool> AddFavoriteAsync(int userId, int roomId);
        Task<bool> RemoveFavoriteAsync(int userId, int roomId);
    }
}
