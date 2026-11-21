using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Localization;
using RoomToRentRadar.Models.RoomListings;
using RoomToRentRadar.Services.RoomListings;

namespace RoomToRentRadar.ViewModels.RoomListings;

/// <summary>
/// Manages presentation state and navigation parameter handling for the room details screen.
/// </summary>
public partial class RoomDetailsViewModel : ObservableObject
#if ANDROID || IOS || MACCATALYST || WINDOWS
    , IQueryAttributable
#endif
{
    private readonly IRoomListingsService _roomListingsService;
    private readonly IErrorHandler _errorHandler;
    private readonly Func<string, Task>? _navigateAsync;
    private CancellationTokenSource? _loadCts;

    public RoomDetailsViewModel(
        IRoomListingsService roomListingsService,
        IErrorHandler errorHandler,
        Func<string, Task>? navigateAsync = null)
    {
        ArgumentNullException.ThrowIfNull(roomListingsService);
        ArgumentNullException.ThrowIfNull(errorHandler);

        _roomListingsService = roomListingsService;
        _errorHandler = errorHandler;
        _navigateAsync = navigateAsync;

        UpdateLocalizedStrings();
        LocalizationResourceManager.Instance.PropertyChanged += (_, _) => UpdateLocalizedStrings();
    }

    private void UpdateLocalizedStrings()
    {
        if (Room is null)
        {
            Title = LocalizationResourceManager.Instance["RoomDetails_Title"];
        }
    }

    [ObservableProperty]
    public partial string Title { get; set; } = "Room Details";

    [ObservableProperty]
    public partial string RoomId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial RoomListItemModel? Room { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsNotFound { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public bool HasRoom => Room is not null;

    partial void OnRoomChanged(RoomListItemModel? value)
    {
        OnPropertyChanged(nameof(HasRoom));
        if (value is not null)
        {
            Title = value.Title;
        }
    }

#if ANDROID || IOS || MACCATALYST || WINDOWS
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var rawId))
        {
            var decodedId = Uri.UnescapeDataString(rawId?.ToString() ?? string.Empty);
            _ = LoadRoomByIdAsync(decodedId);
        }
    }
#endif

    [RelayCommand]
    public async Task LoadRoomByIdAsync(string? roomId)
    {
        var trimmedId = roomId?.Trim() ?? string.Empty;
        RoomId = trimmedId;

        if (string.IsNullOrEmpty(trimmedId))
        {
            Room = null;
            IsNotFound = true;
            StatusMessage = "Valid room ID not found.";
            return;
        }

        var currentCts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _loadCts, currentCts);
        previousCts?.Cancel();
        previousCts?.Dispose();

        var token = currentCts.Token;

        try
        {
            IsLoading = true;
            IsNotFound = false;
            StatusMessage = string.Empty;

            var room = await _roomListingsService.GetRoomByIdAsync(trimmedId, token);

            if (token.IsCancellationRequested || !ReferenceEquals(_loadCts, currentCts))
            {
                return;
            }

            Room = room;
            IsNotFound = room is null;
            StatusMessage = room is null
                ? "Room details could not be found."
                : string.Empty;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Superseded by a newer load request
        }
        catch (Exception ex) when (!token.IsCancellationRequested && ReferenceEquals(_loadCts, currentCts))
        {
            Room = null;
            IsNotFound = true;
            StatusMessage = "Unable to load room details. Please try again.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            if (ReferenceEquals(_loadCts, currentCts))
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    public async Task GoBackAsync()
    {
        try
        {
            if (_navigateAsync is not null)
            {
                await _navigateAsync("..");
                return;
            }

#if ANDROID || IOS || MACCATALYST || WINDOWS
            if (Microsoft.Maui.Controls.Shell.Current is not null)
            {
                await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
            }
#endif
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }
}
