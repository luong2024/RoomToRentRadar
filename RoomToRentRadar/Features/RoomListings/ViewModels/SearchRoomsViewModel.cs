using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Localization;
using RoomToRentRadar.Features.RoomListings.Models;
using RoomToRentRadar.Features.RoomListings.Services;

namespace RoomToRentRadar.Features.RoomListings.ViewModels;

/// <summary>
/// Manages room search and filter state, validation, active filter chips, and result count.
/// </summary>
public abstract partial class SearchRoomsViewModel : ObservableObject
{
    protected static readonly CultureInfo ViCulture = new("vi-VN");
    public const double DefaultRadiusKm = 5.0;
    public const double IctuLatitude = 21.5852;
    public const double IctuLongitude = 105.8073;

    protected readonly IRoomListingsService RoomListingsService;
    private bool _isUpdatingPricePreset;

    protected SearchRoomsViewModel(IRoomListingsService roomListingsService)
    {
        ArgumentNullException.ThrowIfNull(roomListingsService);
        RoomListingsService = roomListingsService;

        ActiveFilters.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasActiveFilters));
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilterCount));
        };
    }

    public ObservableCollection<ActiveFilterItem> ActiveFilters { get; } = [];

    public bool HasActiveFilters => ActiveFilters.Count > 0;

    public int ActiveFilterCount => ActiveFilters.Count;

    public bool HasActiveFilterCount => ActiveFilters.Count > 0;

    [ObservableProperty]
    public partial bool IsFilterPanelOpen { get; set; }

    partial void OnIsFilterPanelOpenChanged(bool value)
    {
        OnFilterPanelOpenStateChanged(value);
    }

    protected virtual void OnFilterPanelOpenStateChanged(bool isOpen)
    {
    }

    [ObservableProperty]
    public partial int TotalResultCount { get; set; }

    // --- Price Filter ---
    [ObservableProperty]
    public partial string FilterMinPriceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FilterMaxPriceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal? MinPrice { get; set; }

    [ObservableProperty]
    public partial decimal? MaxPrice { get; set; }

    [ObservableProperty]
    public partial string SelectedPricePreset { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasPriceError { get; set; }

    [ObservableProperty]
    public partial string PriceErrorMessage { get; set; } = string.Empty;

    // --- Area Filter ---
    [ObservableProperty]
    public partial string FilterMinAreaText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FilterMaxAreaText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double? MinArea { get; set; }

    [ObservableProperty]
    public partial double? MaxArea { get; set; }

    [ObservableProperty]
    public partial bool HasAreaError { get; set; }

    [ObservableProperty]
    public partial string AreaErrorMessage { get; set; } = string.Empty;

    // --- Distance Filter ---
    [ObservableProperty]
    public partial bool IsDistanceFilterActive { get; set; }

    [ObservableProperty]
    public partial double RadiusInKm { get; set; } = DefaultRadiusKm;

    public string FormattedRadius => IsDistanceFilterActive
        ? $"{RadiusInKm:0.#} km"
        : (LocalizationResourceManager.Instance["Filter_DistanceDisabled"] ?? "Tắt");

    // --- Room Type Filter ---
    [ObservableProperty]
    public partial string SelectedRoomType { get; set; } = "all";

    public bool IsPriceUnder1MSelected =>
        string.Equals(SelectedPricePreset, "under1m", StringComparison.OrdinalIgnoreCase);

    public bool IsPrice1MTo2MSelected =>
        string.Equals(SelectedPricePreset, "1m-2m", StringComparison.OrdinalIgnoreCase);

    public bool IsPrice2MTo3MSelected =>
        string.Equals(SelectedPricePreset, "2m-3m", StringComparison.OrdinalIgnoreCase);

    public bool IsAllRoomTypeSelected =>
        string.IsNullOrWhiteSpace(SelectedRoomType) ||
        string.Equals(SelectedRoomType, "all", StringComparison.OrdinalIgnoreCase);

    public bool IsPrivateRoomSelected =>
        string.Equals(SelectedRoomType, "private", StringComparison.OrdinalIgnoreCase);

    public bool IsSharedRoomSelected =>
        string.Equals(SelectedRoomType, "shared", StringComparison.OrdinalIgnoreCase);

    public bool IsApartmentRoomSelected =>
        string.Equals(SelectedRoomType, "apartment", StringComparison.OrdinalIgnoreCase);

    public bool IsFilterValid => !HasPriceError && !HasAreaError;

    private bool _isFormattingMinPrice;
    private bool _isFormattingMaxPrice;

    partial void OnFilterMinPriceTextChanged(string value)
    {
        if (_isFormattingMinPrice)
        {
            return;
        }

        MinPrice = TryParsePrice(value);
        SyncPricePresetFromInputs();
        ValidateFilters();

        if (!string.IsNullOrWhiteSpace(value) &&
            MinPrice.HasValue &&
            !value.Contains(',') &&
            !value.Contains('.') &&
            value.Trim().Length >= 4)
        {
            var formatted = MinPrice.Value.ToString("N0", ViCulture);
            if (!string.Equals(value, formatted, StringComparison.Ordinal))
            {
                _isFormattingMinPrice = true;
                try
                {
                    FilterMinPriceText = formatted;
                }
                finally
                {
                    _isFormattingMinPrice = false;
                }
            }
        }
    }

    partial void OnFilterMaxPriceTextChanged(string value)
    {
        if (_isFormattingMaxPrice)
        {
            return;
        }

        MaxPrice = TryParsePrice(value);
        SyncPricePresetFromInputs();
        ValidateFilters();

        if (!string.IsNullOrWhiteSpace(value) &&
            MaxPrice.HasValue &&
            !value.Contains(',') &&
            !value.Contains('.') &&
            value.Trim().Length >= 4)
        {
            var formatted = MaxPrice.Value.ToString("N0", ViCulture);
            if (!string.Equals(value, formatted, StringComparison.Ordinal))
            {
                _isFormattingMaxPrice = true;
                try
                {
                    FilterMaxPriceText = formatted;
                }
                finally
                {
                    _isFormattingMaxPrice = false;
                }
            }
        }
    }

    partial void OnFilterMinAreaTextChanged(string value)
    {
        MinArea = TryParseArea(value);
        ValidateFilters();
    }

    partial void OnFilterMaxAreaTextChanged(string value)
    {
        MaxArea = TryParseArea(value);
        ValidateFilters();
    }

    partial void OnRadiusInKmChanged(double value)
    {
        OnPropertyChanged(nameof(FormattedRadius));
    }

    partial void OnIsDistanceFilterActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(FormattedRadius));
    }

    partial void OnSelectedPricePresetChanged(string value)
    {
        OnPropertyChanged(nameof(IsPriceUnder1MSelected));
        OnPropertyChanged(nameof(IsPrice1MTo2MSelected));
        OnPropertyChanged(nameof(IsPrice2MTo3MSelected));
    }

    partial void OnSelectedRoomTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsAllRoomTypeSelected));
        OnPropertyChanged(nameof(IsPrivateRoomSelected));
        OnPropertyChanged(nameof(IsSharedRoomSelected));
        OnPropertyChanged(nameof(IsApartmentRoomSelected));
    }

    partial void OnHasPriceErrorChanged(bool value) => OnPropertyChanged(nameof(IsFilterValid));

    partial void OnHasAreaErrorChanged(bool value) => OnPropertyChanged(nameof(IsFilterValid));

    public virtual bool ValidateFilters()
    {
        // 1. Price validation
        HasPriceError = false;
        PriceErrorMessage = string.Empty;

        if (MinPrice.HasValue && MinPrice.Value < 0)
        {
            HasPriceError = true;
            PriceErrorMessage = LocalizationResourceManager.Instance["Validation_PriceNegative"] ?? "Giá không được nhỏ hơn 0.";
        }
        else if (MaxPrice.HasValue && MaxPrice.Value < 0)
        {
            HasPriceError = true;
            PriceErrorMessage = LocalizationResourceManager.Instance["Validation_PriceNegative"] ?? "Giá không được nhỏ hơn 0.";
        }
        else if (MinPrice.HasValue && MaxPrice.HasValue && MinPrice.Value > MaxPrice.Value)
        {
            HasPriceError = true;
            PriceErrorMessage = LocalizationResourceManager.Instance["Validation_MinPriceGreaterThanMax"] ?? "Giá tối thiểu không được lớn hơn giá tối đa.";
        }

        // 2. Area validation
        HasAreaError = false;
        AreaErrorMessage = string.Empty;

        if (MinArea.HasValue && MinArea.Value < 0)
        {
            HasAreaError = true;
            AreaErrorMessage = LocalizationResourceManager.Instance["Validation_AreaNegative"] ?? "Diện tích không được nhỏ hơn 0.";
        }
        else if (MaxArea.HasValue && MaxArea.Value < 0)
        {
            HasAreaError = true;
            AreaErrorMessage = LocalizationResourceManager.Instance["Validation_AreaNegative"] ?? "Diện tích không được nhỏ hơn 0.";
        }
        else if (MinArea.HasValue && MaxArea.HasValue && MinArea.Value > MaxArea.Value)
        {
            HasAreaError = true;
            AreaErrorMessage = LocalizationResourceManager.Instance["Validation_MinAreaGreaterThanMax"] ?? "Diện tích tối thiểu không được lớn hơn diện tích tối đa.";
        }

        return IsFilterValid;
    }

    [RelayCommand]
    public void OpenFilterPanel()
    {
        IsFilterPanelOpen = true;
    }

    [RelayCommand]
    public void CloseFilterPanel()
    {
        IsFilterPanelOpen = false;
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
                default:
                    FilterMinPriceText = string.Empty;
                    FilterMaxPriceText = string.Empty;
                    break;
            }
        }
        finally
        {
            _isUpdatingPricePreset = false;
        }

        MinPrice = TryParsePrice(FilterMinPriceText);
        MaxPrice = TryParsePrice(FilterMaxPriceText);
        ValidateFilters();
    }

    [RelayCommand]
    public void SelectRoomType(string? roomType)
    {
        SelectedRoomType = string.IsNullOrWhiteSpace(roomType)
            ? "all"
            : roomType.Trim().ToLowerInvariant();
    }

    [RelayCommand]
    public virtual async Task ApplyFiltersAsync()
    {
        if (!ValidateFilters())
        {
            return;
        }

        PopulateActiveFilters();
        IsFilterPanelOpen = false;
        await ExecuteSearchAsync();
    }

    [RelayCommand]
    public virtual async Task ResetFiltersAsync()
    {
        _isUpdatingPricePreset = true;
        try
        {
            FilterMinPriceText = string.Empty;
            FilterMaxPriceText = string.Empty;
            MinPrice = null;
            MaxPrice = null;
            SelectedPricePreset = string.Empty;
        }
        finally
        {
            _isUpdatingPricePreset = false;
        }

        FilterMinAreaText = string.Empty;
        FilterMaxAreaText = string.Empty;
        MinArea = null;
        MaxArea = null;

        IsDistanceFilterActive = false;
        RadiusInKm = DefaultRadiusKm;
        SelectedRoomType = "all";

        HasPriceError = false;
        PriceErrorMessage = string.Empty;
        HasAreaError = false;
        AreaErrorMessage = string.Empty;

        ActiveFilters.Clear();
        IsFilterPanelOpen = false;

        await ExecuteSearchAsync();
    }

    [RelayCommand]
    public async Task RemoveActiveFilterAsync(ActiveFilterItem? item)
    {
        if (item is null)
        {
            return;
        }

        ActiveFilters.Remove(item);

        switch (item.Key)
        {
            case "Price":
                _isUpdatingPricePreset = true;
                try
                {
                    FilterMinPriceText = string.Empty;
                    FilterMaxPriceText = string.Empty;
                    MinPrice = null;
                    MaxPrice = null;
                    SelectedPricePreset = string.Empty;
                }
                finally
                {
                    _isUpdatingPricePreset = false;
                }
                break;

            case "Area":
                FilterMinAreaText = string.Empty;
                FilterMaxAreaText = string.Empty;
                MinArea = null;
                MaxArea = null;
                break;

            case "Distance":
                IsDistanceFilterActive = false;
                RadiusInKm = DefaultRadiusKm;
                break;

            case "RoomType":
                SelectedRoomType = "all";
                break;
        }

        ValidateFilters();
        await ExecuteSearchAsync();
    }

    public virtual RoomSearchCriteria CreateSearchCriteria(int pageNumber = 1, int pageSize = 10, string? keyword = null)
    {
        var hasActiveDistance = IsDistanceFilterActive && RadiusInKm > 0;
        return new RoomSearchCriteria
        {
            MinPrice = MinPrice,
            MaxPrice = MaxPrice,
            MinArea = MinArea,
            MaxArea = MaxArea,
            RoomType = string.Equals(SelectedRoomType, "all", StringComparison.OrdinalIgnoreCase) ? null : SelectedRoomType,
            RadiusInKm = hasActiveDistance ? RadiusInKm : null,
            Latitude = hasActiveDistance ? IctuLatitude : null,
            Longitude = hasActiveDistance ? IctuLongitude : null,
            Keyword = keyword,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    protected virtual void PopulateActiveFilters()
    {
        ActiveFilters.Clear();

        // 1. Price tag
        if (MinPrice.HasValue || MaxPrice.HasValue)
        {
            string priceText;
            if (MinPrice.HasValue && MaxPrice.HasValue)
            {
                priceText = $"{FormatPriceShort(MinPrice.Value)} - {FormatPriceShort(MaxPrice.Value)}";
            }
            else if (MinPrice.HasValue)
            {
                priceText = $"> {FormatPriceShort(MinPrice.Value)}";
            }
            else
            {
                priceText = $"< {FormatPriceShort(MaxPrice!.Value)}";
            }

            ActiveFilters.Add(new ActiveFilterItem
            {
                Key = "Price",
                DisplayText = priceText,
                FilterType = "price"
            });
        }

        // 2. Area tag
        if (MinArea.HasValue || MaxArea.HasValue)
        {
            string areaText;
            if (MinArea.HasValue && MaxArea.HasValue)
            {
                areaText = $"{MinArea.Value:0.#} - {MaxArea.Value:0.#} m²";
            }
            else if (MinArea.HasValue)
            {
                areaText = $"> {MinArea.Value:0.#} m²";
            }
            else
            {
                areaText = $"< {MaxArea!.Value:0.#} m²";
            }

            ActiveFilters.Add(new ActiveFilterItem
            {
                Key = "Area",
                DisplayText = areaText,
                FilterType = "area"
            });
        }

        // 3. Distance tag (only if distance filter is active)
        if (IsDistanceFilterActive && RadiusInKm > 0)
        {
            ActiveFilters.Add(new ActiveFilterItem
            {
                Key = "Distance",
                DisplayText = $"≤ {RadiusInKm:0.#} km",
                FilterType = "distance"
            });
        }

        // 4. Room type tag
        if (!string.IsNullOrWhiteSpace(SelectedRoomType) &&
            !string.Equals(SelectedRoomType, "all", StringComparison.OrdinalIgnoreCase))
        {
            var roomTypeText = SelectedRoomType switch
            {
                "private" => LocalizationResourceManager.Instance["Filter_PrivateRoom"] ?? "Khép kín",
                "shared" => LocalizationResourceManager.Instance["Filter_SharedRoom"] ?? "Ở ghép",
                "apartment" => LocalizationResourceManager.Instance["Filter_Apartment"] ?? "Căn hộ",
                _ => SelectedRoomType
            };

            ActiveFilters.Add(new ActiveFilterItem
            {
                Key = "RoomType",
                DisplayText = roomTypeText,
                FilterType = "roomType"
            });
        }
    }

    protected abstract Task ExecuteSearchAsync();

    private void SyncPricePresetFromInputs()
    {
        if (_isUpdatingPricePreset)
        {
            return;
        }

        var min = MinPrice;
        var max = MaxPrice;

        SelectedPricePreset = (min, max) switch
        {
            (0m, 1_000_000m) => "under1m",
            (1_000_000m, 2_000_000m) => "1m-2m",
            (2_000_000m, 3_000_000m) => "2m-3m",
            _ => string.Empty
        };
    }

    protected static decimal? TryParsePrice(string? priceText)
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

    protected static double? TryParseArea(string? areaText)
    {
        if (string.IsNullOrWhiteSpace(areaText))
        {
            return null;
        }

        var cleaned = areaText
            .Replace("m²", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("m2", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (double.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0)
        {
            return value;
        }

        return null;
    }

    private static string FormatPriceShort(decimal price)
    {
        if (price >= 1_000_000m)
        {
            var inMillions = price / 1_000_000m;
            return $"{inMillions:0.#}M";
        }

        if (price >= 1_000m)
        {
            var inThousands = price / 1_000m;
            return $"{inThousands:0.#}k";
        }

        return price.ToString("N0", ViCulture);
    }
}
