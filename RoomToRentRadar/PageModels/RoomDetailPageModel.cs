using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels;

[QueryProperty(nameof(RoomId), "RoomId")]
public class RoomDetailPageModel : INotifyPropertyChanged
{
    private readonly RoomService _roomService;
    private int _roomId = 1;
    private RoomDetailDto _room = new();
    private bool _isAuthenticated;
    private bool _isBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public RoomDetailPageModel(RoomService roomService)
    {
        _roomService = roomService;
        UnlockContactCommand = new Command(async () => await OnUnlockContactAsync());
        CallLandlordCommand = new Command(OnCallLandlord);
        CopyPhoneCommand = new Command(async () => await OnCopyPhoneAsync());
        SelectSimilarRoomCommand = new Command<int>(async (id) => await LoadRoomAsync(id));
    }

    public int RoomId
    {
        get => _roomId;
        set
        {
            _roomId = value;
            _ = LoadRoomAsync(value);
        }
    }

    public RoomDetailDto Room
    {
        get => _room;
        set
        {
            _room = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayLandlordPhone));
            OnPropertyChanged(nameof(IsRoomUnavailable));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set
        {
            _isAuthenticated = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsGuest));
            OnPropertyChanged(nameof(DisplayLandlordPhone));
        }
    }

    public bool IsGuest => !IsAuthenticated;
    public bool IsRoomUnavailable => !string.Equals(Room.Status, "Available", StringComparison.OrdinalIgnoreCase);
    public string StatusText => IsRoomUnavailable ? "Đã cho thuê / Không khả dụng" : "Còn phòng";

    // AC4: Che số điện thoại với khách, hiển thị đầy đủ khi đã đăng nhập
    public string DisplayLandlordPhone => IsAuthenticated
        ? Room.LandlordPhone
        : MaskPhoneNumber(Room.LandlordPhone);

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public ICommand UnlockContactCommand { get; }
    public ICommand CallLandlordCommand { get; }
    public ICommand CopyPhoneCommand { get; }
    public ICommand SelectSimilarRoomCommand { get; }

    public async Task RefreshAuthStateAsync()
    {
        var token = await SecureStorage.Default.GetAsync("auth_token");
        IsAuthenticated = !string.IsNullOrWhiteSpace(token);
    }

    public async Task LoadRoomAsync(int roomId)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await RefreshAuthStateAsync();
            Room = await _roomService.GetRoomDetailAsync(roomId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string MaskPhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7)
            return "**********";
        return $"{phone[..4]}***{phone[^3..]}";
    }

    private static async Task OnUnlockContactAsync()
    {
        // Điều hướng sang màn hình Login của Task T01.2
        await Shell.Current.GoToAsync("LoginPage");
    }

    private void OnCallLandlord()
    {
        if (IsAuthenticated && PhoneDialer.Default.IsSupported && !string.IsNullOrWhiteSpace(Room.LandlordPhone))
            PhoneDialer.Default.Open(Room.LandlordPhone);
    }

    private async Task OnCopyPhoneAsync()
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(Room.LandlordPhone)) return;
        await Clipboard.Default.SetTextAsync(Room.LandlordPhone);
        await Shell.Current.DisplayAlert("Đã sao chép", $"Số điện thoại {Room.LandlordPhone} đã được lưu vào bộ nhớ tạm.", "OK");
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}