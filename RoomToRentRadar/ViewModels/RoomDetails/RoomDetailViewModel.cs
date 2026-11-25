using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Models.RoomDetails;
using RoomToRentRadar.Services.RoomDetails;

namespace RoomToRentRadar.ViewModels.RoomDetails;

public partial class RoomDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IRoomDetailService _roomDetailService;
    private readonly IErrorHandler _errorHandler;
    private readonly IAuthenticationSessionService _sessionService;
    private readonly Func<string, Task>? _navigateAsync;
    private readonly Func<string, string, string, string?, Task<bool>>? _displayAlertAsync;
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
    public partial RoomDetailModel? Room { get; set; }

    [ObservableProperty]
    public partial List<SimilarRoomModel> SimilarRooms { get; set; } = [];

    [ObservableProperty]
    public partial RoomHouseRulesModel? HouseRules { get; set; }

    [ObservableProperty]
    public partial List<HouseRulesVersionModel> HouseRulesHistory { get; set; } = [];

    [ObservableProperty]
    public partial bool IsHouseRulesHistoryVisible { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingHistory { get; set; }

    [ObservableProperty]
    public partial bool IsEditHouseRulesVisible { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveHouseRules))]
    public partial bool IsSavingHouseRules { get; set; }

    [ObservableProperty]
    public partial string HouseRulesEditError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditQuietHours { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditGuestPolicy { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditCookingRules { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditParkingRules { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditAdditionalRulesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditChangeNote { get; set; } = string.Empty;

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

    // House Rules & Versioning (US11)
    public bool HasStructuredHouseRules => HouseRules is not null && !HouseRules.IsLegacyFallback;
    public bool ShowLegacyHouseRulesFallback =>
        !HasStructuredHouseRules && ((HouseRules?.AdditionalRules.Count ?? 0) > 0 || (Room?.HouseRules.Count ?? 0) > 0);
    public bool ShowEmptyHouseRulesState => !HasStructuredHouseRules && !ShowLegacyHouseRulesFallback;
    public string HouseRulesVersionText => HouseRules?.VersionBadgeText ?? "Legacy";
    public string HouseRulesSubtitleText => HasStructuredHouseRules
        ? HouseRules!.NaturalStatusText
        : "Building & living guidelines";
    public bool CanViewHouseRulesHistory => HasStructuredHouseRules;
    public bool HasHouseRulesHistory => HouseRulesHistory.Count > 0;
    public bool HasHouseRulesEditError => !string.IsNullOrWhiteSpace(HouseRulesEditError);
    public bool CanSaveHouseRules => !IsSavingHouseRules;
    public bool CanEditHouseRules
    {
        get
        {
            var user = _sessionService.CurrentUser;
            if (user is null || Room is null)
            {
                return false;
            }

            if (string.Equals(user.Role, UserRoleConstants.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(user.Role, UserRoleConstants.Landlord, StringComparison.OrdinalIgnoreCase)
                && HouseRules?.LandlordId is Guid landlordId
                && landlordId != Guid.Empty
                && landlordId == user.Id;
        }
    }

    public RoomDetailViewModel(
        IRoomDetailService roomDetailService,
        IErrorHandler errorHandler,
        IAuthenticationSessionService sessionService,
        Func<string, Task>? navigateAsync = null,
        Func<string, string, string, string?, Task<bool>>? displayAlertAsync = null)
    {
        _roomDetailService = roomDetailService ?? throw new ArgumentNullException(nameof(roomDetailService));
        _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _navigateAsync = navigateAsync;
        _displayAlertAsync = displayAlertAsync;
        _sessionService.CurrentUserChanged += OnCurrentUserChanged;
    }

    private void OnCurrentUserChanged(object? sender, Core.Models.Authentication.AuthUser? user)
    {
        OnPropertyChanged(nameof(CanEditHouseRules));
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
            HouseRules = null;
            HouseRulesHistory = [];
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
            IsNotFound = false;
            HasLoadError = false;

            if (room.StructuredHouseRules is not null)
            {
                HouseRules = room.StructuredHouseRules;
            }
            else
            {
                try
                {
                    HouseRules = await _roomDetailService.GetHouseRulesAsync(room.Id, token);
                }
                catch
                {
                    HouseRules = null;
                }
            }

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

    [RelayCommand]
    public async Task OpenHouseRulesHistoryAsync()
    {
        if (Room is null || string.IsNullOrWhiteSpace(Room.Id))
        {
            return;
        }

        IsHouseRulesHistoryVisible = true;
        IsLoadingHistory = true;
        OnPropertyChanged(nameof(HasHouseRulesHistory));

        try
        {
            HouseRulesHistory = await _roomDetailService.GetHouseRulesHistoryAsync(Room.Id);
            OnPropertyChanged(nameof(HasHouseRulesHistory));
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    [RelayCommand]
    public void CloseHouseRulesHistory()
    {
        IsHouseRulesHistoryVisible = false;
    }

    [RelayCommand]
    public void OpenEditHouseRules()
    {
        if (!CanEditHouseRules)
        {
            return;
        }

        HouseRulesEditError = string.Empty;
        OnPropertyChanged(nameof(HasHouseRulesEditError));

        if (HouseRules is not null && !HouseRules.IsLegacyFallback)
        {
            EditQuietHours = HouseRules.QuietHours;
            EditGuestPolicy = HouseRules.GuestPolicy;
            EditCookingRules = HouseRules.CookingRules;
            EditParkingRules = HouseRules.ParkingRules;
            EditAdditionalRulesText = string.Join(Environment.NewLine, HouseRules.AdditionalRules);
        }
        else
        {
            EditQuietHours = string.Empty;
            EditGuestPolicy = string.Empty;
            EditCookingRules = string.Empty;
            EditParkingRules = string.Empty;
            EditAdditionalRulesText = Room is not null
                ? string.Join(Environment.NewLine, Room.HouseRules)
                : string.Empty;
        }

        EditChangeNote = string.Empty;
        IsEditHouseRulesVisible = true;
    }

    [RelayCommand]
    public void CloseEditHouseRules()
    {
        IsEditHouseRulesVisible = false;
        HouseRulesEditError = string.Empty;
        OnPropertyChanged(nameof(HasHouseRulesEditError));
    }

    [RelayCommand]
    public async Task SaveHouseRulesAsync()
    {
        if (IsSavingHouseRules || Room is null || !CanEditHouseRules)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditQuietHours) ||
            string.IsNullOrWhiteSpace(EditGuestPolicy) ||
            string.IsNullOrWhiteSpace(EditCookingRules) ||
            string.IsNullOrWhiteSpace(EditParkingRules))
        {
            HouseRulesEditError = "Quiet hours, guest policy, cooking rules, and parking rules are all required.";
            OnPropertyChanged(nameof(HasHouseRulesEditError));
            return;
        }

        IsSavingHouseRules = true;
        HouseRulesEditError = string.Empty;
        OnPropertyChanged(nameof(HasHouseRulesEditError));

        try
        {
            var additional = EditAdditionalRulesText
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var expectedVersion = HouseRules is not null && !HouseRules.IsLegacyFallback
                ? HouseRules.Version
                : (int?)null;

            var request = new UpdateHouseRulesApiRequest(
                QuietHours: EditQuietHours.Trim(),
                GuestPolicy: EditGuestPolicy.Trim(),
                CookingRules: EditCookingRules.Trim(),
                ParkingRules: EditParkingRules.Trim(),
                AdditionalRules: additional,
                ChangeNote: string.IsNullOrWhiteSpace(EditChangeNote) ? null : EditChangeNote.Trim(),
                ExpectedVersion: expectedVersion);

            var updated = await _roomDetailService.UpdateHouseRulesAsync(Room.Id, request);
            if (updated is not null)
            {
                HouseRules = updated;
                Room.StructuredHouseRules = updated;
                Room.HouseRules =
                [
                    $"Quiet hours: {updated.QuietHours}",
                    $"Guest policy: {updated.GuestPolicy}",
                    $"Cooking rules: {updated.CookingRules}",
                    $"Parking rules: {updated.ParkingRules}",
                    .. updated.AdditionalRules
                ];
                NotifyStatusPropertiesChanged();
            }

            IsEditHouseRulesVisible = false;
            await DisplayAlertAsync(
                "House Rules Updated",
                $"House rules saved (version {HouseRules?.VersionBadgeText ?? "updated"}).",
                "OK",
                null);
        }
        catch (Exception ex)
        {
            var isConflict = ex.Message.Contains("409", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase);

            HouseRulesEditError = isConflict
                ? "House rules were updated by another session. Please close and reload the room before saving."
                : ex.Message;
            OnPropertyChanged(nameof(HasHouseRulesEditError));
        }
        finally
        {
            IsSavingHouseRules = false;
        }
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
        OnPropertyChanged(nameof(HasStructuredHouseRules));
        OnPropertyChanged(nameof(ShowLegacyHouseRulesFallback));
        OnPropertyChanged(nameof(ShowEmptyHouseRulesState));
        OnPropertyChanged(nameof(HouseRulesVersionText));
        OnPropertyChanged(nameof(HouseRulesSubtitleText));
        OnPropertyChanged(nameof(CanViewHouseRulesHistory));
        OnPropertyChanged(nameof(CanEditHouseRules));
        OnPropertyChanged(nameof(HasHouseRulesHistory));
        OnPropertyChanged(nameof(HasHouseRulesEditError));
    }
}
