
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Features.Rooms
{
    public partial class RoomDetailPageModel : ObservableObject, IQueryAttributable
    {
        private readonly IRoomListingRepository _roomRepository;

        [ObservableProperty]
        public partial RoomListing? Room { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial string ErrorMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string StatusText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string MonthlyRentText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string DepositText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string AreaText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string AmenitiesText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string DistanceText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string VerifiedText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string StatusColor { get; set; } = "#808080";

        public RoomDetailPageModel(IRoomListingRepository roomRepository)
        {
            _roomRepository = roomRepository;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("roomId", out var value) &&
                int.TryParse(Convert.ToString(value), out int roomId) &&
                roomId > 0)
            {
                _ = LoadRoomAsync(roomId);
            }
            else
            {
                ErrorMessage = "Không tìm thấy mã phòng hợp lệ.";
            }
        }

        [RelayCommand]
        public async Task LoadRoomAsync(int roomId)
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                ErrorMessage = string.Empty;

                Room = await _roomRepository.GetByIdAsync(roomId);

                if (Room == null)
                {
                    ErrorMessage = "Không tìm thấy thông tin phòng.";
                    return;
                }

                UpdateDisplayProperties();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Không thể tải thông tin phòng: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void UpdateDisplayProperties()
        {
            if (Room == null)
                return;

            var culture = new CultureInfo("vi-VN");

            MonthlyRentText = Room.MonthlyRent.ToString("N0", culture) + " đ/tháng";
            DepositText = Room.Deposit.ToString("N0", culture) + " đ";
            AreaText = Room.Area.ToString("0.##", culture) + " m²";

            AmenitiesText = string.IsNullOrWhiteSpace(Room.AmenitiesCsv)
                ? "Chưa cập nhật tiện ích"
                : Room.AmenitiesCsv.Replace(",", " • ");

            DistanceText = Room.DistanceToCampusKm.ToString("0.##", culture) + " km đến ICTU";

            VerifiedText = Room.IsVerified ? "Đã xác minh" : "Chưa xác minh";

            switch (Room.Status)
            {
                case "Available":
                    StatusText = "Đang cho thuê";
                    StatusColor = "#2E9D62";
                    break;

                case "PendingReview":
                    StatusText = "Đang chờ duyệt";
                    StatusColor = "#E5A000";
                    break;

                case "Unavailable":
                    StatusText = "Đã hết phòng";
                    StatusColor = "#D9534F";
                    break;

                default:
                    StatusText = "Chưa xác định";
                    StatusColor = "#808080";
                    break;
            }
        }
    }
}
