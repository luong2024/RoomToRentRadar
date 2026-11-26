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
    private readonly ModalErrorHandler _errorHandler;
    private int _roomId = 1;
    private RoomDetailDto _room = new();
    private bool _isAuthenticated;
    private bool _isBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public RoomDetailPageModel(RoomService roomService, ModalErrorHandler errorHandler)
    {
        _roomService = roomService;
        _errorHandler = errorHandler;

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
    public string StatusText => IsRoomUnavailable ? "Unavailable" : "Available";

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
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
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
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("login");
        }
    }

    private void OnCallLandlord()
    {
        try
        {
            if (IsAuthenticated && PhoneDialer.Default.IsSupported && !string.IsNullOrWhiteSpace(Room.LandlordPhone))
            {
                PhoneDialer.Default.Open(Room.LandlordPhone);
            }
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    private async Task OnCopyPhoneAsync()
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(Room.LandlordPhone))
            return;

        try
        {
            await Clipboard.Default.SetTextAsync(Room.LandlordPhone);
            await AppShell.DisplayToastAsync($"Copied {Room.LandlordPhone}");

            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Copied",
                    $"Phone number {Room.LandlordPhone} has been copied to clipboard.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}