using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels
{
    public sealed record SortOptionItem(string Key, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    public partial class RoomListViewModel : ObservableObject
    {
        public const int DefaultPageSize = 20;

        private readonly IListingsService _listingsService;
        private readonly HashSet<string> _loadedIds = new(StringComparer.Ordinal);
        private int _currentPage = 1;
        private bool _hasInitialized;

        private SortOptionItem? _selectedSortOption;
        private string _selectedCampusId = "ictu-main";
        private bool _isLoading;
        private bool _isRefreshing;
        private bool _isLoadingMore;
        private bool _hasReachedEnd;
        private int _totalCount;
        private string _errorMessage = string.Empty;
        private bool _hasError;
        private RoomListingItemModel? _selectedRoom;

        public ObservableCollection<RoomListingItemModel> Listings { get; } = [];

        public IReadOnlyList<SortOptionItem> SortOptions { get; } =
        [
            new("distance", "Gần ICTU nhất"),
            new("price_asc", "Giá thấp đến cao"),
            new("price_desc", "Giá cao đến thấp"),
            new("area_desc", "Diện tích lớn nhất")
        ];

        public SortOptionItem? SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (SetProperty(ref _selectedSortOption, value) && _hasInitialized && value is not null)
                {
                    _ = LoadInitialListingsAsync();
                }
            }
        }

        public string SelectedCampusId
        {
            get => _selectedCampusId;
            set => SetProperty(ref _selectedCampusId, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set => SetProperty(ref _isLoadingMore, value);
        }

        public bool HasReachedEnd
        {
            get => _hasReachedEnd;
            set => SetProperty(ref _hasReachedEnd, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public RoomListingItemModel? SelectedRoom
        {
            get => _selectedRoom;
            set => SetProperty(ref _selectedRoom, value);
        }

        public RoomListViewModel(IListingsService listingsService)
        {
            _listingsService = listingsService;
            _selectedSortOption = SortOptions[0]; // Mặc định sortBy = "distance" (US01 AC1)
        }

        [RelayCommand]
        private async Task AppearingAsync()
        {
            if (!_hasInitialized && !IsLoading)
            {
                await LoadInitialListingsAsync();
            }
        }

        /// <summary>
        /// Nạp trang 1 (dùng khi mở màn hình lần đầu và khi kéo RefreshView làm mới).
        /// </summary>
        [RelayCommand]
        public async Task LoadInitialListingsAsync()
        {
            if (IsLoading)
                return;

            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;
                HasReachedEnd = false;
                _currentPage = 1;

                var sortKey = SelectedSortOption?.Key ?? "distance";
                var result = await _listingsService.GetListingsAsync(
                    SelectedCampusId,
                    pageIndex: 1,
                    pageSize: DefaultPageSize,
                    sortBy: sortKey,
                    CancellationToken.None);

                Listings.Clear();
                _loadedIds.Clear();

                AppendUniqueItems(result.Items);
                TotalCount = result.TotalCount > 0 ? result.TotalCount : Listings.Count;

                if (result.Items.Count < DefaultPageSize || !result.HasNextPage)
                {
                    HasReachedEnd = true;
                }

                _hasInitialized = true;
            }
            catch (OperationCanceledException)
            {
                // Bỏ qua nếu thao tác tải bị hủy hợp lệ
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Không thể tải danh sách phòng trọ. Vui lòng kiểm tra kết nối và thử lại. ({ex.Message})";
            }
            finally
            {
                IsLoading = false;
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// Nạp trang tiếp theo cho Infinite Scroll (chặn gọi trùng trang và tự dừng ở cuối danh sách).
        /// </summary>
        [RelayCommand]
        public async Task LoadMoreListingsAsync()
        {
            // Edge case 1: Khóa IsLoadingMore & chặn khi đang nạp đầu / đã hết trang
            if (IsLoading || IsRefreshing || IsLoadingMore || HasReachedEnd)
                return;

            try
            {
                IsLoadingMore = true;
                HasError = false;
                ErrorMessage = string.Empty;

                var nextPage = _currentPage + 1;
                var sortKey = SelectedSortOption?.Key ?? "distance";

                var result = await _listingsService.GetListingsAsync(
                    SelectedCampusId,
                    pageIndex: nextPage,
                    pageSize: DefaultPageSize,
                    sortBy: sortKey,
                    CancellationToken.None);

                AppendUniqueItems(result.Items);
                _currentPage = nextPage;

                if (result.TotalCount > 0)
                {
                    TotalCount = result.TotalCount;
                }

                // Edge case 2: Số phòng trả về < pageSize hoặc HasNextPage == false -> ngừng gọi API ở cuối
                if (result.Items.Count < DefaultPageSize || !result.HasNextPage)
                {
                    HasReachedEnd = true;
                }
            }
            catch (OperationCanceledException)
            {
                // Giữ nguyên _currentPage để có thể thử tải lại trang này sau
            }
            catch (Exception ex)
            {
                // Edge case 3: Mất mạng giữa chừng -> báo lỗi êm ái, giữ nguyên danh sách đã tải
                HasError = true;
                ErrorMessage = $"Không tải thêm được phòng trọ. Vui lòng kiểm tra kết nối. ({ex.Message})";
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private void AppendUniqueItems(IEnumerable<RoomListingItemModel> items)
        {
            foreach (var item in items)
            {
                if (_loadedIds.Add(item.Id))
                {
                    Listings.Add(item);
                }
            }
        }
    }
}
