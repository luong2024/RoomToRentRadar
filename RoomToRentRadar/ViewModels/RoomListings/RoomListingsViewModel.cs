using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Localization;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Models.RoomListings;
using RoomToRentRadar.Services.RoomListings;

namespace RoomToRentRadar.ViewModels.RoomListings;

/// <summary>
/// Manages presentation state, sorting, pull-to-refresh, and infinite scroll pagination
/// for the room listings screen.
/// </summary>
public partial class RoomListingsViewModel : SearchRoomsViewModel
#if ANDROID || IOS || MACCATALYST || WINDOWS
    , IQueryAttributable
#endif
{
    public const int DefaultPageSize = 6;

    private readonly IRoomListingsService _roomListingsService;
    private readonly IErrorHandler _errorHandler;
    private readonly IAuthenticationSessionService? _sessionService;
    private readonly IFavouritesService? _favouritesService;
    private readonly IFavoriteFeedbackService? _favoriteFeedbackService;
    private readonly Func<string, Task>? _navigateAsync;
    private readonly Dictionary<string, RoomListItemModel> _favoriteRoomsById = new(StringComparer.Ordinal);
    private readonly HashSet<string> _favoriteMutationsInFlight = new(StringComparer.Ordinal);
    private readonly List<RoomListItemModel> _exploreSnapshot = [];
    private CancellationTokenSource? _paginationCts;
    private CancellationTokenSource? _favoriteLoadCts;
    private Task? _favoritesLoadTask;
    private long _favoriteSessionGeneration;
    private long _favoriteMutationVersion;
    private string? _lastErrorResourceKey;
    private string _exploreErrorMessage = string.Empty;
    private bool _isInitialized;
    private bool _isLoadingMore;
    private bool _isRefreshingExplore;
    private bool _isRefreshingFavorites;
    private bool _suppressAutoSortReload;
    private bool _favoritesLoaded;

    [ObservableProperty]
    public partial bool IsLandlord { get; set; }

    public RoomListingsViewModel(
        IRoomListingsService roomListingsService,
        IErrorHandler errorHandler,
        Func<string, Task>? navigateAsync = null)
        : this(roomListingsService, errorHandler, null, navigateAsync)
    {
    }

    public RoomListingsViewModel(
        IRoomListingsService roomListingsService,
        IErrorHandler errorHandler,
        IAuthenticationSessionService? sessionService,
        Func<string, Task>? navigateAsync = null)
        : this(roomListingsService, errorHandler, sessionService, null, null, navigateAsync)
    {
    }

    public RoomListingsViewModel(
        IRoomListingsService roomListingsService,
        IErrorHandler errorHandler,
        IAuthenticationSessionService? sessionService,
        IFavouritesService? favouritesService,
        IFavoriteFeedbackService? favoriteFeedbackService = null,
        Func<string, Task>? navigateAsync = null)
        : base(roomListingsService)
    {
        ArgumentNullException.ThrowIfNull(roomListingsService);
        ArgumentNullException.ThrowIfNull(errorHandler);

        _roomListingsService = roomListingsService;
        _errorHandler = errorHandler;
        _sessionService = sessionService;
        _favouritesService = favouritesService;
        _favoriteFeedbackService = favoriteFeedbackService;
        _navigateAsync = navigateAsync;
        SortOptions = RoomSortOptionItem.CreateDefaults();
        PrimarySortOptions = SortOptions
            .Where(o => o.Option is RoomSortOption.DistanceAsc)
            .ToArray();
        PriceSortOptions = SortOptions
            .Where(o => o.Option is RoomSortOption.PriceAsc or RoomSortOption.PriceDesc)
            .ToArray();
        SelectedSortOptionItem = SortOptions[0];
        Rooms.CollectionChanged += (_, _) => NotifyStateProperties();

        // Initial unfiltered state
        FilterMinPriceText = string.Empty;
        FilterMaxPriceText = string.Empty;
        MinPrice = null;
        MaxPrice = null;
        SelectedRoomType = "all";
        SelectedPricePreset = string.Empty;

        UpdateLocalizedStrings();
        LocalizationResourceManager.Instance.PropertyChanged += (_, _) => UpdateLocalizedStrings();

        UpdateLandlordStatus();
        if (_sessionService != null)
        {
            _sessionService.CurrentUserChanged += OnCurrentUserChanged;
        }
    }

    private void OnCurrentUserChanged(object? sender, AuthUser? user)
    {
        _favoriteSessionGeneration++;
        _favoriteLoadCts?.Cancel();
        _favoriteLoadCts = null;
        _favoritesLoadTask = null;
        IsLoadingFavorites = false;
        FavoriteErrorMessage = string.Empty;
        IsLandlord = string.Equals(user?.Role, UserRoleConstants.Landlord, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(user?.Role, UserRoleConstants.Admin, StringComparison.OrdinalIgnoreCase);
        _favoritesLoaded = false;
        _favoriteRoomsById.Clear();
        foreach (var room in _exploreSnapshot)
        {
            room.IsFavorite = false;
        }
        if (IsFavoritesTabSelected)
        {
            Rooms.Clear();
        }
        OnPropertyChanged(nameof(FavoriteCount));
        OnPropertyChanged(nameof(HasFavorites));
    }

    private void UpdateLandlordStatus()
    {
        var currentUser = _sessionService?.CurrentUser;
        IsLandlord = string.Equals(currentUser?.Role, UserRoleConstants.Landlord, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(currentUser?.Role, UserRoleConstants.Admin, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateLocalizedStrings()
    {
        Title = LocalizationResourceManager.Instance["Title_ExploreRooms"];

        if (HasError && !string.IsNullOrEmpty(_lastErrorResourceKey))
        {
            ExploreErrorMessage = LocalizationResourceManager.Instance[_lastErrorResourceKey];
            if (IsExploreTabSelected && Rooms.Count == 0)
            {
                EmptyStateMessage = ErrorMessage;
            }
        }
        else if (IsExploreTabSelected && Rooms.Count == 0)
        {
            EmptyStateMessage = LocalizationResourceManager.Instance["Empty_NoRooms"];
        }

        foreach (var option in SortOptions)
        {
            option.UpdateDisplayName();
        }

        OnPropertyChanged(nameof(PriceSortLabel));
        OnPropertyChanged(nameof(ResultSummaryText));
        OnPropertyChanged(nameof(ActiveFilterSummary));
        OnPropertyChanged(nameof(FilterShowResultsText));
    }

    public ObservableCollection<RoomListItemModel> Rooms { get; } = [];

    public IReadOnlyList<RoomSortOptionItem> SortOptions { get; }

    public IReadOnlyList<RoomSortOptionItem> PrimarySortOptions { get; }

    public IReadOnlyList<RoomSortOptionItem> PriceSortOptions { get; }

    public int PageSize { get; init; } = DefaultPageSize;

    [ObservableProperty]
    public partial string Title { get; set; } = "Explore Rooms";

    [ObservableProperty]
    public partial RoomSortOptionItem SelectedSortOptionItem { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    private string ExploreErrorMessage
    {
        get => _exploreErrorMessage;
        set => SetProperty(ref _exploreErrorMessage, value, nameof(ErrorMessage));
    }

    public string ErrorMessage => IsFavoritesTabSelected ? FavoriteErrorMessage : ExploreErrorMessage;

    [ObservableProperty]
    public partial bool HasCompletedInitialLoad { get; set; }

    public bool ShowLoadingState => Rooms.Count == 0 && (IsFavoritesTabSelected ? IsLoadingFavorites : IsExploreTabSelected && IsLoading);

    public bool ShowErrorState => Rooms.Count == 0 && (IsFavoritesTabSelected
        ? !IsLoadingFavorites && !string.IsNullOrEmpty(FavoriteErrorMessage)
        : IsExploreTabSelected && !IsLoading && HasError);

    public bool ShowEmptyState => Rooms.Count == 0 && (SelectedBottomTab switch
    {
        "favorites" => !IsLoadingFavorites && string.IsNullOrEmpty(FavoriteErrorMessage),
        "explore" => !IsLoading && !HasError && HasCompletedInitialLoad,
        _ => HasCompletedInitialLoad
    });

    public bool ShowContentState => Rooms.Count > 0 && (IsFavoritesTabSelected || (IsExploreTabSelected && !IsLoading && !HasError));

    public bool IsRefreshing
    {
        get => IsFavoritesTabSelected ? _isRefreshingFavorites : IsExploreTabSelected && _isRefreshingExplore;
        set
        {
            if (IsFavoritesTabSelected)
            {
                SetProperty(ref _isRefreshingFavorites, value);
            }
            else if (IsExploreTabSelected)
            {
                SetProperty(ref _isRefreshingExplore, value);
            }
        }
    }

    public bool IsLoadingMore
    {
        get => IsExploreTabSelected && _isLoadingMore;
        private set => SetProperty(ref _isLoadingMore, value);
    }

    [ObservableProperty]
    public partial bool HasMoreItems { get; set; } = true;

    [ObservableProperty]
    public partial int CurrentPage { get; set; }

    [ObservableProperty]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    public partial string EmptyStateMessage { get; set; } = "No matching rooms found.";

    [ObservableProperty]
    public partial bool IsLoadingFavorites { get; set; }

    [ObservableProperty]
    public partial string FavoriteErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPriceMenuOpen { get; set; }

    protected override void OnFilterPanelOpenStateChanged(bool isOpen)
    {
        if (IsPriceMenuOpen != isOpen)
        {
            IsPriceMenuOpen = isOpen;
        }
    }

    partial void OnIsPriceMenuOpenChanged(bool value)
    {
        if (IsFilterPanelOpen != value)
        {
            IsFilterPanelOpen = value;
        }
    }

    [ObservableProperty]
    public partial bool IsAmenityAirConSelected { get; set; }

    [ObservableProperty]
    public partial bool IsAmenityWifiSelected { get; set; }

    [ObservableProperty]
    public partial bool IsAmenityParkingSelected { get; set; }

    [ObservableProperty]
    public partial bool IsAmenityBathroomSelected { get; set; }

    [ObservableProperty]
    public partial bool IsAmenityWashingSelected { get; set; }

    [ObservableProperty]
    public partial bool IsAmenityKitchenSelected { get; set; }

    public string FilterShowResultsText =>
        string.Format(
            LocalizationResourceManager.Instance.CurrentCulture,
            LocalizationResourceManager.Instance["Filter_ShowResults_Format"],
            TotalCount);

    [ObservableProperty]
    public partial string SelectedBottomTab { get; set; } = "explore";

    public bool IsExploreTabSelected =>
        string.Equals(SelectedBottomTab, "explore", StringComparison.OrdinalIgnoreCase);

    public bool IsFavoritesTabSelected =>
        string.Equals(SelectedBottomTab, "favorites", StringComparison.OrdinalIgnoreCase);

    public bool IsMessagesTabSelected =>
        string.Equals(SelectedBottomTab, "messages", StringComparison.OrdinalIgnoreCase);

    public bool IsProfileTabSelected =>
        string.Equals(SelectedBottomTab, "profile", StringComparison.OrdinalIgnoreCase);

    public int FavoriteCount => _favoriteRoomsById.Count;

    public bool HasFavorites => FavoriteCount > 0;

    public bool IsPriceSortSelected =>
        SelectedSortOptionItem.Option is RoomSortOption.PriceAsc or RoomSortOption.PriceDesc;

    public string PriceSortLabel =>
        IsPriceSortSelected ? SelectedSortOptionItem.DisplayName : LocalizationResourceManager.Instance["Sort_Price"];

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);

    public string ActiveFilterSummary =>
        string.Format(
            LocalizationResourceManager.Instance.CurrentCulture,
            LocalizationResourceManager.Instance["Filter_Active_Format"],
            SearchQuery);

    public string ResultSummaryText =>
        HasSearchQuery
            ? string.Format(
                LocalizationResourceManager.Instance.CurrentCulture,
                LocalizationResourceManager.Instance["Result_Summary_WithSearch"],
                TotalCount,
                SearchQuery.Trim())
            : TotalCount > 0
                ? string.Format(
                    LocalizationResourceManager.Instance.CurrentCulture,
                    LocalizationResourceManager.Instance["Result_Summary_Format"],
                    TotalCount)
                : HasCompletedInitialLoad
                    ? string.Format(
                        LocalizationResourceManager.Instance.CurrentCulture,
                        LocalizationResourceManager.Instance["Result_Summary_Format"],
                        0)
                    : LocalizationResourceManager.Instance["Result_Summary_Updating"];

    partial void OnIsLoadingChanged(bool value) => NotifyStateProperties();

    partial void OnIsLoadingFavoritesChanged(bool value) => NotifyStateProperties();

    partial void OnFavoriteErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(ErrorMessage));
        NotifyStateProperties();
    }

    partial void OnHasErrorChanged(bool value) => NotifyStateProperties();

    partial void OnHasCompletedInitialLoadChanged(bool value)
    {
        NotifyStateProperties();
        OnPropertyChanged(nameof(ResultSummaryText));
    }

    private void NotifyStateProperties()
    {
        OnPropertyChanged(nameof(ShowLoadingState));
        OnPropertyChanged(nameof(ShowErrorState));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowContentState));
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchQuery));
        OnPropertyChanged(nameof(ResultSummaryText));
        OnPropertyChanged(nameof(ActiveFilterSummary));
    }

    partial void OnTotalCountChanged(int value)
    {
        TotalResultCount = value;
        OnPropertyChanged(nameof(ResultSummaryText));
        OnPropertyChanged(nameof(FilterShowResultsText));
    }

    partial void OnSelectedBottomTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsExploreTabSelected));
        OnPropertyChanged(nameof(IsFavoritesTabSelected));
        OnPropertyChanged(nameof(IsMessagesTabSelected));
        OnPropertyChanged(nameof(IsProfileTabSelected));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(IsLoadingMore));
        OnPropertyChanged(nameof(IsRefreshing));
        NotifyStateProperties();
    }

    partial void OnSelectedSortOptionItemChanged(RoomSortOptionItem value)
    {
        foreach (var option in SortOptions)
        {
            option.IsSelected = ReferenceEquals(option, value);
        }

        OnPropertyChanged(nameof(IsPriceSortSelected));
        OnPropertyChanged(nameof(PriceSortLabel));

        if (!_isInitialized || value is null || _suppressAutoSortReload)
        {
            return;
        }

        _ = LoadFirstPageAsync(isPullToRefresh: false);
    }

    [RelayCommand]
    public async Task SelectSortAsync(RoomSortOptionItem? option)
    {
        IsPriceMenuOpen = false;

        if (option is null || ReferenceEquals(SelectedSortOptionItem, option))
        {
            return;
        }

        _suppressAutoSortReload = true;
        try
        {
            SelectedSortOptionItem = option;
        }
        finally
        {
            _suppressAutoSortReload = false;
        }

        if (_isInitialized)
        {
            await LoadFirstPageAsync(isPullToRefresh: false);
        }
    }

    [RelayCommand]
    public void TogglePriceMenu()
    {
        IsFilterPanelOpen = !IsFilterPanelOpen;
        IsPriceMenuOpen = IsFilterPanelOpen;
    }

    [RelayCommand]
    public void ClosePriceMenu()
    {
        IsFilterPanelOpen = false;
        IsPriceMenuOpen = false;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task ToggleFavorite(RoomListItemModel? room)
    {
        if (room is null || _favoriteMutationsInFlight.Contains(room.Id) || _favouritesService is null)
        {
            return;
        }

        if (_sessionService?.IsAuthenticated != true)
        {
            FavoriteErrorMessage = "Sign in to save favorite rooms.";
            await ShowFavoriteMessageAsync(FavoriteErrorMessage);
            return;
        }

        var sessionEpoch = _sessionService.SessionEpoch;
        var userId = _sessionService.CurrentUser?.Id;
        var wasFavorite = room.IsFavorite;
        SetFavoriteMutationInFlight(room, true);
        FavoriteErrorMessage = string.Empty;
        try
        {
            if (wasFavorite)
            {
                var removedRoom = _favoriteRoomsById.GetValueOrDefault(room.Id, room);
                await _favouritesService.RemoveFavouriteAsync(room.Id);
                if (!IsCurrentFavoriteSession(sessionEpoch, userId))
                {
                    return;
                }

                removedRoom = _favoriteRoomsById.GetValueOrDefault(room.Id, removedRoom);
                ApplyFavoriteState(room, false);
                if (userId is Guid removedByUserId)
                {
                    await ShowUndoSnackbarAsync(removedRoom, sessionEpoch, removedByUserId);
                }
            }
            else
            {
                await _favouritesService.AddFavouriteAsync(room.Id);
                if (IsCurrentFavoriteSession(sessionEpoch, userId))
                {
                    ApplyFavoriteState(room, true);
                }
            }
        }
        catch (Exception ex)
        {
            FavoriteErrorMessage = "Unable to update favorites. Please try again.";
            await ShowFavoriteMessageAsync(FavoriteErrorMessage);
            _errorHandler.HandleError(ex);
        }
        finally
        {
            SetFavoriteMutationInFlight(room, false);
        }
    }

    [RelayCommand]
    public Task ToggleQuickFilterAsync(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Task.CompletedTask;
        }

        var trimmed = keyword.Trim();
        if (string.Equals(SearchQuery?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return ClearSearchAsync();
        }

        return ApplySearchAsync(trimmed);
    }

    [RelayCommand]
    public async Task SelectBottomTab(string? tabKey)
    {
        var normalized = string.IsNullOrWhiteSpace(tabKey) ? "explore" : tabKey.Trim().ToLowerInvariant();
        if (string.Equals(SelectedBottomTab, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        IsPriceMenuOpen = false;
        SelectedBottomTab = normalized;

        if (normalized == "explore")
        {
            PopulateExploreTab();
        }
        else if (normalized == "favorites")
        {
            Rooms.Clear();
            await LoadFavoritesAsync();
            if (IsFavoritesTabSelected)
            {
                PopulateFavoritesTab();
            }
        }
        else if (normalized == "messages")
        {
            Rooms.Clear();
            EmptyStateMessage = "No messages yet. Select a room to message the landlord directly.";
        }
        else if (normalized == "profile")
        {
            Rooms.Clear();
            EmptyStateMessage = "ICTU Student Account • Save favorite rooms and get notified about new listings near campus.";
        }
    }

    public async Task RefreshFavoritesAsync()
    {
        if (_sessionService?.IsAuthenticated == true && _favouritesService is not null)
        {
            await LoadFavoritesAsync(forceRefresh: true);
            if (IsFavoritesTabSelected)
            {
                PopulateFavoritesTab();
            }
        }
    }

    private Task LoadFavoritesAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _favoritesLoaded)
        {
            return Task.CompletedTask;
        }
        if (_favoritesLoadTask is { IsCompleted: false } inFlightTask)
        {
            return inFlightTask;
        }
        if (_favouritesService is null || _sessionService?.IsAuthenticated != true)
        {
            _favoriteRoomsById.Clear();
            _favoritesLoaded = true;
            return Task.CompletedTask;
        }

        var requestCts = new CancellationTokenSource();
        var sessionGeneration = _favoriteSessionGeneration;
        var sessionEpoch = _sessionService.SessionEpoch;
        var userId = _sessionService.CurrentUser?.Id;
        _favoriteLoadCts = requestCts;
        IsLoadingFavorites = true;
        FavoriteErrorMessage = string.Empty;
        _favoritesLoadTask = LoadFavoritesCoreAsync(requestCts, sessionGeneration, sessionEpoch, userId);
        return _favoritesLoadTask;
    }

    private async Task LoadFavoritesCoreAsync(
        CancellationTokenSource requestCts,
        long sessionGeneration,
        long sessionEpoch,
        Guid? userId)
    {
        try
        {
            IReadOnlyList<RoomListItemModel> favourites;
            long mutationVersion;
            do
            {
                mutationVersion = _favoriteMutationVersion;
                favourites = await _favouritesService!.GetMyFavouritesAsync(requestCts.Token);
                if (!IsCurrentFavoriteRequest(sessionGeneration, sessionEpoch, userId, requestCts))
                {
                    return;
                }
            }
            while (mutationVersion != _favoriteMutationVersion);

            _favoriteRoomsById.Clear();
            foreach (var favourite in favourites)
            {
                favourite.IsFavorite = true;
                favourite.IsFavoriteBusy = _favoriteMutationsInFlight.Contains(favourite.Id);
                _favoriteRoomsById[favourite.Id] = favourite;
            }

            foreach (var room in _exploreSnapshot)
            {
                ApplyCachedFavoriteState(room);
            }
            _favoritesLoaded = true;
            FavoriteErrorMessage = string.Empty;
            OnPropertyChanged(nameof(FavoriteCount));
            OnPropertyChanged(nameof(HasFavorites));
        }
        catch (Exception ex)
        {
            if (IsCurrentFavoriteRequest(sessionGeneration, sessionEpoch, userId, requestCts))
            {
                FavoriteErrorMessage = "Unable to load favorite rooms. Please try again.";
                _errorHandler.HandleError(ex);
            }
        }
        finally
        {
            if (ReferenceEquals(_favoriteLoadCts, requestCts))
            {
                _favoriteLoadCts = null;
                _favoritesLoadTask = null;
                IsLoadingFavorites = false;
            }
            requestCts.Dispose();
        }
    }

    private bool IsCurrentFavoriteRequest(
        long sessionGeneration,
        long sessionEpoch,
        Guid? userId,
        CancellationTokenSource requestCts) =>
        !requestCts.IsCancellationRequested
        && sessionGeneration == _favoriteSessionGeneration
        && IsCurrentFavoriteSession(sessionEpoch, userId);

    private bool IsCurrentFavoriteSession(long sessionEpoch, Guid? userId) =>
        _sessionService?.IsAuthenticated == true
        && _sessionService.SessionEpoch == sessionEpoch
        && _sessionService.CurrentUser?.Id == userId;

    private void PopulateExploreTab()
    {
        Rooms.Clear();
        foreach (var room in _exploreSnapshot)
        {
            Rooms.Add(room);
        }
        EmptyStateMessage = Rooms.Count == 0
            ? HasError ? ExploreErrorMessage : LocalizationResourceManager.Instance["Empty_NoRooms"]
            : string.Empty;
    }

    private void PopulateFavoritesTab()
    {
        Rooms.Clear();
        foreach (var favourite in _favoriteRoomsById.Values)
        {
            Rooms.Add(favourite);
        }
        EmptyStateMessage = Rooms.Count == 0
            ? "No favorite rooms yet. Tap the heart icon on any room card to save it!"
            : string.Empty;
    }

    private void ApplyFavoriteState(RoomListItemModel room, bool isFavorite)
    {
        _favoriteMutationVersion++;
        room.IsFavorite = isFavorite;
        foreach (var exploreRoom in _exploreSnapshot.Where(item => item.Id == room.Id))
        {
            exploreRoom.IsFavorite = isFavorite;
        }

        if (isFavorite)
        {
            _favoriteRoomsById.TryAdd(room.Id, room);
            if (IsFavoritesTabSelected && !Rooms.Any(item => item.Id == room.Id))
            {
                Rooms.Add(_favoriteRoomsById[room.Id]);
                EmptyStateMessage = string.Empty;
            }
        }
        else
        {
            if (_favoriteRoomsById.Remove(room.Id, out var favoriteRoom))
            {
                favoriteRoom.IsFavorite = false;
                favoriteRoom.IsFavoriteBusy = false;
            }
            if (IsFavoritesTabSelected && Rooms.FirstOrDefault(item => item.Id == room.Id) is { } displayedRoom)
            {
                Rooms.Remove(displayedRoom);
                EmptyStateMessage = Rooms.Count == 0
                    ? "No favorite rooms yet. Tap the heart icon on any room card to save it!"
                    : string.Empty;
            }
        }
        OnPropertyChanged(nameof(FavoriteCount));
        OnPropertyChanged(nameof(HasFavorites));
    }

    private void SetFavoriteMutationInFlight(RoomListItemModel room, bool isBusy)
    {
        if (isBusy)
        {
            _favoriteMutationsInFlight.Add(room.Id);
        }
        else
        {
            _favoriteMutationsInFlight.Remove(room.Id);
        }

        room.IsFavoriteBusy = isBusy;
        if (_favoriteRoomsById.TryGetValue(room.Id, out var favoriteRoom))
        {
            favoriteRoom.IsFavoriteBusy = isBusy;
        }
        foreach (var exploreRoom in _exploreSnapshot.Where(item => item.Id == room.Id))
        {
            exploreRoom.IsFavoriteBusy = isBusy;
        }
    }

    private async Task ShowUndoSnackbarAsync(RoomListItemModel room, long sessionEpoch, Guid userId)
    {
        if (_favoriteFeedbackService is not null)
        {
            await _favoriteFeedbackService.ShowUndoAsync(
                $"Removed {room.Title} from favorites",
                "Undo",
                () => RestoreFavouriteAsync(room, sessionEpoch, userId));
        }
    }

    private async Task RestoreFavouriteAsync(RoomListItemModel room, long sessionEpoch, Guid userId)
    {
        if (_favouritesService is null || !IsCurrentFavoriteSession(sessionEpoch, userId) || _favoriteMutationsInFlight.Contains(room.Id))
        {
            return;
        }
        SetFavoriteMutationInFlight(room, true);
        try
        {
            await _favouritesService.AddFavouriteAsync(room.Id);
            if (!IsCurrentFavoriteSession(sessionEpoch, userId))
            {
                return;
            }

            ApplyFavoriteState(room, true);
            await ShowFavoriteMessageAsync($"Restored {room.Title} to favorites");
        }
        catch (Exception ex)
        {
            FavoriteErrorMessage = "Unable to restore this favorite. Please try again.";
            await ShowFavoriteMessageAsync(FavoriteErrorMessage);
            _errorHandler.HandleError(ex);
        }
        finally
        {
            SetFavoriteMutationInFlight(room, false);
        }
    }

    private async Task ShowFavoriteMessageAsync(string message)
    {
        if (_favoriteFeedbackService is not null)
        {
            await _favoriteFeedbackService.ShowMessageAsync(message);
        }
    }

    [RelayCommand]
    public void OpenMenu()
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        if (Microsoft.Maui.Controls.Shell.Current is not null)
        {
            Microsoft.Maui.Controls.Shell.Current.FlyoutIsPresented = true;
        }
#endif
    }

    [RelayCommand]
    public void OpenNotifications()
    {
        IsPriceMenuOpen = false;
    }

    [RelayCommand]
    public async Task OpenProfile()
    {
        await SelectBottomTab("profile");
    }

    [RelayCommand]
    public async Task OpenCreateListingAsync()
    {
        if (Shell.Current != null)
        {
            try
            {
                await Shell.Current.GoToAsync(Core.Constants.AppRoutes.CreateListing);
            }
            catch
            {
                await Shell.Current.GoToAsync("//create-listing");
            }
        }
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        if (_sessionService != null)
        {
            if (!_sessionService.IsInitialized)
            {
                await _sessionService.EnsureInitializedAsync();
            }
            UpdateLandlordStatus();
        }

        if (_isInitialized)
        {
            await RefreshFavoritesAsync();
            return;
        }

        _isInitialized = true;
        await LoadFirstPageAsync(isPullToRefresh: false);
        await RefreshFavoritesAsync();
    }

    [RelayCommand]
    public async Task RetryAsync()
    {
        _isInitialized = true;
        if (IsFavoritesTabSelected)
        {
            await RefreshFavoritesAsync();
            return;
        }
        await LoadFirstPageAsync(isPullToRefresh: false);
    }

    protected override Task ExecuteSearchAsync()
    {
        _isInitialized = true;
        return LoadFirstPageAsync(isPullToRefresh: false);
    }

    [RelayCommand]
    public void ToggleAmenityFilter(string? amenityKey)
    {
        switch (amenityKey?.Trim().ToLowerInvariant())
        {
            case "aircon":
                IsAmenityAirConSelected = !IsAmenityAirConSelected;
                break;
            case "wifi":
                IsAmenityWifiSelected = !IsAmenityWifiSelected;
                break;
            case "parking":
                IsAmenityParkingSelected = !IsAmenityParkingSelected;
                break;
            case "bathroom":
                IsAmenityBathroomSelected = !IsAmenityBathroomSelected;
                break;
            case "washing":
                IsAmenityWashingSelected = !IsAmenityWashingSelected;
                break;
            case "kitchen":
                IsAmenityKitchenSelected = !IsAmenityKitchenSelected;
                break;
        }
    }

    [RelayCommand]
    public Task ApplyFilterSheetAsync()
    {
        IsFilterPanelOpen = false;
        IsPriceMenuOpen = false;
        return ApplyFiltersAsync();
    }

    public override async Task ResetFiltersAsync()
    {
        SearchQuery = string.Empty;
        IsAmenityAirConSelected = false;
        IsAmenityWifiSelected = false;
        IsAmenityParkingSelected = false;
        IsAmenityBathroomSelected = false;
        IsAmenityWashingSelected = false;
        IsAmenityKitchenSelected = false;

        _suppressAutoSortReload = true;
        try
        {
            SelectedSortOptionItem = SortOptions[0];
        }
        finally
        {
            _suppressAutoSortReload = false;
        }

        IsPriceMenuOpen = false;

        await base.ResetFiltersAsync();
    }

    [RelayCommand]
    public async Task RefreshRoomsAsync()
    {
        if (IsFavoritesTabSelected)
        {
            SetProperty(ref _isRefreshingFavorites, true, nameof(IsRefreshing));
            try
            {
                await RefreshFavoritesAsync();
            }
            finally
            {
                SetProperty(ref _isRefreshingFavorites, false, nameof(IsRefreshing));
            }
            return;
        }

        _isInitialized = true;
        await LoadFirstPageAsync(isPullToRefresh: true);
    }

    [RelayCommand]
    public Task ApplySearchAsync(string? query)
    {
        SelectedBottomTab = "explore";
        SearchQuery = query?.Trim() ?? string.Empty;
        _isInitialized = true;
        return LoadFirstPageAsync(isPullToRefresh: false);
    }

    [RelayCommand]
    public Task ClearSearchAsync()
    {
        if (!HasSearchQuery)
        {
            return Task.CompletedTask;
        }

        SearchQuery = string.Empty;
        _isInitialized = true;
        return LoadFirstPageAsync(isPullToRefresh: false);
    }

#if ANDROID || IOS || MACCATALYST || WINDOWS
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("search", out var rawSearch))
        {
            var decoded = Uri.UnescapeDataString(rawSearch?.ToString() ?? string.Empty);
            _ = ApplySearchAsync(decoded);
        }
    }
#endif

    [RelayCommand]
    public async Task LoadMoreRoomsAsync()
    {
        // Rule 1: Re-entrancy and boundary guard
        if (!IsExploreTabSelected || IsLoading || IsRefreshing || IsLoadingMore || !HasMoreItems || CurrentPage < 1)
        {
            return;
        }

        var cts = _paginationCts;
        var token = cts?.Token ?? CancellationToken.None;
        var nextPage = CurrentPage + 1;

        try
        {
            IsLoadingMore = true;

            var sortOption = SelectedSortOptionItem?.Option ?? RoomSortOption.DistanceAsc;
            PagedResponse<RoomListItemModel> response;
            if (HasActiveFilters && (MinArea.HasValue || MaxArea.HasValue || IsDistanceFilterActive))
            {
                var criteria = CreateSearchCriteria(
                    pageNumber: nextPage,
                    pageSize: PageSize,
                    keyword: GetActiveSearchTerm(),
                    sortBy: sortOption);

                var searchResult = await _roomListingsService.SearchRoomsAsync(criteria, token);

                var items = searchResult.Items
                    .Select(dto => dto.ToListItemModel())
                    .ToList();

                response = new PagedResponse<RoomListItemModel>(
                    items,
                    searchResult.CurrentPage,
                    PageSize,
                    searchResult.TotalCount);
            }
            else
            {
                response = await _roomListingsService.GetRoomListingsAsync(
                    nextPage,
                    PageSize,
                    sortOption,
                    GetActiveSearchTerm(),
                    BuildFilterCriteria(),
                    token);
            }

            if (token.IsCancellationRequested || !IsExploreTabSelected || !ReferenceEquals(_paginationCts, cts))
            {
                return;
            }

            // Rule 2: Deduplicate items by Id while mutating ObservableCollection in-place
            var existingIds = Rooms.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var room in response.Items)
            {
                if (existingIds.Add(room.Id))
                {
                    ApplyCachedFavoriteState(room);
                    _exploreSnapshot.Add(room);
                    Rooms.Add(room);
                }
            }

            CurrentPage = nextPage;
            TotalCount = response.TotalCount;

            // Rule 3: Update HasMoreItems accurately
            HasMoreItems = response.Items.Count >= PageSize && Rooms.Count < response.TotalCount;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Superseded by a newer refresh or sort operation
        }
        catch (Exception ex) when (!token.IsCancellationRequested && ReferenceEquals(_paginationCts, cts))
        {
            // Rule 5: Preserve existing items and CurrentPage when page > 1 fails
            _errorHandler.HandleError(ex);
        }
        finally
        {
            if (ReferenceEquals(_paginationCts, cts))
            {
                IsLoadingMore = false;
            }
        }
    }

    [RelayCommand]
    public async Task OpenRoomDetailsAsync(RoomListItemModel? room)
    {
        if (room is null || string.IsNullOrWhiteSpace(room.Id))
        {
            return;
        }

        var targetRoute = $"{AppRoutes.RoomDetail}?id={Uri.EscapeDataString(room.Id)}";

        try
        {
            if (_navigateAsync is not null)
            {
                await _navigateAsync(targetRoute);
                return;
            }

#if ANDROID || IOS || MACCATALYST || WINDOWS
            if (Microsoft.Maui.Controls.Shell.Current is not null)
            {
                await Microsoft.Maui.Controls.Shell.Current.GoToAsync(targetRoute);
            }
#endif
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    private async Task LoadFirstPageAsync(bool isPullToRefresh)
    {
        // Rule 4: Cancel any in-flight pagination request when refreshing or changing sort
        var currentCts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _paginationCts, currentCts);
        previousCts?.Cancel();
        previousCts?.Dispose();

        var token = currentCts.Token;

        try
        {
            HasError = false;
            _lastErrorResourceKey = null;
            ExploreErrorMessage = string.Empty;

            if (isPullToRefresh)
            {
                SetProperty(ref _isRefreshingExplore, true, nameof(IsRefreshing));
                IsLoading = false;
            }
            else
            {
                IsLoading = true;
                SetProperty(ref _isRefreshingExplore, false, nameof(IsRefreshing));
                if (IsExploreTabSelected)
                {
                    Rooms.Clear();
                }
            }

            IsLoadingMore = false;

            var sortOption = SelectedSortOptionItem?.Option ?? RoomSortOption.DistanceAsc;

            // Search and filter are executed server-side so results cover the whole catalog and stay paginated.
            PagedResponse<RoomListItemModel> response;
            if (HasActiveFilters && (MinArea.HasValue || MaxArea.HasValue || IsDistanceFilterActive))
            {
                var criteria = CreateSearchCriteria(
                    pageNumber: 1,
                    pageSize: PageSize,
                    keyword: GetActiveSearchTerm(),
                    sortBy: sortOption);

                var searchResult = await _roomListingsService.SearchRoomsAsync(criteria, token);

                var items = searchResult.Items
                    .Select(dto => dto.ToListItemModel())
                    .ToList();

                response = new PagedResponse<RoomListItemModel>(
                    items,
                    searchResult.CurrentPage,
                    PageSize,
                    searchResult.TotalCount);
            }
            else
            {
                response = await _roomListingsService.GetRoomListingsAsync(
                    page: 1,
                    pageSize: PageSize,
                    sortBy: sortOption,
                    search: GetActiveSearchTerm(),
                    filter: BuildFilterCriteria(),
                    cancellationToken: token);
            }

            if (token.IsCancellationRequested || !ReferenceEquals(_paginationCts, currentCts))
            {
                return;
            }

            _exploreSnapshot.Clear();
            var existingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var room in response.Items)
            {
                if (existingIds.Add(room.Id))
                {
                    ApplyCachedFavoriteState(room);
                    _exploreSnapshot.Add(room);
                }
            }

            CurrentPage = 1;
            TotalCount = response.TotalCount;
            HasMoreItems = response.Items.Count >= PageSize && _exploreSnapshot.Count < response.TotalCount;
            HasCompletedInitialLoad = true;
            HasError = false;
            _lastErrorResourceKey = null;
            if (IsExploreTabSelected)
            {
                PopulateExploreTab();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Superseded by a newer request
        }
        catch (Exception ex) when (!token.IsCancellationRequested && ReferenceEquals(_paginationCts, currentCts))
        {
            HasCompletedInitialLoad = true;
            HasError = true;
            _lastErrorResourceKey = ex is HttpRequestException
                ? "State_ErrorNetwork"
                : "State_ErrorServer";
            ExploreErrorMessage = LocalizationResourceManager.Instance[_lastErrorResourceKey];

            if (IsExploreTabSelected && Rooms.Count == 0)
            {
                EmptyStateMessage = ErrorMessage;
            }

            _errorHandler.HandleError(ex);
        }
        finally
        {
            if (ReferenceEquals(_paginationCts, currentCts))
            {
                if (isPullToRefresh)
                {
                    SetProperty(ref _isRefreshingExplore, false, nameof(IsRefreshing));
                }
                else
                {
                    IsLoading = false;
                }

                NotifyStateProperties();
            }
        }
    }

    private string? GetActiveSearchTerm() =>
        HasSearchQuery ? SearchQuery.Trim() : null;

    public RoomFilterCriteria BuildFilterCriteria()
    {
        var minPrice = TryParsePrice(FilterMinPriceText);
        var maxPrice = TryParsePrice(FilterMaxPriceText);
        var roomType = string.IsNullOrWhiteSpace(SelectedRoomType) || string.Equals(SelectedRoomType, "all", StringComparison.OrdinalIgnoreCase)
            ? null
            : SelectedRoomType.Trim().ToLowerInvariant();
        var amenities = GetSelectedAmenities();

        return new RoomFilterCriteria
        {
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            RoomType = roomType,
            Amenities = amenities
        };
    }

    protected override void OnFilterRemoved(string key)
    {
        if (key.StartsWith("Amenity_", StringComparison.OrdinalIgnoreCase))
        {
            var amenity = key["Amenity_".Length..];
            switch (amenity.ToLowerInvariant())
            {
                case "aircon":
                    IsAmenityAirConSelected = false;
                    break;
                case "wifi":
                    IsAmenityWifiSelected = false;
                    break;
                case "parking":
                    IsAmenityParkingSelected = false;
                    break;
                case "bathroom":
                    IsAmenityBathroomSelected = false;
                    break;
                case "washing":
                    IsAmenityWashingSelected = false;
                    break;
                case "kitchen":
                    IsAmenityKitchenSelected = false;
                    break;
            }
        }
    }

    protected override void PopulateActiveFilters()
    {
        base.PopulateActiveFilters();

        if (IsAmenityAirConSelected)
        {
            ActiveFilters.Add(new ActiveFilterItem
            {
                Key = "Amenity_aircon",
                DisplayText = LocalizationResourceManager.Instance["Filter_Amenity_AirCon"],
                FilterType = "amenity"
            });
        }

        if (IsAmenityWifiSelected)
        {
            ActiveFilters.Add(new ActiveFilterItem
            {
                Key = "Amenity_wifi",
                DisplayText = LocalizationResourceManager.Instance["Filter_Amenity_Wifi"],
                FilterType = "amenity"
            });
        }

        if (IsAmenityParkingSelected)
        {
            ActiveFilters.Add(new ActiveFilterItem
            {
                Key = "Amenity_parking",
                DisplayText = LocalizationResourceManager.Instance["Filter_Amenity_Parking"],
                FilterType = "amenity"
            });
        }

        if (IsAmenityBathroomSelected)
        {
            ActiveFilters.Add(new ActiveFilterItem
            {
                Key = "Amenity_bathroom",
                DisplayText = LocalizationResourceManager.Instance["Filter_Amenity_Bathroom"],
                FilterType = "amenity"
            });
        }

        if (IsAmenityWashingSelected)
        {
            ActiveFilters.Add(new ActiveFilterItem
            {
                Key = "Amenity_washing",
                DisplayText = LocalizationResourceManager.Instance["Filter_Amenity_Washing"],
                FilterType = "amenity"
            });
        }

        if (IsAmenityKitchenSelected)
        {
            ActiveFilters.Add(new ActiveFilterItem
            {
                Key = "Amenity_kitchen",
                DisplayText = LocalizationResourceManager.Instance["Filter_Amenity_Kitchen"],
                FilterType = "amenity"
            });
        }
    }

    public override IReadOnlyList<string> GetSelectedAmenities()
    {
        var list = new List<string>(6);
        if (IsAmenityAirConSelected)
        {
            list.Add("aircon");
        }

        if (IsAmenityWifiSelected)
        {
            list.Add("wifi");
        }

        if (IsAmenityParkingSelected)
        {
            list.Add("parking");
        }

        if (IsAmenityBathroomSelected)
        {
            list.Add("bathroom");
        }

        if (IsAmenityWashingSelected)
        {
            list.Add("washing");
        }

        if (IsAmenityKitchenSelected)
        {
            list.Add("kitchen");
        }

        return list;
    }

    private void ApplyCachedFavoriteState(RoomListItemModel room)
    {
        room.IsFavorite = _favoriteRoomsById.ContainsKey(room.Id);
        room.IsFavoriteBusy = _favoriteMutationsInFlight.Contains(room.Id);
    }
}
