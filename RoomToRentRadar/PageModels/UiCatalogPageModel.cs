using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Pages.Controls.Cards.Models;

namespace RoomToRentRadar.PageModels;

public partial class UiCatalogPageModel : ObservableObject
{
    [ObservableProperty]
    public partial RoomCardDisplayModel SampleRoom { get; set; }

    [ObservableProperty]
    public partial bool IsButtonLoading { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InputError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FeedbackMessage { get; set; } = "Nhấn các nút để thử nghiệm tương tác.";

    public UiCatalogPageModel()
    {
        SampleRoom = new RoomCardDisplayModel
        {
            Id = "room-demo-01",
            Title = "Phòng trọ cao cấp Full đồ gần Cổng trường ICTU",
            PricePerMonth = 2200000m,
            AreaSquareMeters = 25.5,
            Address = "Số 18, Đường Z115, Quyết Thắng, TP. Thái Nguyên",
            DistanceMeters = 350,
            ThumbnailUrl = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=600&q=80",
            IsVerified = true,
            IsAvailable = true,
            IsBookmarked = false,
            Amenities = ["Wifi", "Điều hòa", "Nóng lạnh", "Vệ sinh khép kín"]
        };
    }

    [RelayCommand]
    private void ToggleButtonLoading()
    {
        IsButtonLoading = !IsButtonLoading;
        FeedbackMessage = IsButtonLoading ? "RadaButton đang ở trạng thái IsLoading = true" : "RadaButton đã hoàn thành";
    }

    [RelayCommand]
    private void ToggleInputError()
    {
        InputError = string.IsNullOrEmpty(InputError)
            ? "Số điện thoại hoặc thông tin không hợp lệ!"
            : string.Empty;
    }

    [RelayCommand]
    private void ToggleBookmark(string roomId)
    {
        SampleRoom = SampleRoom with { IsBookmarked = !SampleRoom.IsBookmarked };
        FeedbackMessage = SampleRoom.IsBookmarked
            ? "Đã lưu phòng vào danh sách Yêu thích (US04)!"
            : "Đã xóa phòng khỏi danh sách Yêu thích.";
    }

    [RelayCommand]
    private void ViewRoomDetails(string roomId)
    {
        FeedbackMessage = $"Đã kích hoạt điều hướng đến Chi tiết phòng: {roomId}";
    }

    [RelayCommand]
    private void RetryOperation()
    {
        FeedbackMessage = "Đã nhấn nút 'Thử lại' từ ErrorRetryView!";
    }

    [RelayCommand]
    private void ResetFilters()
    {
        SearchQuery = string.Empty;
        FeedbackMessage = "Đã nhấn 'Xóa bộ lọc' từ EmptyStateView!";
    }
}

