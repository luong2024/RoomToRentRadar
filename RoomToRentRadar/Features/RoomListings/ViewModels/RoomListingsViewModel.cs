using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Localization;
using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Features.RoomListings.Services;

namespace RoomToRentRadar.Features.RoomListings.ViewModels;

/// <summary>
/// Manages presentation state, sorting, pull-to-refresh, and infinite scroll pagination
/// for the room listings screen.
/// </summary>
public partial class RoomListingsViewModel : ObservableObject
#if ANDROID || IOS || MACCATALYST || WINDOWS
    , IQueryAttributable
#endif
{
    public const int DefaultPageSize = 6;

    private readonly IRoomListingsService _roomListingsService;
    private readonly IErrorHandler _errorHandler;
    private readonly Func<string, Task>? _navigateAsync;
    private readonly Dictionary<string, RoomListItemModel> _favoriteRoomsById = new(StringComparer.Ordinal);
    private readonly List<RoomListItemModel> _exploreSnapshot = [];
    private CancellationTokenSource? _paginationCts;
    private string? _lastErrorResourceKey;
    private bool _isInitialized;
    private bool _suppressAutoSortReload;
    private bool _isUpdatingPricePreset;

    public RoomListingsViewModel(
        IRoomListingsService roomListingsService,
        IErrorHandler errorHandler,
        Func<string, Task>? navigateAsync = null)
    {
        ArgumentNullException.ThrowIfNull(roomListingsService);
        ArgumentNullException.ThrowIfNull(errorHandler);

        _roomListingsService = roomListingsService;
        _errorHandler = errorHandler;
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

        UpdateLocalizedStrings();
        LocalizationResourceManager.Instance.PropertyChanged += (_, _) => UpdateLocalizedStrings();
    }

    private void UpdateLocalizedStrings()
    {
        Title = LocalizationResourceManager.Instance["Title_ExploreRooms"];

        if (HasError && !string.IsNullOrEmpty(_lastErrorResourceKey))
        {
            ErrorMessage = LocalizationResourceManager.Instance[_lastErrorResourceKey];
            if (Rooms.Count == 0)
            {
                EmptyStateMessage = ErrorMessage;
            }
        }
        else if (Rooms.Count == 0)
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

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasCompletedInitialLoad { get; set; }

    public bool ShowLoadingState => IsLoading && Rooms.Count == 0;

    public bool ShowErrorState => !IsLoading && HasError && Rooms.Count == 0;

    public bool ShowEmptyState => !IsLoading && !HasError && HasCompletedInitialLoad && Rooms.Count == 0;

    public bool ShowContentState => !IsLoading && !HasError && Rooms.Count > 0;

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    public partial bool HasMoreItems { get; set; } = true;

    [ObservableProperty]
    public partial int CurrentPage { get; set; }

    [ObservableProperty]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    public partial string EmptyStateMessage { get; set; } = "No matching rooms found.";

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPriceMenuOpen { get; set; }

    [ObservableProperty]
    public partial string SelectedPricePreset { get; set; } = "1m-2m";

    [ObservableProperty]
    public partial string FilterMinPriceText { get; set; } = "1,000,000";

    [ObservableProperty]
    public partial string FilterMaxPriceText { get; set; } = "2,000,000";

    [ObservableProperty]
    public partial string SelectedRoomType { get; set; } = "private";

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

    public bool IsPriceUnder1MSelected =>
        string.Equals(SelectedPricePreset, "under1m", StringComparison.OrdinalIgnoreCase);

    public bool IsPrice1MTo2MSelected =>
        string.Equals(SelectedPricePreset, "1m-2m", StringComparison.OrdinalIgnoreCase);

    public bool IsPrice2MTo3MSelected =>
        string.Equals(SelectedPricePreset, "2m-3m", StringComparison.OrdinalIgnoreCase);

    public bool IsPrivateRoomSelected =>
        string.Equals(SelectedRoomType, "private", StringComparison.OrdinalIgnoreCase);

    public bool IsSharedRoomSelected =>
        string.Equals(SelectedRoomType, "shared", StringComparison.OrdinalIgnoreCase);

    public string FilterShowResultsText =>
        string.Format(
            LocalizationResourceManager.Instance.CurrentCulture,
            LocalizationResourceManager.Instance["Filter_ShowResults_Format"],
            TotalCount > 0 ? TotalCount : 24);

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
                : LocalizationResourceManager.Instance["Result_Summary_Updating"];

    partial void OnIsLoadingChanged(bool value) => NotifyStateProperties();

    partial void OnHasErrorChanged(bool value) => NotifyStateProperties();

    partial void OnHasCompletedInitialLoadChanged(bool value) => NotifyStateProperties();

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
        OnPropertyChanged(nameof(ResultSummaryText));
        OnPropertyChanged(nameof(FilterShowResultsText));
    }

    partial void OnSelectedPricePresetChanged(string value)
    {
        OnPropertyChanged(nameof(IsPriceUnder1MSelected));
        OnPropertyChanged(nameof(IsPrice1MTo2MSelected));
        OnPropertyChanged(nameof(IsPrice2MTo3MSelected));
    }

    partial void OnFilterMinPriceTextChanged(string value) => SyncPricePresetFromInputs();

    partial void OnFilterMaxPriceTextChanged(string value) => SyncPricePresetFromInputs();

    private void SyncPricePresetFromInputs()
    {
        if (_isUpdatingPricePreset)
        {
            return;
        }

        var min = TryParsePrice(FilterMinPriceText);
        var max = TryParsePrice(FilterMaxPriceText);

        SelectedPricePreset = (min, max) switch
        {
            (0m, 1_000_000m) => "under1m",
            (1_000_000m, 2_000_000m) => "1m-2m",
            (2_000_000m, 3_000_000m) => "2m-3m",
            _ => string.Empty
        };
    }

    partial void OnSelectedRoomTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsPrivateRoomSelected));
        OnPropertyChanged(nameof(IsSharedRoomSelected));
    }

    partial void OnSelectedBottomTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsExploreTabSelected));
        OnPropertyChanged(nameof(IsFavoritesTabSelected));
        OnPropertyChanged(nameof(IsMessagesTabSelected));
        OnPropertyChanged(nameof(IsProfileTabSelected));
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
        IsPriceMenuOpen = !IsPriceMenuOpen;
    }

    [RelayCommand]
    public void ClosePriceMenu()
    {
        IsPriceMenuOpen = false;
    }

    [RelayCommand]
    public void ToggleFavorite(RoomListItemModel? room)
    {
        if (room is null)
        {
            return;
        }

        room.ToggleFavorite();
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
    public void SelectBottomTab(string? tabKey)
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
            Rooms.Clear();
            foreach (var item in _exploreSnapshot)
            {
                Rooms.Add(item);
            }

            EmptyStateMessage = Rooms.Count == 0
                ? LocalizationResourceManager.Instance["Empty_NoRooms"]
                : string.Empty;
        }
        else if (normalized == "favorites")
        {
            Rooms.Clear();
            foreach (var fav in _favoriteRoomsById.Values)
            {
                Rooms.Add(fav);
            }

            EmptyStateMessage = Rooms.Count == 0
                ? "No favorite rooms yet. Tap the heart icon on any room card to save it!"
                : string.Empty;
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
    public void OpenProfile()
    {
        SelectBottomTab("profile");
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await LoadFirstPageAsync(isPullToRefresh: false);
    }

    [RelayCommand]
    public Task RetryAsync()
    {
        _isInitialized = true;
        return LoadFirstPageAsync(isPullToRefresh: false);
    }

    [RelayCommand]
    public void SelectPricePreset(string? preset)
    {
        var normalized = preset?.Trim().ToLowerInvariant() ?? string.Empty;
        _isUpdatingPricePreset = true;
        try
        {
            SelectedPricePreset = normalized;

            switch (normalized)
            {
                case "under1m":
                    FilterMinPriceText = "0";
                    FilterMaxPriceText = "1,000,000";
                    break;
                case "1m-2m":
                    FilterMinPriceText = "1,000,000";
                    FilterMaxPriceText = "2,000,000";
                    break;
                case "2m-3m":
                    FilterMinPriceText = "2,000,000";
                    FilterMaxPriceText = "3,000,000";
                    break;
            }
        }
        finally
        {
            _isUpdatingPricePreset = false;
        }
    }

    [RelayCommand]
    public void SelectRoomType(string? roomType)
    {
        SelectedRoomType = string.IsNullOrWhiteSpace(roomType)
            ? "private"
            : roomType.Trim().ToLowerInvariant();
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
        IsPriceMenuOpen = false;
        _isInitialized = true;
        return LoadFirstPageAsync(isPullToRefresh: false);
    }

    [RelayCommand]
    public Task ResetFiltersAsync()
    {
        SearchQuery = string.Empty;
        SelectPricePreset("1m-2m");
        SelectedRoomType = "private";
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
        _isInitialized = true;
        return LoadFirstPageAsync(isPullToRefresh: false);
    }

    [RelayCommand]
    public Task RefreshRoomsAsync()
    {
        _isInitialized = true;
        return LoadFirstPageAsync(isPullToRefresh: true);
    }

    [RelayCommand]
    public Task ApplySearchAsync(string? query)
    {
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
        if (IsLoading || IsRefreshing || IsLoadingMore || !HasMoreItems || CurrentPage < 1)
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
            var response = await _roomListingsService.GetRoomListingsAsync(
                nextPage,
                PageSize,
                sortOption,
                GetActiveSearchTerm(),
                BuildFilterCriteria(),
                token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            // Rule 2: Deduplicate items by Id while mutating ObservableCollection in-place
            var existingIds = Rooms.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var room in response.Items)
            {
                if (existingIds.Add(room.Id))
                {
                    AttachRoomItem(room);
                    Rooms.Add(room);
                    _exploreSnapshot.Add(room);
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
            ErrorMessage = string.Empty;

            if (isPullToRefresh)
            {
                IsRefreshing = true;
                IsLoading = false;
            }
            else
            {
                IsLoading = true;
                IsRefreshing = false;
                Rooms.Clear();
            }

            IsLoadingMore = false;

            var sortOption = SelectedSortOptionItem?.Option ?? RoomSortOption.DistanceAsc;

            // Search and filter are executed server-side so results cover the whole catalog and stay paginated.
            var response = await _roomListingsService.GetRoomListingsAsync(
                page: 1,
                pageSize: PageSize,
                sortBy: sortOption,
                search: GetActiveSearchTerm(),
                filter: BuildFilterCriteria(),
                cancellationToken: token);

            if (token.IsCancellationRequested || !ReferenceEquals(_paginationCts, currentCts))
            {
                return;
            }

            if (!IsExploreTabSelected)
            {
                SelectedBottomTab = "explore";
            }

            foreach (var oldRoom in _exploreSnapshot)
            {
                oldRoom.PropertyChanged -= OnRoomItemPropertyChanged;
            }

            _exploreSnapshot.Clear();
            Rooms.Clear();
            var existingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var room in response.Items)
            {
                if (existingIds.Add(room.Id))
                {
                    AttachRoomItem(room);
                    Rooms.Add(room);
                    _exploreSnapshot.Add(room);
                }
            }

            CurrentPage = 1;
            TotalCount = response.TotalCount;
            HasMoreItems = response.Items.Count >= PageSize && Rooms.Count < response.TotalCount;
            HasCompletedInitialLoad = true;
            HasError = false;
            _lastErrorResourceKey = null;
            ErrorMessage = string.Empty;
            EmptyStateMessage = Rooms.Count == 0
                ? LocalizationResourceManager.Instance["Empty_NoRooms"]
                : string.Empty;
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
            ErrorMessage = LocalizationResourceManager.Instance[_lastErrorResourceKey];

            if (Rooms.Count == 0)
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
                    IsRefreshing = false;
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
        var roomType = string.IsNullOrWhiteSpace(SelectedRoomType)
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

    private IReadOnlyList<string> GetSelectedAmenities()
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

    private static decimal? TryParsePrice(string? priceText)
    {
        if (string.IsNullOrWhiteSpace(priceText))
        {
            return null;
        }

        var cleaned = priceText
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("đ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("₫", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0)
        {
            return value;
        }

        return null;
    }

    private void AttachRoomItem(RoomListItemModel room)
    {
        room.PropertyChanged -= OnRoomItemPropertyChanged;
        room.IsFavorite = _favoriteRoomsById.ContainsKey(room.Id);
        if (room.IsFavorite)
        {
            _favoriteRoomsById[room.Id] = room;
        }

        room.PropertyChanged += OnRoomItemPropertyChanged;
    }

    private void OnRoomItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not RoomListItemModel room || e.PropertyName != nameof(RoomListItemModel.IsFavorite))
        {
            return;
        }

        if (room.IsFavorite)
        {
            _favoriteRoomsById[room.Id] = room;
        }
        else
        {
            _favoriteRoomsById.Remove(room.Id);
            if (IsFavoritesTabSelected)
            {
                Rooms.Remove(room);
                if (Rooms.Count == 0)
                {
                    EmptyStateMessage = "No favorite rooms yet. Tap the heart icon on any room card to save it!";
                }
            }
        }

        OnPropertyChanged(nameof(FavoriteCount));
        OnPropertyChanged(nameof(HasFavorites));
    }
}
