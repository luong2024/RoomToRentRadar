using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.Home.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private const int PageSize = 12;

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IApiClient _apiClient;
    private readonly IErrorHandler _errorHandler;
    private readonly INavigationService _navigationService;
    private readonly HashSet<Guid> _listingIds = new();
    private string? _nextCursor;
    private int _generation;
    private ActiveListingFiltersDto? _appliedFilters;
    private RoomType? _draftRoomType;
    private CampusOption? _draftCampus;
    private int _draftDistanceKm = HomeFilterOptions.DefaultDistanceKm;

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

    public HomeViewModel(IApiClient apiClient, IErrorHandler errorHandler, INavigationService navigationService)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
        _navigationService = navigationService;
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
    }

    public ObservableCollection<HomeListingItem> Listings { get; } = new();

    public ObservableCollection<FilterSummaryChip> ActiveFilterChips { get; } = new();

    public ObservableCollection<FilterChip> RoomTypeChips { get; } = new();

    public ObservableCollection<FilterChip> CampusChips { get; } = new();

    public ObservableCollection<FilterChip> DistanceChips { get; } = new();

    public bool HasActiveFilters => ActiveFilterChips.Count > 0;

    public bool IsDistanceEnabled => _draftCampus is not null;

    public bool HasFilterError => !string.IsNullOrEmpty(FilterError);

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool CanLoadMore => HasMore
        && _nextCursor is not null
        && !IsLoadingMore
        && !HasError
        && Listings.Count > 0;

    partial void OnIsLoadingMoreChanged(bool value) => OnPropertyChanged(nameof(CanLoadMore));

    partial void OnHasMoreChanged(bool value) => OnPropertyChanged(nameof(CanLoadMore));

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CanLoadMore));
    }

    partial void OnFilterErrorChanged(string value) => OnPropertyChanged(nameof(HasFilterError));

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
        if (IsBusy)
        {
            IsRefreshing = false;
            return;
        }

        await LoadFirstPageAsync(cancellationToken);
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
        IsLoadingMore = true;
        try
        {
            var page = await _apiClient.GetListingsAsync(
                PageSize,
                _nextCursor,
                _appliedFilters,
                cancellationToken);
            if (generation != _generation)
                return;

            Append(page.Data);
            _nextCursor = page.Pagination.NextCursor;
            HasMore = page.Pagination.HasMore;
            TotalCount = page.TotalCount;
            IsTotalCountCapped = page.TotalCountCapped;
            UpdateResultsText();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
            IsLoadingMore = false;
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
    private void ToggleFilterPanel()
    {
        IsFilterPanelOpen = !IsFilterPanelOpen;
        FilterError = string.Empty;

        if (IsFilterPanelOpen)
            SyncDraftWithAppliedFilters();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ApplyFiltersAsync(CancellationToken cancellationToken)
    {
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

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ClearFiltersAsync(CancellationToken cancellationToken)
    {
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
        IsBusy = true;
        ErrorMessage = string.Empty;
        EmptyMessage = "Đang tải phòng trọ…";
        try
        {
            var page = await _apiClient.GetListingsAsync(
                PageSize,
                cursor: null,
                _appliedFilters,
                cancellationToken);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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
                ActiveFilterChips.Add(new FilterSummaryChip(price));

            if (DescribeArea(filters.MinAreaM2, filters.MaxAreaM2) is { } area)
                ActiveFilterChips.Add(new FilterSummaryChip(area));

            if (filters.RoomType is { } roomType)
            {
                var label = HomeFilterOptions.RoomTypes
                    .Where(option => option.Value == roomType)
                    .Select(option => option.Label)
                    .FirstOrDefault() ?? RoomTypeWire.ToWireValue(roomType);
                ActiveFilterChips.Add(new FilterSummaryChip(label));
            }

            if (filters.CampusLat is { } lat
                && filters.CampusLng is { } lng
                && filters.MaxDistanceKm is { } maxDistanceKm)
            {
                var campus = HomeFilterOptions.Campuses
                    .FirstOrDefault(option => option.Lat == lat && option.Lng == lng);
                var name = campus?.Name ?? "Trường";
                ActiveFilterChips.Add(new FilterSummaryChip($"{name} · {FormatNumber(maxDistanceKm)} km"));
            }
        }

        OnPropertyChanged(nameof(HasActiveFilters));
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
        if (!double.IsFinite(wholeVnd) || wholeVnd > long.MaxValue)
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
                Listings.Add(HomeListingItem.FromDto(listing));
        }
    }
}
