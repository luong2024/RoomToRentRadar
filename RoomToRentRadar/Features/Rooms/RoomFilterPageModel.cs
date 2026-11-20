using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Abstractions.Rooms;
using RoomToRentRadar.Models;

namespace RoomToRentRadar.Features.Rooms
{
    /// <summary>
    /// PageModel for US05 / Task T05.2: Multi-Filter Modal and Active Filter Count.
    /// Manages search filter criteria, calculates active filter counts, and previews search results.
    /// </summary>
    public partial class RoomFilterPageModel : ObservableObject
    {
        private readonly IRoomListingRepository _roomRepository;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
        public partial string? Keyword { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
        public partial decimal? MinPrice { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
        public partial decimal? MaxPrice { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
        public partial double? MinArea { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
        public partial double? MaxArea { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
        public partial string? SelectedRoomType { get; set; } = "Tất cả";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ActiveFilterCount))]
        public partial double? MaxDistanceKm { get; set; }

        [ObservableProperty]
        public partial string SortBy { get; set; } = "DistanceAsc";

        [ObservableProperty]
        public partial int MatchedRoomCount { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        public ObservableCollection<string> RoomTypes { get; } = new()
        {
            "Tất cả",
            "Phòng khép kín",
            "Chung cư mini",
            "Nhà nguyên căn",
            "Ở ghép"
        };

        public ObservableCollection<string> SortOptions { get; } = new()
        {
            "Gần ICTU nhất",
            "Giá thuê: Thấp đến cao",
            "Giá thuê: Cao đến thấp"
        };

        public int ActiveFilterCount
        {
            get
            {
                int count = 0;
                if (!string.IsNullOrWhiteSpace(Keyword)) count++;
                if (MinPrice.HasValue || MaxPrice.HasValue) count++;
                if (MinArea.HasValue || MaxArea.HasValue) count++;
                if (!string.IsNullOrWhiteSpace(SelectedRoomType) && SelectedRoomType != "Tất cả") count++;
                if (MaxDistanceKm.HasValue) count++;
                return count;
            }
        }

        public RoomFilterPageModel(IRoomListingRepository roomRepository)
        {
            _roomRepository = roomRepository;
        }

        public RoomFilterCriteria BuildCriteria()
        {
            string sortKey = SortBy switch
            {
                "Giá thuê: Thấp đến cao" or "PriceAsc" => "PriceAsc",
                "Giá thuê: Cao đến thấp" or "PriceDesc" => "PriceDesc",
                _ => "DistanceAsc"
            };

            return new RoomFilterCriteria
            {
                Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim(),
                MinPrice = MinPrice,
                MaxPrice = MaxPrice,
                MinArea = MinArea,
                MaxArea = MaxArea,
                RoomType = (string.IsNullOrWhiteSpace(SelectedRoomType) || SelectedRoomType == "Tất cả") ? null : SelectedRoomType,
                MaxDistanceKm = MaxDistanceKm,
                SortBy = sortKey
            };
        }

        [RelayCommand]
        public async Task AppearingAsync()
        {
            await UpdatePreviewCountAsync();
        }

        [RelayCommand]
        public async Task UpdatePreviewCountAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                var criteria = BuildCriteria();
                var results = await _roomRepository.SearchAsync(criteria);
                MatchedRoomCount = results.Count;
            }
            catch
            {
                MatchedRoomCount = 0;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task ClearFiltersAsync()
        {
            Keyword = null;
            MinPrice = null;
            MaxPrice = null;
            MinArea = null;
            MaxArea = null;
            SelectedRoomType = "Tất cả";
            MaxDistanceKm = null;
            SortBy = "DistanceAsc";

            OnPropertyChanged(nameof(ActiveFilterCount));
            await UpdatePreviewCountAsync();
        }

        [RelayCommand]
        public async Task ApplyFiltersAsync()
        {
            await UpdatePreviewCountAsync();
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("..");
            }
        }

        [RelayCommand]
        public async Task CloseModalAsync()
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("..");
            }
        }
    }
}

