using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Features.RoomDetails.Models;
using RoomToRentRadar.Features.RoomDetails.Services;

namespace RoomToRentRadar.Features.RoomDetails.ViewModels;

public partial class RoomDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IRoomDetailService _roomDetailService;
    private readonly IErrorHandler _errorHandler;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string RoomId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial RoomDetailModel? Room { get; set; }

    [ObservableProperty]
    public partial bool IsUserLoggedIn { get; set; } = false; // Giả lập trạng thái auth

    [ObservableProperty]
    public partial List<SimilarRoomModel> SimilarRooms { get; set; } = [];

    // Trạng thái hiển thị (AC2, AC3)
    public bool IsAvailable => Room?.Status == ListingStatus.Available;
    public bool IsRented => Room?.Status == ListingStatus.Rented;
    public bool IsUnavailable => Room?.Status == ListingStatus.Unavailable;
    public bool CanBookOrContact => IsAvailable;
    public bool ShowSimilarRooms => IsRented || IsUnavailable;

    // Trạng thái xem số điện thoại (AC4)
    public bool CanViewPhoneNumber => IsUserLoggedIn && !string.IsNullOrWhiteSpace(Room?.LandlordPhone);
    public string DisplayPhoneNumber => CanViewPhoneNumber ? Room!.LandlordPhone! : "Đăng nhập để xem số điện thoại";

    public RoomDetailViewModel(IRoomDetailService roomDetailService, IErrorHandler errorHandler)
    {
        _roomDetailService = roomDetailService;
        _errorHandler = errorHandler;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idObj) && idObj is string id)
        {
            RoomId = id;
            LoadRoomDetailsCommand.Execute(null);
        }
    }

    [RelayCommand]
    public async Task LoadRoomDetailsAsync()
    {
        if (string.IsNullOrWhiteSpace(RoomId))
            RoomId = "default-1"; // ID mặc định khi chạy test màn hình

        try
        {
            IsBusy = true;
            Room = await _roomDetailService.GetRoomDetailAsync(RoomId);

            if (Room is null)
            {
                await Shell.Current.DisplayAlert("Thông báo", "Không tìm thấy phòng trọ này.", "OK");
                return;
            }

            // Cập nhật các computed properties
            NotifyStatusPropertiesChanged();

            // Nếu phòng không khả dụng hoặc đã thuê, tự động tải phòng tương tự (AC3)
            if (ShowSimilarRooms)
            {
                SimilarRooms = await _roomDetailService.GetSimilarRoomsAsync(RoomId);
            }
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleAuthSimulation()
    {
        // Nút toggle để bạn và giảng viên test AC4 ngay trên UI
        IsUserLoggedIn = !IsUserLoggedIn;
        OnPropertyChanged(nameof(CanViewPhoneNumber));
        OnPropertyChanged(nameof(DisplayPhoneNumber));
    }

    [RelayCommand]
    private async Task SelectSimilarRoomAsync(SimilarRoomModel similarRoom)
    {
        if (similarRoom is null) return;
        await Shell.Current.GoToAsync($"{AppRoutes.RoomDetail}?id={similarRoom.Id}");
    }

    [RelayCommand]
    private async Task BookInspectionAsync()
    {
        if (!CanBookOrContact) return;
        await Shell.Current.DisplayAlert("Đặt lịch hẹn", $"Bạn muốn đặt lịch xem {Room?.Title}?", "Xác nhận", "Hủy");
    }

    [RelayCommand]
    private async Task CallLandlordAsync()
    {
        if (!IsUserLoggedIn)
        {
            await Shell.Current.DisplayAlert("Yêu cầu đăng nhập", "Vui lòng đăng nhập để xem và gọi số chủ trọ.", "Đóng");
            return;
        }

        if (PhoneDialer.Default.IsSupported && !string.IsNullOrWhiteSpace(Room?.LandlordPhone))
        {
            PhoneDialer.Default.Open(Room.LandlordPhone);
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private void NotifyStatusPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(IsRented));
        OnPropertyChanged(nameof(IsUnavailable));
        OnPropertyChanged(nameof(CanBookOrContact));
        OnPropertyChanged(nameof(ShowSimilarRooms));
        OnPropertyChanged(nameof(CanViewPhoneNumber));
        OnPropertyChanged(nameof(DisplayPhoneNumber));
    }
}