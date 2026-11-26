using RoomToRentRadar.Models;

namespace RoomToRentRadar.Services
{
    /// <summary>
    /// Shared state holder for US11 Side-by-Side Room Comparison (up to 3 rooms).
    /// Allows RoomListPage (T02.2) and RoomDetailPage (T03.2) to add rooms to compare in RoomComparePage (T11.1).
    /// Registered as Singleton by the Composition Root.
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
