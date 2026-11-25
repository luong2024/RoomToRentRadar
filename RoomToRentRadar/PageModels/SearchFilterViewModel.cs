using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels;

public sealed record SearchSortOptionModel(string Key, string DisplayName)
{
    public override string ToString() => DisplayName;
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

    private const double DefaultMinArea = 10.0;
    private const double DefaultMaxArea = 50.0;
    private const int DefaultMaxDistanceMeters = 3000;
    private const string AllRoomTypesValue = "Tất cả";

    private readonly ISearchFilterService _searchFilterService;
    private readonly IAppNavigationService? _navigationService;
    private CancellationTokenSource? _searchCts;

    private string _minRentText = string.Empty;
    private string _maxRentText = string.Empty;
    private double _minArea = DefaultMinArea;
    private double _maxArea = DefaultMaxArea;
    private double _maxDistanceMeters = DefaultMaxDistanceMeters;
    private string _selectedRoomType = AllRoomTypesValue;
    private bool _onlyVerified = true;
    private int _totalResultCount;
    private bool _isSearching;
    private bool _isFromLiveApi;
    private SearchSortOptionModel _selectedSortOption;

    public string MinRentText
    {
        get => _minRentText;
        set
        {
            if (SetProperty(ref _minRentText, value))
            {
                TriggerApplyFilters();
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
                TriggerApplyFilters();
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
                TriggerApplyFilters();
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
                TriggerApplyFilters();
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
                TriggerApplyFilters();
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
                TriggerApplyFilters();
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
                TriggerApplyFilters();
            }
        }
    }

    public IReadOnlyList<SearchSortOptionModel> SortOptions { get; } =
    [
        new("distance", "Gần ICTU nhất"),
        new("price_asc", "Giá thấp đến cao"),
        new("price_desc", "Giá cao đến thấp"),
        new("area_desc", "Diện tích lớn nhất")
    ];

    public SearchSortOptionModel SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (value is not null && SetProperty(ref _selectedSortOption, value))
            {
                TriggerApplyFilters();
            }
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set => SetProperty(ref _isSearching, value);
    }

    public bool IsFromLiveApi
    {
        get => _isFromLiveApi;
        private set
        {
            if (SetProperty(ref _isFromLiveApi, value))
            {
                OnPropertyChanged(nameof(DataSourceBadgeText));
            }
        }
    }

    public string DataSourceBadgeText => IsFromLiveApi ? "🟢 API T04.1 trực tuyến" : "⚡ Bộ lọc Offline";

    public string FormattedMaxDistance => MaxDistanceMeters < 1000
        ? $"≤ {MaxDistanceMeters:0} m từ cổng ICTU"
        : $"≤ {MaxDistanceMeters / 1000.0:0.1} km từ cổng ICTU";

    public string FormattedAreaRange => $"{MinArea:0.#} m² – {MaxArea:0.#} m²";

    public int TotalResultCount
    {
        get => _totalResultCount;
        private set
        {
            if (SetProperty(ref _totalResultCount, value))
            {
                OnPropertyChanged(nameof(ResultCountText));
            }
        }
    }

    public string ResultCountText => $"Tìm thấy {TotalResultCount} phòng phù hợp";

    public bool HasActiveFilters => ActiveFilterChips.Count > 0;

    public bool HasNoResults => !IsSearching && FilteredRooms.Count == 0;

    public ObservableCollection<ActiveFilterChipModel> ActiveFilterChips { get; } = [];

    public ObservableCollection<RoomTypeOptionModel> RoomTypeOptions { get; } = [];

    public ObservableCollection<FilteredRoomCardModel> FilteredRooms { get; } = [];

    public IAsyncRelayCommand ClearFiltersCommand { get; }

    public IAsyncRelayCommand ApplyFiltersCommand { get; }

    public IRelayCommand<RoomTypeOptionModel> SelectRoomTypeCommand { get; }

    public IRelayCommand<ActiveFilterChipModel> RemoveFilterChipCommand { get; }

    public IRelayCommand<string> ApplyPricePresetCommand { get; }

    public IRelayCommand<string> ApplyAreaPresetCommand { get; }

    public IAsyncRelayCommand<FilteredRoomCardModel> OpenRoomDetailCommand { get; }

    public SearchFilterViewModel(
        ISearchFilterService? searchFilterService = null,
        IAppNavigationService? navigationService = null)
    {
        _searchFilterService = searchFilterService ?? new MockSearchFilterService();
        _navigationService = navigationService;
        _selectedSortOption = SortOptions[0];

        ClearFiltersCommand = new AsyncRelayCommand(ClearFiltersAsync);
        ApplyFiltersCommand = new AsyncRelayCommand(() => ApplyFiltersAsync());
        SelectRoomTypeCommand = new RelayCommand<RoomTypeOptionModel>(SelectRoomType);
        RemoveFilterChipCommand = new RelayCommand<ActiveFilterChipModel>(RemoveFilterChip);
        ApplyPricePresetCommand = new RelayCommand<string>(ApplyPricePreset);
        ApplyAreaPresetCommand = new RelayCommand<string>(ApplyAreaPreset);
        OpenRoomDetailCommand = new AsyncRelayCommand<FilteredRoomCardModel>(OpenRoomDetailAsync);

        InitializeRoomTypes();
        _ = ApplyFiltersAsync();
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
        TriggerApplyFilters();
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
        TriggerApplyFilters();
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
                TriggerApplyFilters();
                break;
            case "roomType":
                SelectedRoomType = AllRoomTypesValue;
                break;
            case "distance":
                MaxDistanceMeters = DefaultMaxDistanceMeters;
                break;
        }
    }

    public async Task ClearFiltersAsync()
    {
        _minRentText = string.Empty;
        _maxRentText = string.Empty;
        _minArea = DefaultMinArea;
        _maxArea = DefaultMaxArea;
        _maxDistanceMeters = DefaultMaxDistanceMeters;
        _selectedRoomType = AllRoomTypesValue;
        _onlyVerified = true;
        _selectedSortOption = SortOptions[0];

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
        OnPropertyChanged(nameof(SelectedSortOption));

        await ApplyFiltersAsync();
    }

    private void TriggerApplyFilters()
    {
        _ = ApplyFiltersAsync();
    }

    public async Task ApplyFiltersAsync(CancellationToken cancellationToken = default)
    {
        var minRent = ParseOptionalDecimal(MinRentText);
        var maxRent = ParseOptionalDecimal(MaxRentText);

        RebuildActiveFilterChips(minRent, maxRent);

        _searchCts?.Cancel();
        _searchCts = cancellationToken == default
            ? new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var token = _searchCts.Token;

        try
        {
            IsSearching = true;
            OnPropertyChanged(nameof(HasNoResults));

            var query = new SearchListingsQueryContract
            {
                CampusId = "ictu-main",
                MinRent = minRent,
                MaxRent = maxRent,
                MinArea = MinArea > DefaultMinArea ? MinArea : null,
                MaxArea = MaxArea < DefaultMaxArea ? MaxArea : null,
                RoomType = string.Equals(SelectedRoomType, AllRoomTypesValue, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : SelectedRoomType,
                MaxDistanceMeters = MaxDistanceMeters,
                SortBy = SelectedSortOption.Key,
                PageIndex = 1,
                PageSize = 20
            };

            var fetchResult = await _searchFilterService.SearchAsync(query, token);
            if (token.IsCancellationRequested)
                return;

            var response = fetchResult.Response;
            IsFromLiveApi = fetchResult.IsFromLiveApi;

            FilteredRooms.Clear();
            var colors = new[] { "#4F46E5", "#7C3AED", "#0D9488", "#2563EB", "#4338CA" };
            for (var i = 0; i < response.Items.Count; i++)
            {
                var item = response.Items[i];
                if (OnlyVerified && !item.IsVerified)
                    continue;

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
                    PhotoBadgeText = ResolvePhotoBadge(item.Id),
                    FallbackColorHex = colors[i % colors.Length]
                });
            }

            TotalResultCount = OnlyVerified ? Math.Min(response.TotalCount, FilteredRooms.Count == response.Items.Count ? response.TotalCount : FilteredRooms.Count) : response.TotalCount;
        }
        catch (OperationCanceledException)
        {
            // Expected when rapid filter edits supersede an earlier request
        }
        finally
        {
            IsSearching = false;
            OnPropertyChanged(nameof(HasActiveFilters));
            OnPropertyChanged(nameof(HasNoResults));
            OnPropertyChanged(nameof(ResultCountText));
        }
    }

    private void RebuildActiveFilterChips(decimal? parsedMinRent, decimal? parsedMaxRent)
    {
        ActiveFilterChips.Clear();

        if (parsedMinRent is { } minVal)
        {
            ActiveFilterChips.Add(new ActiveFilterChipModel
            {
                Key = "minRent",
                Label = $"Giá từ {string.Format(ViCulture, "{0:N0} đ", minVal)}"
            });
        }

        if (parsedMaxRent is { } maxVal)
        {
            ActiveFilterChips.Add(new ActiveFilterChipModel
            {
                Key = "maxRent",
                Label = $"Giá ≤ {string.Format(ViCulture, "{0:N0} đ", maxVal)}"
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

        OnPropertyChanged(nameof(HasActiveFilters));
    }

    private async Task OpenRoomDetailAsync(FilteredRoomCardModel? room)
    {
        if (room is null || _navigationService is null)
            return;

        await _navigationService.NavigateToRoomDetailAsync(room.Id);
    }

    private static string ResolvePhotoBadge(string roomId) => roomId.ToLowerInvariant() switch
    {
        "room-2026-001" => "📷 6 ảnh • 🌐 360°",
        "room-2026-002" => "📷 8 ảnh • 🌐 360°",
        "room-2026-005" => "📷 10 ảnh • 🌐 360°",
        "room-2026-008" => "📷 9 ảnh • 🌐 360°",
        _ => "📷 5 ảnh"
    };

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
            "room-2026-004" or "room-ictu-004" or "room-2026-009" => "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=700&q=80",
            "room-2026-005" or "room-ictu-005" => "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=700&q=80",
            "room-2026-006" => "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=700&q=80",
            "room-2026-007" => "https://images.unsplash.com/photo-1616594039964-ae9021a400a0?auto=format&fit=crop&w=700&q=80",
            "room-2026-008" => "https://images.unsplash.com/photo-1595526114035-0d45ed16cfbf?auto=format&fit=crop&w=700&q=80",
            _ => "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=700&q=80"
        };
    }

    private static string CleanDigits(string input)
    {
        return new string(input.Where(char.IsDigit).ToArray());
    }

    private static decimal? ParseOptionalDecimal(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var digits = CleanDigits(input);
        return decimal.TryParse(digits, out var val) && val > 0 ? val : null;
    }
}
