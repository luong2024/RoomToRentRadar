using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Models.RoomDetails;
using RoomToRentRadar.Services.RoomDetails;
using RoomToRentRadar.Services.RoomListings;

namespace RoomToRentRadar.ViewModels.RoomDetails;

public partial class RoomDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IRoomDetailService _roomDetailService;
    private readonly IErrorHandler _errorHandler;
    private readonly IAuthenticationSessionService _sessionService;
    private readonly Func<string, Task>? _navigateAsync;
    private readonly Func<string, string, string, string?, Task<bool>>? _displayAlertAsync;
    private readonly IFavouritesService? _favouritesService;
    private readonly IFavoriteFeedbackService? _favoriteFeedbackService;
    private CancellationTokenSource? _loadCts;

    [ObservableProperty]
    public partial string Title { get; set; } = "Room details";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsNotFound { get; set; }

    [ObservableProperty]
    public partial bool HasLoadError { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RoomId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    [ObservableProperty]
    public partial bool IsFavoriteBusy { get; set; }

    [ObservableProperty]
    public partial string FavoriteErrorMessage { get; set; } = string.Empty;

    public bool CanToggleFavorite => !IsFavoriteBusy;
    public bool HasFavoriteError => !string.IsNullOrWhiteSpace(FavoriteErrorMessage);
    public string FavoriteAccessibilityText => IsFavorite ? "Remove room from favorites" : "Add room to favorites";

    partial void OnIsFavoriteChanged(bool value) => OnPropertyChanged(nameof(FavoriteAccessibilityText));
    partial void OnIsFavoriteBusyChanged(bool value) => OnPropertyChanged(nameof(CanToggleFavorite));
    partial void OnFavoriteErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasFavoriteError));

    [ObservableProperty]
    public partial RoomDetailModel? Room { get; set; }

    [ObservableProperty]
    public partial List<SimilarRoomModel> SimilarRooms { get; set; } = [];

    public bool HasRoom => Room is not null;
    public bool IsLoadStateVisible => IsNotFound || HasLoadError;

    // Listing Status & Visibility (AC2, AC3)
    public bool IsAvailable => Room?.Status == ListingStatus.Available;
    public bool IsRented => Room?.Status == ListingStatus.Rented;
    public bool IsUnavailable => Room?.Status == ListingStatus.Unavailable;
    public bool IsAddressHidden => !IsAvailable;
    public bool CanBookOrContact => IsAvailable;
    public bool ShowSimilarRooms => IsRented || IsUnavailable;
    public string ListingStatusTitle => IsRented
        ? "This room has been rented"
        : IsUnavailable
            ? "This listing is unavailable"
            : string.Empty;
    public string ListingStatusMessage => Room?.UnavailableMessage ?? (IsRented
        ? "This room is no longer available. Browse similar available rooms below."
        : "This listing is temporarily unavailable. Browse similar available rooms below.");

    public bool HasLandlordContact => !string.IsNullOrWhiteSpace(Room?.LandlordPhone)
        && !Room.LandlordPhone.StartsWith("***", StringComparison.Ordinal);
    public bool CanCallLandlord => IsAvailable && HasLandlordContact;
    public string DisplayPhoneNumber => Room?.LandlordPhone ?? "Contact information is unavailable";

    public RoomDetailViewModel(
        IRoomDetailService roomDetailService,
        IErrorHandler errorHandler,
        IAuthenticationSessionService sessionService,
        Func<string, Task>? navigateAsync = null,
        Func<string, string, string, string?, Task<bool>>? displayAlertAsync = null,
        IFavouritesService? favouritesService = null,
        IFavoriteFeedbackService? favoriteFeedbackService = null)
    {
        _roomDetailService = roomDetailService ?? throw new ArgumentNullException(nameof(roomDetailService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _navigateAsync = navigateAsync;
        _displayAlertAsync = displayAlertAsync;
        _favouritesService = favouritesService;
        _favoriteFeedbackService = favoriteFeedbackService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idObj) && idObj is not null)
        {
            var rawId = idObj.ToString() ?? string.Empty;
            var roomId = Uri.UnescapeDataString(rawId);
            if (string.Equals(RoomId, roomId, StringComparison.Ordinal) && IsBusy)
            {
                return;
            }

            RoomId = roomId;
            _ = LoadRoomDetailsCommand.ExecuteAsync(null);
        }
        else
        {
            RoomId = string.Empty;
            _ = LoadRoomDetailsCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task LoadRoomDetailsAsync()
    {
        var currentCts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _loadCts, currentCts);
        previousCts?.Cancel();

        if (string.IsNullOrWhiteSpace(RoomId))
        {
            IsBusy = false;
            Room = null;
            SimilarRooms = [];
            IsNotFound = true;
            HasLoadError = false;
            StatusMessage = "A room ID is required.";
            NotifyStatusPropertiesChanged();
            if (ReferenceEquals(_loadCts, currentCts))
            {
                _loadCts = null;
                currentCts.Dispose();
            }
            return;
        }

        var token = currentCts.Token;
        var roomId = RoomId;

        try
        {
            IsBusy = true;
            Room = null;
            IsNotFound = false;
            HasLoadError = false;
            StatusMessage = string.Empty;
            SimilarRooms = [];
            NotifyStatusPropertiesChanged();

            var room = await _roomDetailService.GetRoomDetailAsync(roomId, token);

            if (token.IsCancellationRequested || !ReferenceEquals(_loadCts, currentCts))
            {
                return;
            }

            if (room is null)
            {
                Room = null;
                SimilarRooms = [];
                IsNotFound = true;
                HasLoadError = false;
                StatusMessage = "This room was not found or has been removed.";
                NotifyStatusPropertiesChanged();
                return;
            }

            Room = room;
            Title = room.Title;
            IsFavorite = false;
            FavoriteErrorMessage = string.Empty;
            if (_sessionService.IsAuthenticated && _favouritesService is not null)
            {
                try
                {
                    IsFavorite = (await _favouritesService.GetMyFavouriteIdsAsync(token)).Contains(roomId);
                }
                catch (Exception ex)
                {
                    FavoriteErrorMessage = "Unable to load favorite status.";
                    _errorHandler.HandleError(ex);
                }
            }
            IsNotFound = false;
            HasLoadError = false;

            // Refresh computed properties
            NotifyStatusPropertiesChanged();

            // Similar rooms are included in the detail response. An empty list is a valid API result.
            SimilarRooms = room.SimilarRooms ?? [];
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer navigation request superseded this load.
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested && ReferenceEquals(_loadCts, currentCts))
            {
                Room = null;
                SimilarRooms = [];
                IsNotFound = false;
                HasLoadError = true;
                StatusMessage = "Unable to load room details. Check your network connection and try again.";
                _errorHandler.HandleError(ex);
                NotifyStatusPropertiesChanged();
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCts, currentCts))
            {
                IsBusy = false;
                _loadCts = null;
            }

            currentCts.Dispose();
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task ToggleFavoriteAsync()
    {
        if (Room is null || IsFavoriteBusy || _favouritesService is null)
        {
            return;
        }
        if (!_sessionService.IsAuthenticated)
        {
            StatusMessage = "Sign in to save favorite rooms.";
            return;
        }
        IsFavoriteBusy = true;
        try
        {
            if (IsFavorite)
            {
                await _favouritesService.RemoveFavouriteAsync(Room.Id);
                IsFavorite = false;
                if (_favoriteFeedbackService is not null)
                {
                    var removedRoom = Room;
                    await _favoriteFeedbackService.ShowUndoAsync(
                        $"Removed {removedRoom.Title} from favorites",
                        "Undo",
                        () => RestoreFavouriteAsync(removedRoom.Id));
                }
            }
            else
            {
                await _favouritesService.AddFavouriteAsync(Room.Id);
                IsFavorite = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to update favorites. Please try again.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsFavoriteBusy = false;
        }
    }

    private async Task RestoreFavouriteAsync(string roomId)
    {
        if (_favouritesService is null || !_sessionService.IsAuthenticated || IsFavoriteBusy)
        {
            return;
        }
        IsFavoriteBusy = true;
        try
        {
            await _favouritesService.AddFavouriteAsync(roomId);
            IsFavorite = true;
            FavoriteErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            FavoriteErrorMessage = "Unable to restore this favorite. Please try again.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsFavoriteBusy = false;
        }
    }

    [RelayCommand]
    public async Task SelectSimilarRoomAsync(SimilarRoomModel similarRoom)
    {
        if (similarRoom is null || string.IsNullOrWhiteSpace(similarRoom.Id)) return;

        var targetRoute = $"{AppRoutes.RoomDetail}?id={Uri.EscapeDataString(similarRoom.Id)}";
        await NavigateAsync(targetRoute);
    }

    /// <summary>
    /// Placeholder action for scheduling a viewing (US02 scope covers room details & listing status).
    /// Full appointment booking workflow is scheduled for the viewing management user story.
    /// </summary>
    [RelayCommand]
    public async Task BookInspectionAsync()
    {
        if (!CanBookOrContact)
        {
            return;
        }

        if (!_sessionService.IsAuthenticated)
        {
            await NavigateAsync($"//{AppRoutes.Login}");
            return;
        }

        await DisplayAlertAsync(
            "Coming Soon",
            "Viewing appointment scheduling is not available yet.",
            "OK",
            null);
    }

    private async Task<bool> DisplayAlertAsync(string title, string message, string accept, string? cancel)
    {
        try
        {
            if (_displayAlertAsync is not null)
            {
                return await _displayAlertAsync(title, message, accept, cancel);
            }

#if ANDROID || IOS || MACCATALYST || WINDOWS
            if (Shell.Current is not null)
            {
                if (cancel is null)
                {
                    await Shell.Current.DisplayAlertAsync(title, message, accept);
                    return true;
                }

                return await Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
            }
#endif
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }

        return false;
    }

    [RelayCommand]
    public async Task CallLandlordAsync()
    {
        var phoneNumber = Room?.LandlordPhone;
        if (CanCallLandlord && !string.IsNullOrWhiteSpace(phoneNumber) && PhoneDialer.Default.IsSupported)
        {
            PhoneDialer.Default.Open(phoneNumber);
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
        OnPropertyChanged(nameof(IsLoadStateVisible));
        OnPropertyChanged(nameof(HasRoom));
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(IsRented));
        OnPropertyChanged(nameof(IsUnavailable));
        OnPropertyChanged(nameof(IsAddressHidden));
        OnPropertyChanged(nameof(CanBookOrContact));
        OnPropertyChanged(nameof(ShowSimilarRooms));
        OnPropertyChanged(nameof(ListingStatusTitle));
        OnPropertyChanged(nameof(ListingStatusMessage));
        OnPropertyChanged(nameof(HasLandlordContact));
        OnPropertyChanged(nameof(CanCallLandlord));
        OnPropertyChanged(nameof(DisplayPhoneNumber));
    }
}
