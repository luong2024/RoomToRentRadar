using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
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
    private CancellationTokenSource? _paginationCts;
    private bool _isInitialized;
    private bool _suppressAutoSortReload;

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
            .Where(o => o.Option is RoomSortOption.Newest or RoomSortOption.DistanceAsc)
            .ToArray();
        PriceSortOptions = SortOptions
            .Where(o => o.Option is RoomSortOption.PriceAsc or RoomSortOption.PriceDesc)
            .ToArray();
        SelectedSortOptionItem = SortOptions[0];
    }

    public ObservableCollection<RoomListItemModel> Rooms { get; } = [];

    public IReadOnlyList<RoomSortOptionItem> SortOptions { get; }

    public IReadOnlyList<RoomSortOptionItem> PrimarySortOptions { get; }

    public IReadOnlyList<RoomSortOptionItem> PriceSortOptions { get; }

    public int PageSize { get; init; } = DefaultPageSize;

    [ObservableProperty]
    public partial string Title { get; set; } = "Khám phá phòng trọ";

    [ObservableProperty]
    public partial RoomSortOptionItem SelectedSortOptionItem { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

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
    public partial string EmptyStateMessage { get; set; } = "Chưa có phòng trọ nào phù hợp.";

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPriceMenuOpen { get; set; }

    public bool IsPriceSortSelected =>
        SelectedSortOptionItem.Option is RoomSortOption.PriceAsc or RoomSortOption.PriceDesc;

    public string PriceSortLabel =>
        IsPriceSortSelected ? SelectedSortOptionItem.DisplayName : "Giá";

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);

    public string ResultSummaryText =>
        HasSearchQuery
            ? $"Tìm thấy {TotalCount} phòng cho \"{SearchQuery.Trim()}\""
            : TotalCount > 0
                ? $"Tìm thấy {TotalCount} phòng quanh ICTU"
                : "Đang cập nhật danh sách phòng quanh ICTU";

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchQuery));
        OnPropertyChanged(nameof(ResultSummaryText));
    }

    partial void OnTotalCountChanged(int value)
    {
        OnPropertyChanged(nameof(ResultSummaryText));
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

            var sortOption = SelectedSortOptionItem?.Option ?? RoomSortOption.Newest;
            var response = await _roomListingsService.GetRoomListingsAsync(
                nextPage,
                PageSize,
                sortOption,
                GetActiveSearchTerm(),
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

        var targetRoute = $"{AppRoutes.RoomDetails}?id={Uri.EscapeDataString(room.Id)}";

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
            if (isPullToRefresh)
            {
                IsRefreshing = true;
                IsLoading = false;
            }
            else
            {
                IsLoading = true;
                IsRefreshing = false;
            }

            IsLoadingMore = false;

            var sortOption = SelectedSortOptionItem?.Option ?? RoomSortOption.Newest;

            // Search is executed server-side so results cover the whole catalog and stay paginated.
            var response = await _roomListingsService.GetRoomListingsAsync(
                page: 1,
                pageSize: PageSize,
                sortBy: sortOption,
                search: GetActiveSearchTerm(),
                cancellationToken: token);

            if (token.IsCancellationRequested || !ReferenceEquals(_paginationCts, currentCts))
            {
                return;
            }

            Rooms.Clear();
            var existingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var room in response.Items)
            {
                if (existingIds.Add(room.Id))
                {
                    Rooms.Add(room);
                }
            }

            CurrentPage = 1;
            TotalCount = response.TotalCount;
            HasMoreItems = response.Items.Count >= PageSize && Rooms.Count < response.TotalCount;
            EmptyStateMessage = Rooms.Count == 0
                ? "Không tìm thấy phòng trọ nào phù hợp."
                : string.Empty;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Superseded by a newer request
        }
        catch (Exception ex) when (!token.IsCancellationRequested && ReferenceEquals(_paginationCts, currentCts))
        {
            if (Rooms.Count == 0)
            {
                EmptyStateMessage = "Không thể tải danh sách phòng. Vui lòng thử lại.";
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
            }
        }
    }

    private string? GetActiveSearchTerm() =>
        HasSearchQuery ? SearchQuery.Trim() : null;
}
