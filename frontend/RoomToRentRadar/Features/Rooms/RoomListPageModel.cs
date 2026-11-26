using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Features.Rooms
{
    /// <summary>
    /// US02 / T02.2: verified room listing screen with room cards and infinite scroll.
    /// AC1: default sort is distance to ICTU (DistanceAsc), done by the API.
    /// AC2: cards show title, monthly rent, area, distance to campus and the verification badge (RoomCardView).
    /// AC3: pages of <see cref="PageSize"/> rooms are loaded on demand while scrolling (cached with ETag by
    ///      ApiRoomListingRepository), so the first screen appears fast even with 500 listings.
    /// AC4: only Available + Verified rooms, filtered by the API.
    /// Search (US05) and favourites (US07) live in their own tasks, not on this screen.
    /// </summary>
    public partial class RoomListPageModel : ObservableObject
    {
        public const int PageSize = 10;

        /// <summary>Sort keys accepted by GET /api/rooms (RoomListingController, T02.1).</summary>
        public const string SortDistanceAsc = "DistanceAsc";
        public const string SortPriceAsc = "PriceAsc";
        public const string SortPriceDesc = "PriceDesc";

        private const string LoadErrorMessage =
            "Không tải được danh sách phòng. Hãy kiểm tra kết nối mạng rồi thử lại.";

        private readonly IRoomListingRepository _listingRepository;
        private readonly ILogger<RoomListPageModel> _logger;

        // Serializes first-page loads and "load more" so two pages are never appended at the same time.
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private int _currentPage;

        public RoomListPageModel(IRoomListingRepository listingRepository, ILogger<RoomListPageModel> logger)
        {
            _listingRepository = listingRepository;
            _logger = logger;
        }

        public ObservableCollection<RoomListing> Rooms { get; } = new();

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        [ObservableProperty]
        public partial bool IsRefreshing { get; set; }

        [ObservableProperty]
        public partial bool IsLoadingMore { get; set; }

        [ObservableProperty]
        public partial bool HasMoreItems { get; set; } = true;

        /// <summary>True only when the API answered and there is really no room (not on errors).</summary>
        [ObservableProperty]
        public partial bool IsEmpty { get; set; }

        [ObservableProperty]
        public partial string SelectedSortBy { get; set; } = SortDistanceAsc;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        public partial string? ErrorMessage { get; set; }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>AC1, AC3, AC4: loads the first page with the selected sort (distance by default).</summary>
        [RelayCommand]
        public async Task LoadRoomsAsync()
        {
            if (IsBusy)
            {
                IsRefreshing = false;
                return;
            }

            await _loadLock.WaitAsync();
            try
            {
                IsBusy = true;
                ErrorMessage = null;

                var rooms = await _listingRepository.ListVerifiedRoomsAsync(0, PageSize, SelectedSortBy);

                _currentPage = 0;
                Rooms.Clear();
                foreach (var room in rooms)
                {
                    Rooms.Add(room);
                }

                HasMoreItems = rooms.Count == PageSize;
                IsEmpty = Rooms.Count == 0;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                _logger.LogError(ex, "Could not load the room list.");
                ErrorMessage = UserMessage(ex);
                IsEmpty = false; // an error is not "no rooms": show the error with a retry button instead
                HasMoreItems = false;
            }
            finally
            {
                IsBusy = false;
                IsRefreshing = false;
                _loadLock.Release();
            }
        }

        /// <summary>Pull-to-refresh.</summary>
        [RelayCommand]
        public Task RefreshRoomsAsync() => LoadRoomsAsync();

        /// <summary>AC3: infinite scroll, called by CollectionView when the end of the list is near.</summary>
        [RelayCommand]
        public async Task LoadMoreRoomsAsync()
        {
            if (IsBusy || IsLoadingMore || !HasMoreItems || HasError)
            {
                return;
            }

            await _loadLock.WaitAsync();
            try
            {
                IsLoadingMore = true;
                var nextPage = _currentPage + 1;
                var rooms = await _listingRepository.ListVerifiedRoomsAsync(nextPage, PageSize, SelectedSortBy);

                _currentPage = nextPage;
                foreach (var room in rooms)
                {
                    // A room can move between pages if prices/statuses change while scrolling.
                    if (!Rooms.Any(existing => existing.Id == room.Id))
                    {
                        Rooms.Add(room);
                    }
                }

                HasMoreItems = rooms.Count == PageSize;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                // Keep the rooms already shown; the user can pull to refresh.
                _logger.LogWarning(ex, "Could not load more rooms.");
                HasMoreItems = false;
            }
            finally
            {
                IsLoadingMore = false;
                _loadLock.Release();
            }
        }

        /// <summary>AC1: switches between distance (default) and price sorting, then reloads from the first page.</summary>
        [RelayCommand]
        public async Task ChangeSortByAsync(string sortBy)
        {
            if (sortBy is not (SortDistanceAsc or SortPriceAsc or SortPriceDesc) || SelectedSortBy == sortBy)
            {
                return;
            }

            SelectedSortBy = sortBy;
            await LoadRoomsAsync();
        }

        [RelayCommand]
        public Task NavigateToDetailAsync(RoomListing? room) =>
            room is null ? Task.CompletedTask : NavigateAsync($"{Constants.Routes.RoomDetail}?roomId={room.Id}");

        [RelayCommand]
        public Task NavigateToFilterAsync() => NavigateAsync(Constants.Routes.RoomFilter);

        [RelayCommand]
        public Task NavigateToMapAsync() => NavigateAsync(Constants.Routes.RoomMap);

        private async Task NavigateAsync(string route)
        {
            try
            {
                await Shell.Current.GoToAsync(route);
            }
            catch (Exception ex)
            {
                // Detail/filter/map screens belong to other tasks; until they are registered the route is unknown.
                _logger.LogWarning(ex, "Navigation to {Route} failed.", route);
                ErrorMessage = "Màn hình này đang được phát triển.";
            }
        }

        private static string UserMessage(Exception ex) =>
            ex is HttpRequestException { StatusCode: not null } http && !string.IsNullOrWhiteSpace(http.Message)
                ? http.Message // Vietnamese message from the API (ErrorResponse)
                : LoadErrorMessage;
    }
}
