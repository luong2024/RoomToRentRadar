using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RoomToRentRadar.PageModels;

public sealed class ApiSearchActiveFiltersDto
{
    public string CampusId { get; set; } = "ictu-main";
    public decimal? MinRent { get; set; }
    public decimal? MaxRent { get; set; }
    public double? MinArea { get; set; }
    public double? MaxArea { get; set; }
    public string? RoomType { get; set; }
    public double? MaxDistanceMeters { get; set; }
    public string SortBy { get; set; } = "distance";
}

public sealed class ApiSearchListingCardDto
{
    public string Id { get; set; } = string.Empty;
    public string CampusId { get; set; } = "ictu-main";
    public string Title { get; set; } = string.Empty;
    public decimal Rent { get; set; }
    public double Area { get; set; }
    public double DistanceMeters { get; set; }
    public string RoomType { get; set; } = "Studio";
    public string Address { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "Available";
    public bool IsVerified { get; set; }
}

public sealed class ApiSearchResponseDto
{
    public int TotalCount { get; set; }
    public ApiSearchActiveFiltersDto ActiveFilters { get; set; } = new();
    public List<ApiSearchListingCardDto> Items { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool HasNextPage { get; set; }
}

public class ActiveFilterChipModel : ObservableObject
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DisplayText => $"{Label}  ✕";
}

public class RoomTypeOptionModel : ObservableObject
{
    private bool _isSelected;

    public string Value { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string DisplayName => $"{Icon} {Value}";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(ChipBackgroundHex));
                OnPropertyChanged(nameof(ChipTextColorHex));
            }
        }
    }

    public string ChipBackgroundHex => IsSelected ? "#512BD4" : "#F3F4F6";
    public string ChipTextColorHex => IsSelected ? "#FFFFFF" : "#374151";
}

public class FilteredRoomCardModel : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Rent { get; set; }
    public double Area { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public int DistanceMeters { get; set; }
    public string Address { get; set; } = string.Empty;
    public bool IsVerified { get; set; } = true;
    public double SecurityScore { get; set; } = 4.7;
    public string ImageUrl { get; set; } = string.Empty;
    public string PhotoBadgeText { get; set; } = "📷 5 ảnh • 360°";
    public string FallbackColorHex { get; set; } = "#4F46E5";

    public string FormattedRent => string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} đ/tháng", Rent);
    public string FormattedSpecs => $"📐 {Area:0.#} m²  •  🛵 Cách ICTU {DistanceMeters}m";
    public string FormattedSecurity => $"★ {SecurityScore:0.0}";
}

public class SearchFilterViewModel : ObservableObject
{
    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2.0)
    };

    private const decimal DefaultMinRent = 500_000m;
    private const decimal DefaultMaxRent = 5_000_000m;
    private const double DefaultMinArea = 10.0;
    private const double DefaultMaxArea = 50.0;
    private const int DefaultMaxDistanceMeters = 3000;
    private const string AllRoomTypesValue = "Tất cả";

    private readonly List<FilteredRoomCardModel> _allCatalogRooms = [];
    private CancellationTokenSource? _apiSearchCts;

    private string _minRentText = string.Empty;
    private string _maxRentText = string.Empty;
    private double _minArea = DefaultMinArea;
    private double _maxArea = DefaultMaxArea;
    private double _maxDistanceMeters = DefaultMaxDistanceMeters;
    private string _selectedRoomType = AllRoomTypesValue;
    private bool _onlyVerified = true;

    private static string BaseApiUrl =>
        DeviceInfo.Current.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5000"
            : "http://localhost:5000";

    public string MinRentText
    {
        get => _minRentText;
        set
        {
            if (SetProperty(ref _minRentText, value))
            {
                ApplyFiltersInternal();
            }
        }
    }

    public string MaxRentText
    {
        get => _maxRentText;
        set
        {
            if (SetProperty(ref _maxRentText, value))
            {
                ApplyFiltersInternal();
            }
        }
    }

    public double MinArea
    {
        get => _minArea;
        set
        {
            var clamped = Math.Round(Math.Clamp(value, DefaultMinArea, DefaultMaxArea), 1);
            if (SetProperty(ref _minArea, clamped))
            {
                if (_maxArea < _minArea)
                {
                    _maxArea = _minArea;
                    OnPropertyChanged(nameof(MaxArea));
                }
                OnPropertyChanged(nameof(FormattedAreaRange));
                ApplyFiltersInternal();
            }
        }
    }

    public double MaxArea
    {
        get => _maxArea;
        set
        {
            var clamped = Math.Round(Math.Clamp(value, DefaultMinArea, DefaultMaxArea), 1);
            if (SetProperty(ref _maxArea, clamped))
            {
                if (_minArea > _maxArea)
                {
                    _minArea = _maxArea;
                    OnPropertyChanged(nameof(MinArea));
                }
                OnPropertyChanged(nameof(FormattedAreaRange));
                ApplyFiltersInternal();
            }
        }
    }

    public double MaxDistanceMeters
    {
        get => _maxDistanceMeters;
        set
        {
            var rounded = Math.Round(value / 50.0) * 50.0;
            if (SetProperty(ref _maxDistanceMeters, Math.Clamp(rounded, 100, DefaultMaxDistanceMeters)))
            {
                OnPropertyChanged(nameof(FormattedMaxDistance));
                ApplyFiltersInternal();
            }
        }
    }

    public string SelectedRoomType
    {
        get => _selectedRoomType;
        set
        {
            if (SetProperty(ref _selectedRoomType, value))
            {
                foreach (var opt in RoomTypeOptions)
                {
                    opt.IsSelected = string.Equals(opt.Value, value, StringComparison.OrdinalIgnoreCase);
                }
                ApplyFiltersInternal();
            }
        }
    }

    public bool OnlyVerified
    {
        get => _onlyVerified;
        set
        {
            if (SetProperty(ref _onlyVerified, value))
            {
                ApplyFiltersInternal();
            }
        }
    }

    public string FormattedMaxDistance => MaxDistanceMeters < 1000
        ? $"≤ {MaxDistanceMeters:0} m từ cổng ICTU"
        : $"≤ {MaxDistanceMeters / 1000.0:0.1} km từ cổng ICTU";

    public string FormattedAreaRange => $"{MinArea:0.#} m² – {MaxArea:0.#} m²";

    public int TotalResultCount => FilteredRooms.Count;

    public string ResultCountText => $"Tìm thấy {FilteredRooms.Count} phòng phù hợp";

    public bool HasActiveFilters => ActiveFilterChips.Count > 0;

    public bool HasNoResults => FilteredRooms.Count == 0;

    public ObservableCollection<ActiveFilterChipModel> ActiveFilterChips { get; } = [];

    public ObservableCollection<RoomTypeOptionModel> RoomTypeOptions { get; } = [];

    public ObservableCollection<FilteredRoomCardModel> FilteredRooms { get; } = [];

    public IRelayCommand ClearFiltersCommand { get; }

    public IRelayCommand ApplyFiltersCommand { get; }

    public IRelayCommand<RoomTypeOptionModel> SelectRoomTypeCommand { get; }

    public IRelayCommand<ActiveFilterChipModel> RemoveFilterChipCommand { get; }

    public IRelayCommand<string> ApplyPricePresetCommand { get; }

    public IRelayCommand<string> ApplyAreaPresetCommand { get; }

    public SearchFilterViewModel()
    {
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        ApplyFiltersCommand = new RelayCommand(ApplyFiltersInternal);
        SelectRoomTypeCommand = new RelayCommand<RoomTypeOptionModel>(SelectRoomType);
        RemoveFilterChipCommand = new RelayCommand<ActiveFilterChipModel>(RemoveFilterChip);
        ApplyPricePresetCommand = new RelayCommand<string>(ApplyPricePreset);
        ApplyAreaPresetCommand = new RelayCommand<string>(ApplyAreaPreset);

        InitializeRoomTypes();
        SeedCatalogRooms();
        ApplyFiltersInternal();
    }

    private void InitializeRoomTypes()
    {
        RoomTypeOptions.Clear();
        RoomTypeOptions.Add(new RoomTypeOptionModel { Value = AllRoomTypesValue, Icon = "🏘️", IsSelected = true });
        RoomTypeOptions.Add(new RoomTypeOptionModel { Value = "Studio", Icon = "✨", IsSelected = false });
        RoomTypeOptions.Add(new RoomTypeOptionModel { Value = "Phòng khép kín", Icon = "🚪", IsSelected = false });
        RoomTypeOptions.Add(new RoomTypeOptionModel { Value = "Gác xép", Icon = "🪜", IsSelected = false });
        RoomTypeOptions.Add(new RoomTypeOptionModel { Value = "Chung cư mini", Icon = "🏢", IsSelected = false });
    }

    private void SeedCatalogRooms()
    {
        _allCatalogRooms.Clear();
        _allCatalogRooms.AddRange([
            new FilteredRoomCardModel
            {
                Id = "room-2026-001",
                Title = "Studio Ban Công Thoáng Cách Cổng Chính ICTU 400m",
                Rent = 1_800_000m,
                Area = 22.5,
                RoomType = "Studio",
                DistanceMeters = 400,
                Address = "Ngõ 18 Đường Z115, Tân Thịnh, TP. Thái Nguyên",
                IsVerified = true,
                SecurityScore = 4.8,
                ImageUrl = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=700&q=80",
                PhotoBadgeText = "📷 6 ảnh • 🌐 360°",
                FallbackColorHex = "#4F46E5"
            },
            new FilteredRoomCardModel
            {
                Id = "room-2026-002",
                Title = "Studio Full Nội Thất Cổng Phụ ICTU",
                Rent = 1_950_000m,
                Area = 24.0,
                RoomType = "Studio",
                DistanceMeters = 350,
                Address = "Ngõ 42 Đường Z115, Tân Thịnh, TP. Thái Nguyên",
                IsVerified = true,
                SecurityScore = 4.9,
                ImageUrl = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=700&q=80",
                PhotoBadgeText = "📷 8 ảnh • 🌐 360°",
                FallbackColorHex = "#7C3AED"
            },
            new FilteredRoomCardModel
            {
                Id = "room-2026-003",
                Title = "Phòng Trọ Khép Kín Gác Xép Gỗ Thoáng Mát",
                Rent = 1_500_000m,
                Area = 20.0,
                RoomType = "Gác xép",
                DistanceMeters = 550,
                Address = "Đường Quang Trung, Tân Thịnh, TP. Thái Nguyên",
                IsVerified = true,
                SecurityScore = 4.6,
                ImageUrl = "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?auto=format&fit=crop&w=700&q=80",
                PhotoBadgeText = "📷 5 ảnh",
                FallbackColorHex = "#0D9488"
            },
            new FilteredRoomCardModel
            {
                Id = "room-2026-004",
                Title = "Phòng Khép Kín Sinh Viên Giá Rẻ Gần Chợ Z115",
                Rent = 1_200_000m,
                Area = 16.0,
                RoomType = "Phòng khép kín",
                DistanceMeters = 700,
                Address = "Tổ 5 Phường Tân Thịnh, TP. Thái Nguyên",
                IsVerified = true,
                SecurityScore = 4.5,
                ImageUrl = "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=700&q=80",
                PhotoBadgeText = "📷 4 ảnh",
                FallbackColorHex = "#2563EB"
            },
            new FilteredRoomCardModel
            {
                Id = "room-2026-005",
                Title = "Chung Cư Mini Có Thang Máy & Khóa Vân Tay",
                Rent = 2_600_000m,
                Area = 28.0,
                RoomType = "Chung cư mini",
                DistanceMeters = 850,
                Address = "Đường Z115, Quyết Thắng, TP. Thái Nguyên",
                IsVerified = true,
                SecurityScore = 4.9,
                ImageUrl = "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=700&q=80",
                PhotoBadgeText = "📷 10 ảnh • 🌐 360°",
                FallbackColorHex = "#4338CA"
            },
            new FilteredRoomCardModel
            {
                Id = "room-2026-006",
                Title = "Phòng Khép Kín Ban Công Rộng Gần Hồ Điều Hòa",
                Rent = 1_650_000m,
                Area = 21.0,
                RoomType = "Phòng khép kín",
                DistanceMeters = 1200,
                Address = "Tổ 7 Phường Tân Thịnh, TP. Thái Nguyên",
                IsVerified = true,
                SecurityScore = 4.7,
                ImageUrl = "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=700&q=80",
                PhotoBadgeText = "📷 6 ảnh",
                FallbackColorHex = "#059669"
            },
            new FilteredRoomCardModel
            {
                Id = "room-2026-007",
                Title = "Phòng Gác Xép Có Điều Hòa Cách ICTU 1.5km",
                Rent = 1_350_000m,
                Area = 18.5,
                RoomType = "Gác xép",
                DistanceMeters = 1500,
                Address = "Phường Quyết Thắng, TP. Thái Nguyên",
                IsVerified = true,
                SecurityScore = 4.4,
                ImageUrl = "https://images.unsplash.com/photo-1616594039964-ae9021a400a0?auto=format&fit=crop&w=700&q=80",
                PhotoBadgeText = "📷 5 ảnh",
                FallbackColorHex = "#D97706"
            },
            new FilteredRoomCardModel
            {
                Id = "room-2026-008",
                Title = "Chung Cư Mini 2 Giường Cho Nhóm Sinh Viên Ở Ghép",
                Rent = 3_200_000m,
                Area = 35.0,
                RoomType = "Chung cư mini",
                DistanceMeters = 1800,
                Address = "Đường Lương Ngọc Quyến, TP. Thái Nguyên",
                IsVerified = true,
                SecurityScore = 4.8,
                ImageUrl = "https://images.unsplash.com/photo-1595526114035-0d45ed16cfbf?auto=format&fit=crop&w=700&q=80",
                PhotoBadgeText = "📷 9 ảnh • 🌐 360°",
                FallbackColorHex = "#9333EA"
            }
        ]);
    }

    private void SelectRoomType(RoomTypeOptionModel? option)
    {
        if (option is null)
            return;

        SelectedRoomType = option.Value;
    }

    private void ApplyPricePreset(string? presetKey)
    {
        switch (presetKey)
        {
            case "under15":
                _minRentText = "500000";
                _maxRentText = "1500000";
                break;
            case "15to20":
                _minRentText = "1500000";
                _maxRentText = "2000000";
                break;
            case "20to30":
                _minRentText = "2000000";
                _maxRentText = "3000000";
                break;
            default:
                _minRentText = string.Empty;
                _maxRentText = string.Empty;
                break;
        }

        OnPropertyChanged(nameof(MinRentText));
        OnPropertyChanged(nameof(MaxRentText));
        ApplyFiltersInternal();
    }

    private void ApplyAreaPreset(string? presetKey)
    {
        switch (presetKey)
        {
            case "compact":
                _minArea = 15;
                _maxArea = 20;
                break;
            case "standard":
                _minArea = 20;
                _maxArea = 28;
                break;
            case "spacious":
                _minArea = 25;
                _maxArea = 50;
                break;
            default:
                _minArea = DefaultMinArea;
                _maxArea = DefaultMaxArea;
                break;
        }

        OnPropertyChanged(nameof(MinArea));
        OnPropertyChanged(nameof(MaxArea));
        OnPropertyChanged(nameof(FormattedAreaRange));
        ApplyFiltersInternal();
    }

    private void RemoveFilterChip(ActiveFilterChipModel? chip)
    {
        if (chip is null)
            return;

        switch (chip.Key)
        {
            case "minRent":
                MinRentText = string.Empty;
                break;
            case "maxRent":
                MaxRentText = string.Empty;
                break;
            case "area":
                _minArea = DefaultMinArea;
                _maxArea = DefaultMaxArea;
                OnPropertyChanged(nameof(MinArea));
                OnPropertyChanged(nameof(MaxArea));
                OnPropertyChanged(nameof(FormattedAreaRange));
                ApplyFiltersInternal();
                break;
            case "roomType":
                SelectedRoomType = AllRoomTypesValue;
                break;
            case "distance":
                MaxDistanceMeters = DefaultMaxDistanceMeters;
                break;
        }
    }

    public void ClearFilters()
    {
        _minRentText = string.Empty;
        _maxRentText = string.Empty;
        _minArea = DefaultMinArea;
        _maxArea = DefaultMaxArea;
        _maxDistanceMeters = DefaultMaxDistanceMeters;
        _selectedRoomType = AllRoomTypesValue;
        _onlyVerified = true;

        foreach (var opt in RoomTypeOptions)
        {
            opt.IsSelected = string.Equals(opt.Value, AllRoomTypesValue, StringComparison.OrdinalIgnoreCase);
        }

        OnPropertyChanged(nameof(MinRentText));
        OnPropertyChanged(nameof(MaxRentText));
        OnPropertyChanged(nameof(MinArea));
        OnPropertyChanged(nameof(MaxArea));
        OnPropertyChanged(nameof(FormattedAreaRange));
        OnPropertyChanged(nameof(MaxDistanceMeters));
        OnPropertyChanged(nameof(FormattedMaxDistance));
        OnPropertyChanged(nameof(SelectedRoomType));
        OnPropertyChanged(nameof(OnlyVerified));

        ApplyFiltersInternal();
    }

    private void ApplyFiltersInternal()
    {
        var minRent = ParseDecimalOrDefault(MinRentText, DefaultMinRent);
        var maxRent = ParseDecimalOrDefault(MaxRentText, DefaultMaxRent);

        if (maxRent < minRent)
        {
            maxRent = DefaultMaxRent;
        }

        ActiveFilterChips.Clear();

        if (!string.IsNullOrWhiteSpace(MinRentText) && decimal.TryParse(CleanDigits(MinRentText), out var parsedMinRent))
        {
            ActiveFilterChips.Add(new ActiveFilterChipModel
            {
                Key = "minRent",
                Label = $"Giá từ {string.Format(ViCulture, "{0:N0} đ", parsedMinRent)}"
            });
        }

        if (!string.IsNullOrWhiteSpace(MaxRentText) && decimal.TryParse(CleanDigits(MaxRentText), out var parsedMaxRent))
        {
            ActiveFilterChips.Add(new ActiveFilterChipModel
            {
                Key = "maxRent",
                Label = $"Giá ≤ {string.Format(ViCulture, "{0:N0} đ", parsedMaxRent)}"
            });
        }

        if (MinArea > DefaultMinArea || MaxArea < DefaultMaxArea)
        {
            ActiveFilterChips.Add(new ActiveFilterChipModel
            {
                Key = "area",
                Label = $"Diện tích {MinArea:0.#}–{MaxArea:0.#} m²"
            });
        }

        if (!string.Equals(SelectedRoomType, AllRoomTypesValue, StringComparison.OrdinalIgnoreCase))
        {
            ActiveFilterChips.Add(new ActiveFilterChipModel
            {
                Key = "roomType",
                Label = $"Loại: {SelectedRoomType}"
            });
        }

        if (MaxDistanceMeters < DefaultMaxDistanceMeters)
        {
            ActiveFilterChips.Add(new ActiveFilterChipModel
            {
                Key = "distance",
                Label = $"Cách ICTU ≤ {MaxDistanceMeters:0}m"
            });
        }

        var query = _allCatalogRooms.Where(r =>
            r.Rent >= minRent &&
            r.Rent <= maxRent &&
            r.Area >= MinArea &&
            r.Area <= MaxArea &&
            r.DistanceMeters <= MaxDistanceMeters &&
            (!OnlyVerified || r.IsVerified) &&
            MatchesRoomType(r.RoomType, SelectedRoomType))
            .OrderBy(r => r.DistanceMeters)
            .ToList();

        FilteredRooms.Clear();
        foreach (var room in query)
        {
            FilteredRooms.Add(room);
        }

        OnPropertyChanged(nameof(TotalResultCount));
        OnPropertyChanged(nameof(ResultCountText));
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(HasNoResults));

        _apiSearchCts?.Cancel();
        _apiSearchCts = new CancellationTokenSource();
        _ = FetchSearchResultsFromApiAsync(minRent, maxRent, _apiSearchCts.Token);
    }

    /// <summary>
    /// Calls Backend API T04.1 (GET /api/v1/listings/search) and updates FilteredRooms when the local API is online.
    /// Automatically preserves local filtered results when Backend API is offline.
    /// </summary>
    public async Task FetchSearchResultsFromApiAsync(
        decimal minRent,
        decimal maxRent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParams = new List<string>
            {
                "campusId=ictu-main",
                $"minRent={minRent.ToString(CultureInfo.InvariantCulture)}",
                $"maxRent={maxRent.ToString(CultureInfo.InvariantCulture)}",
                $"minArea={MinArea.ToString(CultureInfo.InvariantCulture)}",
                $"maxArea={MaxArea.ToString(CultureInfo.InvariantCulture)}",
                $"maxDistanceMeters={MaxDistanceMeters.ToString(CultureInfo.InvariantCulture)}",
                "sortBy=distance",
                "pageIndex=1",
                "pageSize=20"
            };

            if (!string.Equals(SelectedRoomType, AllRoomTypesValue, StringComparison.OrdinalIgnoreCase))
            {
                var apiRoomType = string.Equals(SelectedRoomType, "Phòng khép kín", StringComparison.OrdinalIgnoreCase)
                    ? "Khép kín"
                    : SelectedRoomType;
                queryParams.Add($"roomType={Uri.EscapeDataString(apiRoomType)}");
            }

            var url = $"{BaseApiUrl}/api/v1/listings/search?{string.Join("&", queryParams)}";
            var response = await SharedHttpClient.GetFromJsonAsync<ApiSearchResponseDto>(url, JsonOptions, cancellationToken);
            if (response is null || cancellationToken.IsCancellationRequested)
                return;

            var colors = new[] { "#4F46E5", "#7C3AED", "#0D9488", "#2563EB", "#4338CA" };
            FilteredRooms.Clear();
            for (var i = 0; i < response.Items.Count; i++)
            {
                var item = response.Items[i];
                FilteredRooms.Add(new FilteredRoomCardModel
                {
                    Id = item.Id,
                    Title = item.Title,
                    Rent = item.Rent,
                    Area = item.Area,
                    RoomType = item.RoomType,
                    DistanceMeters = (int)Math.Round(item.DistanceMeters),
                    Address = item.Address,
                    IsVerified = item.IsVerified,
                    SecurityScore = 4.8,
                    ImageUrl = ResolveRoomPhotoUrl(item.Id, item.ThumbnailUrl),
                    PhotoBadgeText = "📷 6 ảnh • 🌐 360°",
                    FallbackColorHex = colors[i % colors.Length]
                });
            }

            OnPropertyChanged(nameof(TotalResultCount));
            OnPropertyChanged(nameof(ResultCountText));
            OnPropertyChanged(nameof(HasActiveFilters));
            OnPropertyChanged(nameof(HasNoResults));
        }
        catch (Exception)
        {
            // Keep local filtered catalog when Backend API (http://localhost:5000) is not running
        }
    }

    private static bool MatchesRoomType(string roomType, string selectedFilter)
    {
        if (string.Equals(selectedFilter, AllRoomTypesValue, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(selectedFilter, "Phòng khép kín", StringComparison.OrdinalIgnoreCase)
            && string.Equals(roomType, "Khép kín", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(roomType, selectedFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveRoomPhotoUrl(string roomId, string? rawUrl)
    {
        if (!string.IsNullOrWhiteSpace(rawUrl) && !rawUrl.Contains("cdn.example", StringComparison.OrdinalIgnoreCase))
        {
            return rawUrl;
        }

        return roomId.ToLowerInvariant() switch
        {
            "room-2026-002" or "room-ictu-002" => "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=700&q=80",
            "room-2026-003" or "room-ictu-003" => "https://images.unsplash.com/photo-1598928506311-c55ded91a20c?auto=format&fit=crop&w=700&q=80",
            "room-2026-004" or "room-ictu-004" => "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=700&q=80",
            "room-2026-005" or "room-ictu-005" => "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=700&q=80",
            _ => "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=700&q=80"
        };
    }

    private static string CleanDigits(string input)
    {
        return new string(input.Where(char.IsDigit).ToArray());
    }

    private static decimal ParseDecimalOrDefault(string input, decimal fallback)
    {
        var digits = CleanDigits(input);
        return decimal.TryParse(digits, out var val) && val > 0 ? val : fallback;
    }
}
