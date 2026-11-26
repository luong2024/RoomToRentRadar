using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private const int PageSize = 12;

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IApiClient _apiClient;
    private readonly IErrorHandler _errorHandler;
    private readonly INavigationService _navigationService;
    private readonly IRoomComparisonStore _comparisonStore;
    private readonly AuthService? _authService;
    private readonly SynchronizationContext? _uiContext;
    private bool _isAuthSubscribed;
    private CancellationTokenSource? _searchCancellation;
    private CancellationTokenSource? _compareHintCancellation;
    private readonly HashSet<Guid> _listingIds = new();
    private string? _nextCursor;
    private int _generation;
    private ActiveListingFiltersDto? _appliedFilters;
    private RoomType? _draftRoomType;
    private CampusOption? _draftCampus;
    private int _draftDistanceKm = HomeFilterOptions.DefaultDistanceKm;
    private bool _hasLoaded;

    [ObservableProperty]
    public partial bool IsSearchOpen { get; set; }

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    public partial bool HasMore { get; set; }

    [ObservableProperty]
    public partial string ResultsText { get; set; }

    [ObservableProperty]
    public partial string EmptyMessage { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial HomeListingItem? SelectedListing { get; set; }

    [ObservableProperty]
    public partial bool IsFilterPanelOpen { get; set; }

    [ObservableProperty]
    public partial string PriceMinText { get; set; }

    [ObservableProperty]
    public partial string PriceMaxText { get; set; }

    [ObservableProperty]
    public partial string AreaMinText { get; set; }

    [ObservableProperty]
    public partial string AreaMaxText { get; set; }

    [ObservableProperty]
    public partial string FilterError { get; set; }

    [ObservableProperty]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    public partial bool IsTotalCountCapped { get; set; }

    [ObservableProperty]
    public partial string CompareHint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSignedIn { get; set; }

    [ObservableProperty]
    public partial string SignedInBadgeText { get; set; } = string.Empty;

    public HomeViewModel(
        IApiClient apiClient,
        IErrorHandler errorHandler,
        INavigationService navigationService,
        IRoomComparisonStore comparisonStore,
        AuthService? authService = null)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
        _navigationService = navigationService;
        _comparisonStore = comparisonStore;
        _authService = authService;
        _uiContext = SynchronizationContext.Current;
        if (_authService is not null)
        {
            AttachAuthSession();
        }
        ResultsText = "Đang tải phòng trọ…";
        EmptyMessage = "Đang tải phòng trọ…";
        ErrorMessage = string.Empty;
        FilterError = string.Empty;
        PriceMinText = string.Empty;
        PriceMaxText = string.Empty;
        AreaMinText = string.Empty;
        AreaMaxText = string.Empty;

        foreach (var (label, roomType) in HomeFilterOptions.RoomTypes)
        {
            RoomTypeChips.Add(new FilterChip(
                label,
                roomType,
                chip => SelectRoomTypeChip(chip.Value as RoomType?)));
        }

        CampusChips.Add(new FilterChip("Tất cả", null, _ => SelectCampusChip(null)));
        foreach (var campus in HomeFilterOptions.Campuses)
        {
            CampusChips.Add(new FilterChip(
                campus.Name,
                campus,
                chip => SelectCampusChip(chip.Value as CampusOption)));
        }

        foreach (var kilometres in HomeFilterOptions.DistanceKilometres)
        {
            DistanceChips.Add(new FilterChip(
                $"{kilometres} km",
                kilometres,
                chip => SelectDistanceChip((int)chip.Value!)));
        }

        SelectRoomTypeChip(null);
        SelectCampusChip(null);
        SelectDistanceChip(HomeFilterOptions.DefaultDistanceKm);
        UpdateCampusSuggestions();

        PriceChips.Add(new FilterChip("Đến 2 triệu", ("", "2"), SelectPriceChip));
        PriceChips.Add(new FilterChip("2–3 triệu", ("2", "3"), SelectPriceChip));
        PriceChips.Add(new FilterChip("3–5 triệu", ("3", "5"), SelectPriceChip));
        PriceChips.Add(new FilterChip("Từ 5 triệu", ("5", ""), SelectPriceChip));
    }

    public ObservableCollection<HomeListingItem> Listings { get; } = new();

    public ObservableCollection<FilterSummaryChip> ActiveFilterChips { get; } = new();

    public ObservableCollection<FilterChip> RoomTypeChips { get; } = new();

    public ObservableCollection<FilterChip> CampusChips { get; } = new();

    public ObservableCollection<FilterChip> DistanceChips { get; } = new();

    public ObservableCollection<FilterChip> PriceChips { get; } = new();

    public ObservableCollection<CampusOption> CampusSuggestions { get; } = new();

    public bool HasLoaded => _hasLoaded;

    public int ActiveFilterCount => ActiveFilterChips.Count;

    public string FilterButtonText => ActiveFilterCount > 0 ? $"Bộ lọc ({ActiveFilterCount})" : "Bộ lọc";

    public string SearchDisplayText => _appliedFilters?.CampusLat is { } lat && _appliedFilters.CampusLng is { } lng
        ? HomeFilterOptions.Campuses.FirstOrDefault(campus => campus.Lat == lat && campus.Lng == lng)?.Name ?? "Tìm phòng gần trường"
        : "Bạn muốn ở gần trường nào?";

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);

    public bool HasNoSuggestions => CampusSuggestions.Count == 0;

    public bool HasListings => Listings.Count > 0;

    public bool ShowLoadingState => IsBusy && Listings.Count == 0;

    public bool ShowEmptyState => !IsBusy && !HasError && Listings.Count == 0;

    public bool CanApplyFilters => !IsBusy;

    public bool HasActiveFilters => ActiveFilterChips.Count > 0;

    public bool IsDistanceEnabled => _draftCampus is not null;

    public bool HasFilterError => !string.IsNullOrEmpty(FilterError);

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public int ComparisonCount => _comparisonStore.Rooms.Count;

    public bool HasComparison => ComparisonCount > 0;

    public bool ShowComparisonBar => HasComparison && !IsSearchOpen;

    public string ComparisonBarText => $"So sánh ({ComparisonCount})";

    public bool HasCompareHint => !string.IsNullOrEmpty(CompareHint);

    public bool CanLoadMore => HasMore
        && _nextCursor is not null
        && !IsBusy
        && !IsLoadingMore
        && !HasError
        && Listings.Count > 0;

    partial void OnIsLoadingMoreChanged(bool value) => OnPropertyChanged(nameof(CanLoadMore));

    partial void OnIsSearchOpenChanged(bool value) => OnPropertyChanged(nameof(ShowComparisonBar));

    partial void OnCompareHintChanged(string value) => OnPropertyChanged(nameof(HasCompareHint));

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(ShowLoadingState));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(CanApplyFilters));
        ApplyFiltersCommand.NotifyCanExecuteChanged();
        ClearFiltersCommand.NotifyCanExecuteChanged();
        foreach (var chip in ActiveFilterChips)
            (chip.RemoveCommand as IAsyncRelayCommand)?.NotifyCanExecuteChanged();
    }


    partial void OnHasMoreChanged(bool value) => OnPropertyChanged(nameof(CanLoadMore));

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnFilterErrorChanged(string value) => OnPropertyChanged(nameof(HasFilterError));

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchQuery));
        UpdateCampusSuggestions();
    }

    partial void OnPriceMinTextChanged(string value) => UpdatePriceChipSelection();

    partial void OnPriceMaxTextChanged(string value) => UpdatePriceChipSelection();

    [RelayCommand]
    private void OpenSearch()
    {
        SearchQuery = string.Empty;
        IsSearchOpen = true;
    }

    [RelayCommand]
    private void CloseSearch() => IsSearchOpen = false;

    [RelayCommand]
    private void ClearSearchQuery() => SearchQuery = string.Empty;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SelectSearchCampusAsync(CampusOption? campus, CancellationToken cancellationToken)
    {
        if (campus is null || IsBusy)
            return;

        SyncDraftWithAppliedFilters();
        SelectCampusChip(campus);
        IsSearchOpen = false;
        await ApplyFiltersAsync(cancellationToken);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SubmitSearchAsync(CancellationToken cancellationToken)
    {
        // Submit only an unambiguous known campus; no fabricated backend suggestions.
        if (CampusSuggestions.Count == 1)
            await SelectSearchCampusAsync(CampusSuggestions[0], cancellationToken);
    }

    private void UpdateCampusSuggestions()
    {
        var query = SearchQuery.Trim();
        CampusSuggestions.Clear();
        foreach (var campus in HomeFilterOptions.Campuses)
        {
            if (query.Length == 0 || Vietnamese.CompareInfo.IndexOf(campus.Name, query,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
                CampusSuggestions.Add(campus);
        }
        OnPropertyChanged(nameof(HasNoSuggestions));
    }

    private void SelectPriceChip(FilterChip chip)
    {
        if (chip.Value is not ValueTuple<string, string> range)
            return;
        PriceMinText = range.Item1;
        PriceMaxText = range.Item2;
    }

    private void UpdatePriceChipSelection()
    {
        foreach (var chip in PriceChips)
            chip.IsSelected = chip.Value is ValueTuple<string, string> range
                && PriceMinText == range.Item1 && PriceMaxText == range.Item2;
    }

    [RelayCommand]
    private void ResetDraft()
    {
        ResetFilterDraft();
        FilterError = string.Empty;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        await LoadFirstPageAsync(cancellationToken);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        // SfPullToRefresh raises its own spinning state on pull without touching
        // the bound property, so toggle it here to always emit a change that ends it.
        IsRefreshing = true;
        try
        {
            if (IsBusy)
                return;

            await LoadFirstPageAsync(cancellationToken);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RetryAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || IsLoadingMore)
            return;

        await LoadFirstPageAsync(cancellationToken);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadMoreAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || IsLoadingMore || !CanLoadMore)
            return;

        var generation = _generation;
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _searchCancellation?.Token ?? CancellationToken.None);
        var requestToken = linkedCancellation.Token;
        IsLoadingMore = true;
        try
        {
            var page = await _apiClient.GetListingsAsync(
                PageSize,
                _nextCursor,
                _appliedFilters,
                requestToken);
            if (generation != _generation)
                return;

            Append(page.Data);
            _nextCursor = page.Pagination.NextCursor;
            HasMore = page.Pagination.HasMore;
            TotalCount = page.TotalCount;
            IsTotalCountCapped = page.TotalCountCapped;
            UpdateResultsText();
        }
        catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (generation != _generation)
                return;

            ErrorMessage = "Không thể tải thêm phòng. Vui lòng thử lại.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            if (generation == _generation)
                IsLoadingMore = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task OpenListingAsync(HomeListingItem? listing)
    {
        if (listing is null)
            return;

        try
        {
            await _navigationService.GoToRoomDetailAsync(listing.Id);
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task OpenSelectedListingAsync()
    {
        if (SelectedListing is not { } listing)
            return;

        try
        {
            await _navigationService.GoToRoomDetailAsync(listing.Id);
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
        finally
        {
            // Clearing the selection lets the same room be tapped again.
            SelectedListing = null;
        }
    }

    [RelayCommand]
    private void ToggleCompare(HomeListingItem? listing)
    {
        if (listing is null)
        {
            return;
        }

        if (_comparisonStore.Contains(listing.Id))
        {
            _comparisonStore.Remove(listing.Id);
        }
        else
        {
            var candidate = new ComparisonCandidate(
                listing.Id,
                listing.Title,
                listing.CoverPhotoUrl,
                listing.Price,
                listing.AreaM2);
            if (_comparisonStore.TryAdd(candidate) == RoomComparisonAddResult.LimitReached)
            {
                ShowCompareHint($"Chỉ so sánh được tối đa {_comparisonStore.MaxRooms} phòng.");
                return;
            }
        }

        listing.IsCompared = _comparisonStore.Contains(listing.Id);
        NotifyComparisonState();
    }

    [RelayCommand]
    private async Task OpenComparisonAsync()
    {
        try
        {
            await _navigationService.GoToComparisonAsync();
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task OpenAuthAsync()
    {
        try
        {
            if (_authService is null || !_authService.IsSignedIn)
            {
                await _navigationService.GoToLoginAsync();
                return;
            }

            await _navigationService.GoToProfileAsync();
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    public void AttachAuthSession()
    {
        if (_authService is null)
        {
            return;
        }

        if (!_isAuthSubscribed)
        {
            _authService.SessionChanged += OnAuthSessionChanged;
            _isAuthSubscribed = true;
        }

        SyncAuthState();
    }

    public void DetachAuthSession()
    {
        if (_authService is null || !_isAuthSubscribed)
        {
            return;
        }

        _authService.SessionChanged -= OnAuthSessionChanged;
        _isAuthSubscribed = false;
    }

    private void OnAuthSessionChanged(object? sender, EventArgs e)
    {
        if (_uiContext is not null && SynchronizationContext.Current != _uiContext)
        {
            _uiContext.Post(_ => SyncAuthState(), null);
            return;
        }

        SyncAuthState();
    }

    public void SyncAuthState()
    {
        if (_authService is null || !_authService.IsSignedIn)
        {
            IsSignedIn = false;
            SignedInBadgeText = string.Empty;
            return;
        }

        IsSignedIn = true;
        SignedInBadgeText = string.IsNullOrWhiteSpace(_authService.CurrentDisplayName)
            ? FormatRoleLabel(_authService.CurrentRole)
            : _authService.CurrentDisplayName;
    }

    private static string FormatRoleLabel(AppUserRole? role) => role switch
    {
        AppUserRole.Landlord => "Chủ phòng",
        AppUserRole.Moderator => "Kiểm duyệt viên",
        AppUserRole.Admin => "Quản trị viên",
        _ => "Sinh viên"
    };

    public void RefreshComparisonState()
    {
        foreach (var listing in Listings)
        {
            listing.IsCompared = _comparisonStore.Contains(listing.Id);
        }

        NotifyComparisonState();
    }

    private void NotifyComparisonState()
    {
        OnPropertyChanged(nameof(ComparisonCount));
        OnPropertyChanged(nameof(HasComparison));
        OnPropertyChanged(nameof(ShowComparisonBar));
        OnPropertyChanged(nameof(ComparisonBarText));
    }

    private void ShowCompareHint(string message)
    {
        CompareHint = message;
        _compareHintCancellation?.Cancel();
        _compareHintCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _compareHintCancellation = cancellation;
        _ = ClearCompareHintAsync(cancellation.Token);
    }

    private async Task ClearCompareHintAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(2500, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        CompareHint = string.Empty;
    }

    [RelayCommand]
    private void ToggleFilterPanel()
    {
        IsFilterPanelOpen = !IsFilterPanelOpen;
        FilterError = string.Empty;

        if (IsFilterPanelOpen)
        {
            SyncDraftWithAppliedFilters();
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanApplyFilters))]
    private async Task ApplyFiltersAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;
        if (!TryBuildFilters(out var filters, out var error))
        {
            FilterError = error;
            return;
        }

        FilterError = string.Empty;
        IsFilterPanelOpen = false;

        if (filters == _appliedFilters)
            return;

        _appliedFilters = filters;
        RebuildActiveFilterChips();
        await LoadFirstPageAsync(cancellationToken);
    }

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanApplyFilters))]
    private async Task ClearFiltersAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;
        ResetFilterDraft();
        FilterError = string.Empty;
        IsFilterPanelOpen = false;

        if (_appliedFilters is null && ActiveFilterChips.Count == 0)
            return;

        _appliedFilters = null;
        RebuildActiveFilterChips();
        await LoadFirstPageAsync(cancellationToken);
    }

    private async Task LoadFirstPageAsync(CancellationToken cancellationToken)
    {
        var generation = ++_generation;
        _hasLoaded = true;
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _searchCancellation = requestCancellation;
        var requestToken = requestCancellation.Token;
        IsBusy = true;
        IsLoadingMore = false;
        _nextCursor = null;
        HasMore = false;
        TotalCount = 0;
        IsTotalCountCapped = false;
        Reset(Array.Empty<ListingSummaryDto>());
        ResultsText = "Đang cập nhật số phòng…";
        ErrorMessage = string.Empty;
        EmptyMessage = "Đang tải phòng trọ…";
        try
        {
            var page = await _apiClient.GetListingsAsync(
                PageSize,
                cursor: null,
                _appliedFilters,
                requestToken);
            if (generation != _generation)
                return;

            Reset(page.Data);
            _nextCursor = page.Pagination.NextCursor;
            HasMore = page.Pagination.HasMore;
            TotalCount = page.TotalCount;
            IsTotalCountCapped = page.TotalCountCapped;
            UpdateResultsText();
            EmptyMessage = HasActiveFilters
                ? "Không tìm thấy phòng phù hợp. Hãy thử nới lỏng bộ lọc."
                : "Hiện chưa có phòng nào được đăng.";
        }
        catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
        {
            if (generation == _generation)
            {
                ResultsText = "Đã dừng tải phòng.";
                EmptyMessage = "Đã dừng tải phòng. Hãy tải lại để tiếp tục.";
            }
        }
        catch (Exception ex)
        {
            if (generation != _generation)
                return;

            Reset(Array.Empty<ListingSummaryDto>());
            _nextCursor = null;
            HasMore = false;
            TotalCount = 0;
            IsTotalCountCapped = false;
            ResultsText = "Chưa tải được phòng trọ.";
            EmptyMessage = "Chưa tải được phòng trọ.";
            ErrorMessage = "Không thể tải danh sách phòng. Vui lòng thử lại.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            if (generation == _generation)
            {
                IsBusy = false;
                IsRefreshing = false;
            }
        }
    }

    private bool TryBuildFilters(out ActiveListingFiltersDto? filters, out string error)
    {
        filters = null;

        if (!TryParsePrice(PriceMinText, out var minPrice, out error)
            || !TryParsePrice(PriceMaxText, out var maxPrice, out error)
            || !TryParseArea(AreaMinText, out var minArea, out error)
            || !TryParseArea(AreaMaxText, out var maxArea, out error))
        {
            return false;
        }

        if (minPrice is { } lowestPrice && maxPrice is { } highestPrice && lowestPrice > highestPrice)
        {
            error = "Giá tối thiểu không được lớn hơn giá tối đa.";
            return false;
        }

        if (minArea is { } smallestArea && maxArea is { } largestArea && smallestArea > largestArea)
        {
            error = "Diện tích tối thiểu không được lớn hơn diện tích tối đa.";
            return false;
        }

        double? campusLat = null;
        double? campusLng = null;
        double? maxDistanceKm = null;
        if (_draftCampus is { } campus)
        {
            campusLat = campus.Lat;
            campusLng = campus.Lng;
            maxDistanceKm = _draftDistanceKm;
        }

        var hasFilters = minPrice is not null
            || maxPrice is not null
            || minArea is not null
            || maxArea is not null
            || _draftRoomType is not null
            || _draftCampus is not null;

        if (hasFilters)
        {
            filters = new ActiveListingFiltersDto(
                minPrice,
                maxPrice,
                minArea,
                maxArea,
                null,
                _draftRoomType,
                campusLat,
                campusLng,
                maxDistanceKm);
        }

        return true;
    }

    private void SyncDraftWithAppliedFilters()
    {
        if (_appliedFilters is not { } filters)
        {
            ResetFilterDraft();
            return;
        }

        PriceMinText = filters.MinPrice is { } minPrice ? FormatMillions(minPrice) : string.Empty;
        PriceMaxText = filters.MaxPrice is { } maxPrice ? FormatMillions(maxPrice) : string.Empty;
        AreaMinText = filters.MinAreaM2 is { } minArea ? FormatNumber(minArea) : string.Empty;
        AreaMaxText = filters.MaxAreaM2 is { } maxArea ? FormatNumber(maxArea) : string.Empty;

        SelectRoomTypeChip(filters.RoomType);

        var campus = filters.CampusLat is { } lat && filters.CampusLng is { } lng
            ? HomeFilterOptions.Campuses.FirstOrDefault(option => option.Lat == lat && option.Lng == lng)
            : null;
        SelectCampusChip(campus);
        SelectDistanceChip(filters.MaxDistanceKm is { } kilometres
            ? (int)kilometres
            : HomeFilterOptions.DefaultDistanceKm);
    }

    private void ResetFilterDraft()
    {
        PriceMinText = string.Empty;
        PriceMaxText = string.Empty;
        AreaMinText = string.Empty;
        AreaMaxText = string.Empty;
        SelectRoomTypeChip(null);
        SelectCampusChip(null);
        SelectDistanceChip(HomeFilterOptions.DefaultDistanceKm);
    }

    private void RebuildActiveFilterChips()
    {
        ActiveFilterChips.Clear();

        if (_appliedFilters is { } filters)
        {
            if (DescribePrice(filters.MinPrice, filters.MaxPrice) is { } price)
                ActiveFilterChips.Add(CreateSummaryChip(price, "price"));

            if (DescribeArea(filters.MinAreaM2, filters.MaxAreaM2) is { } area)
                ActiveFilterChips.Add(CreateSummaryChip(area, "area"));

            if (filters.RoomType is { } roomType)
            {
                var label = HomeFilterOptions.RoomTypes
                    .Where(option => option.Value == roomType)
                    .Select(option => option.Label)
                    .FirstOrDefault() ?? RoomTypeWire.ToWireValue(roomType);
                ActiveFilterChips.Add(CreateSummaryChip(label, "type"));
            }

            if (filters.CampusLat is { } lat
                && filters.CampusLng is { } lng
                && filters.MaxDistanceKm is { } maxDistanceKm)
            {
                var campus = HomeFilterOptions.Campuses
                    .FirstOrDefault(option => option.Lat == lat && option.Lng == lng);
                var name = campus?.Name ?? "Trường";
                ActiveFilterChips.Add(CreateSummaryChip($"{name} · {FormatNumber(maxDistanceKm)} km", "campus"));
            }
        }

        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(ActiveFilterCount));
        OnPropertyChanged(nameof(FilterButtonText));
        OnPropertyChanged(nameof(SearchDisplayText));
    }

    private FilterSummaryChip CreateSummaryChip(string label, string group)
        => new(label)
        {
            RemoveCommand = new AsyncRelayCommand(() => RemoveFilterGroupAsync(group), () => !IsBusy)
        };

    private async Task RemoveFilterGroupAsync(string group)
    {
        if (_appliedFilters is not { } filters || IsBusy)
            return;

        _appliedFilters = group switch
        {
            "price" => filters with { MinPrice = null, MaxPrice = null },
            "area" => filters with { MinAreaM2 = null, MaxAreaM2 = null },
            "type" => filters with { RoomType = null },
            "campus" => filters with { CampusLat = null, CampusLng = null, MaxDistanceKm = null },
            _ => filters
        };
        if (_appliedFilters == new ActiveListingFiltersDto(null, null, null, null, null, null, null, null, null))
            _appliedFilters = null;
        SyncDraftWithAppliedFilters();
        RebuildActiveFilterChips();
        await LoadFirstPageAsync(CancellationToken.None);
    }

    private void SelectRoomTypeChip(RoomType? roomType)
    {
        _draftRoomType = roomType;
        foreach (var chip in RoomTypeChips)
            chip.IsSelected = Equals(chip.Value, roomType);
    }

    private void SelectCampusChip(CampusOption? campus)
    {
        _draftCampus = campus;
        foreach (var chip in CampusChips)
            chip.IsSelected = Equals(chip.Value, campus);

        OnPropertyChanged(nameof(IsDistanceEnabled));
    }

    private void SelectDistanceChip(int kilometres)
    {
        _draftDistanceKm = kilometres;
        foreach (var chip in DistanceChips)
            chip.IsSelected = Equals(chip.Value, kilometres);
    }

    private void UpdateResultsText()
    {
        if (TotalCount == 0)
        {
            ResultsText = HasActiveFilters ? "0 phòng phù hợp." : "Chưa có phòng nào.";
            return;
        }

        ResultsText = IsTotalCountCapped
            ? $"{SearchLimits.TotalCountLimit.ToString("N0", Vietnamese)}+ phòng phù hợp"
            : $"{TotalCount.ToString("N0", Vietnamese)} phòng phù hợp";
    }

    private static bool TryParsePrice(string? text, out long? value, out string error)
    {
        value = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        if (!TryParseNumber(text, out var millions) || millions < 0)
        {
            error = "Giá thuê phải là số không âm (triệu đồng).";
            return false;
        }

        var wholeVnd = millions * 1_000_000d;
        // long.MaxValue rounds to 2^63 as a double; reject that exclusive bound
        // before converting so a large valid-looking input cannot wrap negative.
        if (!double.IsFinite(wholeVnd) || wholeVnd >= 9223372036854775808d)
        {
            error = "Giá thuê quá lớn.";
            return false;
        }

        value = (long)Math.Round(wholeVnd, MidpointRounding.AwayFromZero);
        return true;
    }

    private static bool TryParseArea(string? text, out double? value, out string error)
    {
        value = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        if (!TryParseNumber(text, out var area) || area < 0)
        {
            error = "Diện tích phải là số không âm (m²).";
            return false;
        }

        value = area;
        return true;
    }

    private static bool TryParseNumber(string text, out double value)
    {
        var normalized = text.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);
    }

    private static string? DescribePrice(long? min, long? max)
    {
        if (min is null && max is null)
            return null;

        if (min is { } lowest && max is { } highest)
            return $"Giá {FormatMillions(lowest)} – {FormatMillions(highest)} triệu";

        if (min is { } onlyMin)
            return $"Giá từ {FormatMillions(onlyMin)} triệu";

        return max is { } onlyMax ? $"Giá tối đa {FormatMillions(onlyMax)} triệu" : null;
    }

    private static string? DescribeArea(double? min, double? max)
    {
        if (min is null && max is null)
            return null;

        if (min is { } smallest && max is { } largest)
            return $"DT {FormatNumber(smallest)} – {FormatNumber(largest)} m²";

        if (min is { } onlyMin)
            return $"DT từ {FormatNumber(onlyMin)} m²";

        return max is { } onlyMax ? $"DT tối đa {FormatNumber(onlyMax)} m²" : null;
    }

    private static string FormatMillions(long vnd) => FormatNumber(vnd / 1_000_000d);

    private static string FormatNumber(double value) => value.ToString("0.##", Vietnamese);

    private void Reset(IReadOnlyList<ListingSummaryDto> listings)
    {
        _listingIds.Clear();
        Listings.Clear();
        Append(listings);
    }

    private void Append(IEnumerable<ListingSummaryDto> listings)
    {
        foreach (var listing in listings)
        {
            if (_listingIds.Add(listing.Id))
            {
                var item = HomeListingItem.FromDto(listing);
                item.IsCompared = _comparisonStore.Contains(item.Id);
                Listings.Add(item);
            }
        }
        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowLoadingState));
        OnPropertyChanged(nameof(HasListings));
    }
}
