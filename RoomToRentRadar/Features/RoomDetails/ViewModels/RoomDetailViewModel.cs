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
    private readonly Func<string, Task>? _navigateAsync;

    [ObservableProperty]
    public partial string Title { get; set; } = "Chi tiết phòng trọ";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsNotFound { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RoomId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial RoomDetailModel? Room { get; set; }

    [ObservableProperty]
    public partial bool IsUserLoggedIn { get; set; } = false; // Auth simulation toggle

    [ObservableProperty]
    public partial List<SimilarRoomModel> SimilarRooms { get; set; } = [];

    public bool HasRoom => Room is not null;

    // Listing Status & Visibility (AC2, AC3)
    public bool IsAvailable => Room?.Status == ListingStatus.Available;
    public bool IsRented => Room?.Status == ListingStatus.Rented;
    public bool IsUnavailable => Room?.Status == ListingStatus.Unavailable;
    public bool CanBookOrContact => IsAvailable;
    public bool ShowSimilarRooms => IsRented || IsUnavailable;

    // Landlord Phone Visibility based on Authentication (AC4)
    public bool CanViewPhoneNumber => IsUserLoggedIn && !string.IsNullOrWhiteSpace(Room?.LandlordPhone);
    public string DisplayPhoneNumber => CanViewPhoneNumber ? Room!.LandlordPhone! : "Login to view phone number";

    public RoomDetailViewModel(
        IRoomDetailService roomDetailService,
        IErrorHandler errorHandler,
        Func<string, Task>? navigateAsync = null)
    {
        _roomDetailService = roomDetailService ?? throw new ArgumentNullException(nameof(roomDetailService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _navigateAsync = navigateAsync;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idObj) && idObj is not null)
        {
            var rawId = idObj.ToString() ?? string.Empty;
            RoomId = Uri.UnescapeDataString(rawId);
            _ = LoadRoomDetailsAsync();
        }
    }

    [RelayCommand]
    public async Task LoadRoomDetailsAsync()
    {
        if (string.IsNullOrWhiteSpace(RoomId))
            RoomId = "default-1"; // Default test ID

        try
        {
            IsBusy = true;
            IsNotFound = false;
            StatusMessage = string.Empty;

            var room = await _roomDetailService.GetRoomDetailAsync(RoomId);

            if (room is null)
            {
                Room = null;
                IsNotFound = true;
                StatusMessage = "Không tìm thấy thông tin phòng trọ này hoặc phòng đã bị xóa.";
                NotifyStatusPropertiesChanged();
                return;
            }

            Room = room;
            Title = room.Title;
            IsNotFound = false;

            // Refresh computed properties
            NotifyStatusPropertiesChanged();

            // Load similar properties when property is rented or unavailable (AC3)
            if (room.SimilarRooms is { Count: > 0 })
            {
                SimilarRooms = room.SimilarRooms;
            }
            else if (ShowSimilarRooms)
            {
                SimilarRooms = await _roomDetailService.GetSimilarRoomsAsync(RoomId);
            }
        }
        catch (Exception ex)
        {
            Room = null;
            IsNotFound = true;
            StatusMessage = "Không thể tải chi tiết phòng trọ. Vui lòng kiểm tra kết nối mạng và thử lại.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void ToggleAuthSimulation()
    {
        // Toggle simulation for instructor and automated verification (AC4)
        IsUserLoggedIn = !IsUserLoggedIn;
        OnPropertyChanged(nameof(CanViewPhoneNumber));
        OnPropertyChanged(nameof(DisplayPhoneNumber));
    }

    [RelayCommand]
    public async Task SelectSimilarRoomAsync(SimilarRoomModel similarRoom)
    {
        if (similarRoom is null || string.IsNullOrWhiteSpace(similarRoom.Id)) return;

        var targetRoute = $"{AppRoutes.RoomDetails}?id={Uri.EscapeDataString(similarRoom.Id)}";
        await NavigateAsync(targetRoute);
    }

    [RelayCommand]
    public async Task BookInspectionAsync()
    {
        if (!CanBookOrContact) return;

#if ANDROID || IOS || MACCATALYST || WINDOWS
        if (Shell.Current is not null)
        {
            await Shell.Current.DisplayAlertAsync("Đặt lịch xem phòng", $"Xác nhận gửi yêu cầu hẹn xem phòng \"{Room?.Title}\" tới chủ trọ?", "Xác nhận", "Hủy");
        }
#endif
    }

    [RelayCommand]
    public async Task CallLandlordAsync()
    {
        if (!IsUserLoggedIn)
        {
#if ANDROID || IOS || MACCATALYST || WINDOWS
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync("Yêu cầu đăng nhập", "Vui lòng đăng nhập để xem số điện thoại và liên hệ chủ trọ.", "Đóng");
            }
#endif
            return;
        }

        if (PhoneDialer.Default.IsSupported && !string.IsNullOrWhiteSpace(Room?.LandlordPhone))
        {
            PhoneDialer.Default.Open(Room.LandlordPhone);
        }
    }

    [RelayCommand]
    public async Task GoBackAsync()
    {
        await NavigateAsync("..");
    }

    private async Task NavigateAsync(string route)
    {
        try
        {
            if (_navigateAsync is not null)
            {
                await _navigateAsync(route);
                return;
            }

#if ANDROID || IOS || MACCATALYST || WINDOWS
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync(route);
            }
#endif
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    private void NotifyStatusPropertiesChanged()
    {
        OnPropertyChanged(nameof(HasRoom));
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(IsRented));
        OnPropertyChanged(nameof(IsUnavailable));
        OnPropertyChanged(nameof(CanBookOrContact));
        OnPropertyChanged(nameof(ShowSimilarRooms));
        OnPropertyChanged(nameof(CanViewPhoneNumber));
        OnPropertyChanged(nameof(DisplayPhoneNumber));
    }
}
