using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Abstractions.Auth;
using RoomToRentRadar.Abstractions.Favorites;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Data;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Features.Rooms
{
    /// <summary>
    /// ViewModel for US02 Verified Room Listings Discovery Screen, Room Cards, and Infinite Scroll.
    /// AC1: Default sorted by distance to ICTU campus (DistanceAsc).
    /// AC2: Cards show title, monthly rent, area, distance to campus, and verification badge.
    /// AC3: Pagination / Infinite scroll handling up to 500 listings efficiently.
    /// AC4: Displays strictly listings with Status = "Available" and IsVerified = true.
    /// Strictly adheres to Constructor Injection (DIP) without Service Locator anti-patterns.
    /// </summary>
    public partial class RoomListPageModel : ObservableObject
    {
        private readonly IRoomListingRepository _listingRepository;
        private readonly IFavoriteRepository _favoriteRepository;
        private readonly IAuthService _authService;
        private readonly RentalDatabaseService? _databaseService;
        private readonly ILogger<RoomListPageModel>? _logger;

        public const int PageSize = 10;
        private int _currentPage = 0;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public ObservableCollection<RoomListing> Rooms { get; } = new();

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial bool IsRefreshing { get; set; }

        [ObservableProperty]
        public partial bool IsLoadingMore { get; set; }

        [ObservableProperty]
        public partial bool HasMoreItems { get; set; } = true;

        [ObservableProperty]
        public partial int TotalCount { get; set; }

        [ObservableProperty]
        public partial bool IsEmpty { get; set; }

        [ObservableProperty]
        public partial string SelectedSortBy { get; set; } = "DistanceAsc"; // DistanceAsc, RentAsc, RentDesc, AreaDesc

        [ObservableProperty]
        public partial string SearchKeyword { get; set; } = string.Empty;

        public RoomListPageModel(
            IRoomListingRepository listingRepository,
            IFavoriteRepository favoriteRepository,
            IAuthService authService,
            RentalDatabaseService? databaseService = null,
            ILogger<RoomListPageModel>? logger = null)
        {
            _listingRepository = listingRepository;
            _favoriteRepository = favoriteRepository;
            _authService = authService;
            _databaseService = databaseService;
            _logger = logger;
        }

        partial void OnSearchKeywordChanged(string value)
        {
            _ = LoadRoomsAsync();
        }

        private int GetCurrentUserId()
        {
            return _authService.CurrentUser?.Id ?? 1;
        }

        /// <summary>
        /// US02 AC1, AC3, AC4: Loads initial page of verified, available rooms sorted by distance to ICTU by default.
        /// </summary>
        [RelayCommand]
        public async Task LoadRoomsAsync()
        {
            if (IsBusy) return;

            await _loadLock.WaitAsync();
            try
            {
                IsBusy = true;
                _currentPage = 0;
                HasMoreItems = true;

                var rooms = await FetchRoomsPageAsync(pageIndex: 0, pageSize: PageSize, SelectedSortBy, SearchKeyword);

                Rooms.Clear();
                foreach (var r in rooms)
                {
                    Rooms.Add(r);
                }

                TotalCount = Rooms.Count;
                IsEmpty = Rooms.Count == 0;
                if (rooms.Count < PageSize)
                {
                    HasMoreItems = false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading rooms");
                Rooms.Clear();
                IsEmpty = true;
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
                _loadLock.Release();
            }
        }

        [RelayCommand]
        public async Task RefreshRoomsAsync()
        {
            IsRefreshing = true;
            await LoadRoomsAsync();
        }

        /// <summary>
        /// US02 AC3: Infinite scroll pagination triggered when scrolling near the end of list.
        /// </summary>
        [RelayCommand]
        public async Task LoadMoreRoomsAsync()
        {
            if (IsBusy || IsLoadingMore || !HasMoreItems) return;

            await _loadLock.WaitAsync();
            try
            {
                IsLoadingMore = true;
                int nextPage = _currentPage + 1;

                var nextRooms = await FetchRoomsPageAsync(pageIndex: nextPage, pageSize: PageSize, SelectedSortBy, SearchKeyword);

                if (nextRooms.Count == 0)
                {
                    HasMoreItems = false;
                }
                else
                {
                    _currentPage = nextPage;
                    foreach (var r in nextRooms)
                    {
                        if (!Rooms.Any(existing => existing.Id == r.Id))
                        {
                            Rooms.Add(r);
                        }
                    }

                    TotalCount = Rooms.Count;
                    if (nextRooms.Count < PageSize)
                    {
                        HasMoreItems = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading more rooms");
                HasMoreItems = false;
            }
            finally
            {
                IsLoadingMore = false;
                _loadLock.Release();
            }
        }

        /// <summary>
        /// US02 AC1: Sort options (DistanceAsc is default).
        /// </summary>
        [RelayCommand]
        public async Task ChangeSortByAsync(string sortBy)
        {
            if (SelectedSortBy == sortBy) return;
            SelectedSortBy = sortBy;
            await LoadRoomsAsync();
        }

        /// <summary>
        /// Toggles room favorite status and updates local item state immediately.
        /// </summary>
        [RelayCommand]
        public async Task ToggleFavoriteAsync(RoomListing? room)
        {
            if (room == null) return;

            room.IsFavorite = !room.IsFavorite;
            int userId = GetCurrentUserId();

            try
            {
                await _favoriteRepository.ToggleFavoriteAsync(userId, room.Id);

                string msg = room.IsFavorite
                    ? $"Đã thêm \"{room.Title}\" vào danh sách yêu thích"
                    : $"Đã xóa \"{room.Title}\" khỏi danh sách yêu thích";
                NotifySnackbar(msg);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to toggle favorite for room {RoomId}", room.Id);
            }
        }

        [RelayCommand]
        public async Task NavigateToDetailAsync(RoomListing? room)
        {
            if (room == null) return;
            try
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync($"{Constants.Routes.RoomDetail}?roomId={room.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to navigate to room detail {RoomId}", room.Id);
            }
        }

        [RelayCommand]
        public async Task NavigateToFilterAsync()
        {
            try
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync(Constants.Routes.RoomFilter);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to navigate to room filter");
            }
        }

        [RelayCommand]
        public async Task NavigateToMapAsync()
        {
            try
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync(Constants.Routes.RoomMap);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to navigate to room map");
            }
        }

        private async Task<List<RoomListing>> FetchRoomsPageAsync(int pageIndex, int pageSize, string sortBy, string keyword)
        {
            var result = await _listingRepository.ListVerifiedRoomsAsync(pageIndex, pageSize, sortBy);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLowerInvariant();
                result = result.Where(r =>
                    (r.Title?.ToLowerInvariant().Contains(kw) ?? false) ||
                    (r.Address?.ToLowerInvariant().Contains(kw) ?? false) ||
                    (r.Ward?.ToLowerInvariant().Contains(kw) ?? false) ||
                    (r.RoomType?.ToLowerInvariant().Contains(kw) ?? false)).ToList();
            }

            await SyncFavoriteStatusesAsync(result);
            return result;
        }

        private async Task SyncFavoriteStatusesAsync(List<RoomListing> rooms)
        {
            if (rooms.Count == 0) return;
            int userId = GetCurrentUserId();

            try
            {
                var favs = await _favoriteRepository.GetFavoriteRoomsAsync(userId);
                var favoriteRoomIds = favs.Select(f => f.Id).ToHashSet();

                foreach (var r in rooms)
                {
                    r.IsFavorite = favoriteRoomIds.Contains(r.Id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to sync favorite statuses for user {UserId}", userId);
            }
        }

        private static void NotifySnackbar(string message)
        {
            try
            {
                if (Shell.Current != null)
                {
                    _ = AppShell.DisplaySnackbarAsync(message);
                }
            }
            catch
            {
            }
        }
    }
}
