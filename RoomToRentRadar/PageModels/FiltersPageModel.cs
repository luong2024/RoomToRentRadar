using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Domain.Enums;
using RoomToRentRadar.PageModels.Base;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;
using RoomToRentRadar.Services.Search.Core;

namespace RoomToRentRadar.PageModels;

/// <summary>
/// SCR-SRCH-01 view model (US04 / T04.2): edits a draft copy of <see cref="RoomSearchCriteria"/>,
/// debounces the live result count and only commits to the store on "Show N rooms".
/// </summary>
public partial class FiltersPageModel : BasePageModel, IDisposable
{
    private readonly IRoomGateway _roomGateway;
    private readonly ISearchCriteriaStore _criteriaStore;
    private readonly INavigationService _navigation;
    private readonly TimeSpan _debounceDelay;

    private long? _minRentVnd;
    private long? _maxRentVnd;
    private double? _minAreaM2;
    private double? _maxAreaM2;
    private double? _maxDistanceKm;
    private string? _sort;
    private string? _campusId;
    private string _campusDisplayName = "ICTU";
    private List<RoomType> _roomTypes = [];
    private bool _isSyncingInputs;
    private bool _initialized;
    private bool _rentTextInvalid;
    private bool _areaTextInvalid;
    private CancellationTokenSource? _debounceCts;
    private int _requestGeneration;
    private int _liveResultCount;
    private FilterValidationResult _validation = FilterValidationResult.Valid;

    public FiltersPageModel(
        IRoomGateway roomGateway,
        ISearchCriteriaStore criteriaStore,
        INavigationService navigation,
        TimeSpan? debounceDelay = null)
    {
        _roomGateway = roomGateway ?? throw new ArgumentNullException(nameof(roomGateway));
        _criteriaStore = criteriaStore ?? throw new ArgumentNullException(nameof(criteriaStore));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(300);
    }

    // ---- Slider values (two-way with RangeSliderControl) ----
    [ObservableProperty]
    public partial double RentSliderLower { get; set; }

    [ObservableProperty]
    public partial double RentSliderUpper { get; set; } = FilterRangeValidator.MaxRentBoundVnd;

    [ObservableProperty]
    public partial double AreaSliderLower { get; set; } = FilterRangeValidator.MinAreaBoundM2;

    [ObservableProperty]
    public partial double AreaSliderUpper { get; set; } = FilterRangeValidator.MaxAreaBoundM2;

    // ---- Numeric entry values ----
    [ObservableProperty]
    public partial string MinRentText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MaxRentText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MinAreaText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MaxAreaText { get; set; } = string.Empty;

    // ---- Inline errors / banners ----
    [ObservableProperty]
    public partial string PriceErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AreaErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DistanceErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasNetworkError { get; set; }

    [ObservableProperty]
    public partial string NetworkErrorMessage { get; set; } = string.Empty;

    // ---- Live count / apply button ----
    [ObservableProperty]
    public partial bool IsCounting { get; set; }

    [ObservableProperty]
    public partial bool HasLiveResultCount { get; set; }

    [ObservableProperty]
    public partial bool CanApply { get; set; } = true;

    public int LiveResultCount => _liveResultCount;

    public bool HasPriceError => !string.IsNullOrEmpty(PriceErrorMessage);

    public bool HasAreaError => !string.IsNullOrEmpty(AreaErrorMessage);

    public bool HasDistanceError => !string.IsNullOrEmpty(DistanceErrorMessage);

    public string RentRangeLabel => FormatRentRangeLabel();

    public string AreaRangeLabel => FormatAreaRangeLabel();

    public string ShowRoomsText
    {
        get
        {
            if (IsCounting)
                return AppResources.GetString("Filters_Counting");

            if (!HasLiveResultCount)
                return AppResources.GetString("Filters_ShowRoomsFallback");

            return LiveResultCount switch
            {
                0 => AppResources.GetString("Filters_ShowRoomsZero"),
                1 => AppResources.GetString("Filters_ShowRoomsSingular"),
                _ => string.Format(CultureInfo.CurrentCulture, AppResources.GetString("Filters_ShowRoomsPlural"), LiveResultCount)
            };
        }
    }

    // ---- Localized labels ----
    public string TitleText => AppResources.GetString("Filters_Title");
    public string ClearText => AppResources.GetString("Filters_Clear");
    public string CloseDescription => AppResources.GetString("Filters_CloseDescription");
    public string MonthlyRentText => AppResources.GetString("Filters_MonthlyRent");
    public string AreaText => AppResources.GetString("Filters_Area");
    public string RoomTypeText => AppResources.GetString("Filters_RoomType");
    public string SortByText => AppResources.GetString("Filters_SortBy");
    public string MinLabel => AppResources.GetString("Filters_MinLabel");
    public string MaxLabel => AppResources.GetString("Filters_MaxLabel");
    public string MinRentPlaceholder => AppResources.GetString("Filters_MinRentPlaceholder");
    public string MaxRentPlaceholder => AppResources.GetString("Filters_MaxRentPlaceholder");
    public string MinAreaPlaceholder => AppResources.GetString("Filters_MinAreaPlaceholder");
    public string MaxAreaPlaceholder => AppResources.GetString("Filters_MaxAreaPlaceholder");
    public string PresetUnder1_5MText => AppResources.GetString("Filters_Preset_Under1_5M");
    public string PresetUnder2MText => AppResources.GetString("Filters_Preset_Under2M");
    public string PresetUnder3MText => AppResources.GetString("Filters_Preset_Under3M");
    public string SortDistanceText => AppResources.GetString("Sort_Distance");
    public string SortPriceAscText => AppResources.GetString("Sort_PriceAsc");
    public string SortPriceDescText => AppResources.GetString("Sort_PriceDesc");
    public string SortNewestText => AppResources.GetString("Sort_Newest");
    public string DistanceTitleText => string.Format(
        CultureInfo.CurrentCulture,
        AppResources.GetString("Filters_MaxDistanceToCampus"),
        _campusDisplayName);

    public string Distance1Text => FormatDistanceOption(1);
    public string Distance2Text => FormatDistanceOption(2);
    public string Distance3Text => FormatDistanceOption(3);
    public string Distance5Text => FormatDistanceOption(5);
    public string Distance10Text => FormatDistanceOption(10);

    public string RoomTypeBoardingRoomText => AppResources.GetString("RoomType_BoardingRoom");
    public string RoomTypeMiniApartmentText => AppResources.GetString("RoomType_MiniApartment");
    public string RoomTypeWholeHouseText => AppResources.GetString("RoomType_WholeHouse");
    public string RoomTypeDormitoryText => AppResources.GetString("RoomType_Dormitory");

    // ---- Selection state ----
    public bool IsPresetUnder1_5MSelected => IsPresetSelected(1_500_000);
    public bool IsPresetUnder2MSelected => IsPresetSelected(2_000_000);
    public bool IsPresetUnder3MSelected => IsPresetSelected(3_000_000);

    public bool IsBoardingRoomSelected => _roomTypes.Contains(RoomType.BoardingRoom);
    public bool IsMiniApartmentSelected => _roomTypes.Contains(RoomType.MiniApartment);
    public bool IsWholeHouseSelected => _roomTypes.Contains(RoomType.WholeHouse);
    public bool IsDormitorySelected => _roomTypes.Contains(RoomType.Dormitory);

    public bool IsDistance1Selected => _maxDistanceKm == 1.0;
    public bool IsDistance2Selected => _maxDistanceKm == 2.0;
    public bool IsDistance3Selected => _maxDistanceKm == 3.0;
    public bool IsDistance5Selected => _maxDistanceKm == 5.0;
    public bool IsDistance10Selected => _maxDistanceKm == 10.0;

    public bool IsSortDistanceSelected => string.Equals(EffectiveSort, RoomSearchRequest.DefaultSort, StringComparison.Ordinal);
    public bool IsSortPriceAscSelected => string.Equals(EffectiveSort, "price_asc", StringComparison.Ordinal);
    public bool IsSortPriceDescSelected => string.Equals(EffectiveSort, "price_desc", StringComparison.Ordinal);
    public bool IsSortNewestSelected => string.Equals(EffectiveSort, "newest", StringComparison.Ordinal);

    private string EffectiveSort => string.IsNullOrWhiteSpace(_sort) ? RoomSearchRequest.DefaultSort : _sort;

    public async Task InitializeAsync()
    {
        if (!_initialized)
        {
            _initialized = true;
            LoadDraftFromStore();
            SyncAllFromDraft();
        }

        RefreshLocalizedProperties();
        await RefreshLiveCountAsync();
    }

    // ---- Slider handlers ----
    partial void OnRentSliderLowerChanged(double value) => OnRentSliderChanged();

    partial void OnRentSliderUpperChanged(double value) => OnRentSliderChanged();

    partial void OnAreaSliderLowerChanged(double value) => OnAreaSliderChanged();

    partial void OnAreaSliderUpperChanged(double value) => OnAreaSliderChanged();

    private void OnRentSliderChanged()
    {
        if (_isSyncingInputs)
            return;

        var lower = Math.Clamp(RentSliderLower, FilterRangeValidator.MinRentBoundVnd, FilterRangeValidator.MaxRentBoundVnd);
        var upper = Math.Clamp(RentSliderUpper, FilterRangeValidator.MinRentBoundVnd, FilterRangeValidator.MaxRentBoundVnd);
        if (lower > upper)
            (lower, upper) = (upper, lower);

        // Thumbs resting on the outer bounds mean "no limit" for that side.
        long? min = lower <= FilterRangeValidator.MinRentBoundVnd ? null : (long)Math.Round(lower);
        long? max = upper >= FilterRangeValidator.MaxRentBoundVnd ? null : (long)Math.Round(upper);
        ApplyRentValues(min, max, syncSliders: false);
    }

    private void OnAreaSliderChanged()
    {
        if (_isSyncingInputs)
            return;

        var lower = Math.Clamp(AreaSliderLower, FilterRangeValidator.MinAreaBoundM2, FilterRangeValidator.MaxAreaBoundM2);
        var upper = Math.Clamp(AreaSliderUpper, FilterRangeValidator.MinAreaBoundM2, FilterRangeValidator.MaxAreaBoundM2);
        if (lower > upper)
            (lower, upper) = (upper, lower);

        double? min = lower <= FilterRangeValidator.MinAreaBoundM2 ? null : Math.Round(lower);
        double? max = upper >= FilterRangeValidator.MaxAreaBoundM2 ? null : Math.Round(upper);
        ApplyAreaValues(min, max, syncSliders: false);
    }

    // ---- Entry handlers ----
    partial void OnMinRentTextChanged(string value) => OnRentTextChanged();

    partial void OnMaxRentTextChanged(string value) => OnRentTextChanged();

    partial void OnMinAreaTextChanged(string value) => OnAreaTextChanged();

    partial void OnMaxAreaTextChanged(string value) => OnAreaTextChanged();

    private void OnRentTextChanged()
    {
        if (_isSyncingInputs)
            return;

        var minOk = TryParseRent(MinRentText, out var min);
        var maxOk = TryParseRent(MaxRentText, out var max);
        _rentTextInvalid = !minOk || !maxOk;
        ApplyRentValues(minOk ? min : null, maxOk ? max : null, syncSliders: true);
    }

    private void OnAreaTextChanged()
    {
        if (_isSyncingInputs)
            return;

        var minOk = TryParseArea(MinAreaText, out var min);
        var maxOk = TryParseArea(MaxAreaText, out var max);
        _areaTextInvalid = !minOk || !maxOk;
        ApplyAreaValues(minOk ? min : null, maxOk ? max : null, syncSliders: true);
    }

    private void ApplyRentValues(long? min, long? max, bool syncSliders)
    {
        _minRentVnd = min;
        _maxRentVnd = max;
        _rentTextInvalid = false;

        _isSyncingInputs = true;
        try
        {
            MinRentText = min?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            MaxRentText = max?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            if (syncSliders)
            {
                RentSliderLower = Math.Clamp(min ?? FilterRangeValidator.MinRentBoundVnd, FilterRangeValidator.MinRentBoundVnd, FilterRangeValidator.MaxRentBoundVnd);
                RentSliderUpper = Math.Clamp(max ?? FilterRangeValidator.MaxRentBoundVnd, FilterRangeValidator.MinRentBoundVnd, FilterRangeValidator.MaxRentBoundVnd);
            }
        }
        finally
        {
            _isSyncingInputs = false;
        }

        RefreshRentDerivedProperties();
        Revalidate(scheduleCount: true);
    }

    private void ApplyAreaValues(double? min, double? max, bool syncSliders)
    {
        _minAreaM2 = min;
        _maxAreaM2 = max;
        _areaTextInvalid = false;

        _isSyncingInputs = true;
        try
        {
            MinAreaText = min?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty;
            MaxAreaText = max?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty;
            if (syncSliders)
            {
                AreaSliderLower = Math.Clamp(min ?? FilterRangeValidator.MinAreaBoundM2, FilterRangeValidator.MinAreaBoundM2, FilterRangeValidator.MaxAreaBoundM2);
                AreaSliderUpper = Math.Clamp(max ?? FilterRangeValidator.MaxAreaBoundM2, FilterRangeValidator.MinAreaBoundM2, FilterRangeValidator.MaxAreaBoundM2);
            }
        }
        finally
        {
            _isSyncingInputs = false;
        }

        RefreshAreaDerivedProperties();
        Revalidate(scheduleCount: true);
    }

    private void RefreshRentDerivedProperties()
    {
        OnPropertyChanged(nameof(RentRangeLabel));
        OnPropertyChanged(nameof(IsPresetUnder1_5MSelected));
        OnPropertyChanged(nameof(IsPresetUnder2MSelected));
        OnPropertyChanged(nameof(IsPresetUnder3MSelected));
    }

    private void RefreshAreaDerivedProperties()
    {
        OnPropertyChanged(nameof(AreaRangeLabel));
    }

    private void RefreshSelectionProperties()
    {
        OnPropertyChanged(nameof(IsBoardingRoomSelected));
        OnPropertyChanged(nameof(IsMiniApartmentSelected));
        OnPropertyChanged(nameof(IsWholeHouseSelected));
        OnPropertyChanged(nameof(IsDormitorySelected));
        OnPropertyChanged(nameof(IsDistance1Selected));
        OnPropertyChanged(nameof(IsDistance2Selected));
        OnPropertyChanged(nameof(IsDistance3Selected));
        OnPropertyChanged(nameof(IsDistance5Selected));
        OnPropertyChanged(nameof(IsDistance10Selected));
        OnPropertyChanged(nameof(IsSortDistanceSelected));
        OnPropertyChanged(nameof(IsSortPriceAscSelected));
        OnPropertyChanged(nameof(IsSortPriceDescSelected));
        OnPropertyChanged(nameof(IsSortNewestSelected));
    }

    // ---- Commands ----
    [RelayCommand]
    private void SelectRentPreset(string? presetVnd)
    {
        if (!long.TryParse(presetVnd, NumberStyles.None, CultureInfo.InvariantCulture, out var preset))
            return;

        if (IsPresetSelected(preset))
        {
            ApplyRentValues(null, null, syncSliders: true);
            return;
        }

        ApplyRentValues(null, preset, syncSliders: true);
    }

    [RelayCommand]
    private void ToggleRoomType(RoomType roomType)
    {
        if (_roomTypes.Contains(roomType))
            _roomTypes.Remove(roomType);
        else
            _roomTypes.Add(roomType);

        _roomTypes.Sort();
        RefreshSelectionProperties();
        Revalidate(scheduleCount: true);
    }

    [RelayCommand]
    private void SelectMaxDistance(string? kmText)
    {
        if (!double.TryParse(kmText, NumberStyles.Float, CultureInfo.InvariantCulture, out var km))
            return;

        _maxDistanceKm = _maxDistanceKm == km ? null : km;
        OnPropertyChanged(nameof(IsDistance1Selected));
        OnPropertyChanged(nameof(IsDistance2Selected));
        OnPropertyChanged(nameof(IsDistance3Selected));
        OnPropertyChanged(nameof(IsDistance5Selected));
        OnPropertyChanged(nameof(IsDistance10Selected));
        Revalidate(scheduleCount: true);
    }

    [RelayCommand]
    private void SelectSort(string? sortKey)
    {
        _sort = string.IsNullOrWhiteSpace(sortKey) ||
                string.Equals(sortKey, RoomSearchRequest.DefaultSort, StringComparison.Ordinal)
            ? null
            : sortKey;

        OnPropertyChanged(nameof(IsSortDistanceSelected));
        OnPropertyChanged(nameof(IsSortPriceAscSelected));
        OnPropertyChanged(nameof(IsSortPriceDescSelected));
        OnPropertyChanged(nameof(IsSortNewestSelected));
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        var cleared = _criteriaStore.Current.ClearFiltersPreservingCampus();
        _campusId = cleared.CampusId;
        _campusDisplayName = cleared.CampusDisplayName;
        _minRentVnd = null;
        _maxRentVnd = null;
        _minAreaM2 = null;
        _maxAreaM2 = null;
        _maxDistanceKm = null;
        _sort = null;
        _roomTypes.Clear();
        _rentTextInvalid = false;
        _areaTextInvalid = false;
        SyncAllFromDraft();
        RefreshLocalizedProperties();
        await RefreshLiveCountAsync();
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        CancelPendingCount();
        _criteriaStore.Apply(BuildDraftCriteria());
        await _navigation.GoBackAsync();
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        CancelPendingCount();
        await _navigation.GoBackAsync();
    }

    [RelayCommand]
    private Task RetryCountAsync() => RefreshLiveCountAsync();

    // ---- Live count ----
    public async Task RefreshLiveCountAsync()
    {
        if (!_validation.IsValid)
            return;

        CancelPendingCount();
        var generation = ++_requestGeneration;
        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        await ExecuteCountAsync(cts, generation);
    }

    private void ScheduleLiveCount()
    {
        CancelPendingCount();
        var generation = ++_requestGeneration;
        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        _ = ExecuteCountAsync(cts, generation);
    }

    private async Task ExecuteCountAsync(CancellationTokenSource cts, int generation)
    {
        try
        {
            if (_debounceDelay > TimeSpan.Zero)
                await Task.Delay(_debounceDelay, cts.Token);

            if (cts.IsCancellationRequested || generation != _requestGeneration)
                return;

            IsCounting = true;
            HasNetworkError = false;
            NetworkErrorMessage = string.Empty;

            var page = await _roomGateway.SearchRoomsAsync(
                BuildDraftCriteria().ToRoomSearchRequest(pageNumber: 1, pageSize: 1),
                cts.Token);

            if (cts.IsCancellationRequested || generation != _requestGeneration)
                return;

            _liveResultCount = page.TotalCount;
            HasLiveResultCount = true;
            OnPropertyChanged(nameof(LiveResultCount));
            OnPropertyChanged(nameof(ShowRoomsText));
        }
        catch (OperationCanceledException)
        {
        }
        catch (ApiException exception)
        {
            HandleApiException(exception);
        }
        catch (Exception exception)
        {
            ShowNetworkError(exception);
        }
        finally
        {
            if (generation == _requestGeneration)
                IsCounting = false;
        }
    }

    private void CancelPendingCount()
    {
        // Only cancel: disposing here could race with a cache lookup that still registers the token.
        var cts = _debounceCts;
        _debounceCts = null;
        cts?.Cancel();
        _requestGeneration++;
    }

    private void HandleApiException(ApiException exception)
    {
        switch (exception.ErrorCode)
        {
            case ProblemCodes.InvalidPriceRange:
                PriceErrorMessage = AppResources.GetString("Error_INVALID_PRICE_RANGE");
                break;
            case ProblemCodes.InvalidAreaRange:
                AreaErrorMessage = AppResources.GetString("Error_INVALID_AREA_RANGE");
                break;
            case ProblemCodes.InvalidDistanceRange:
                DistanceErrorMessage = AppResources.GetString("Error_INVALID_DISTANCE_RANGE");
                break;
            default:
                ShowNetworkError(exception);
                break;
        }
    }

    private void ShowNetworkError(Exception exception)
    {
        HasNetworkError = true;
        NetworkErrorMessage = ErrorMessageMapper.Map(exception, null);
    }

    // ---- Draft / validation ----
    private void LoadDraftFromStore()
    {
        var criteria = _criteriaStore.Current;
        _campusId = criteria.CampusId;
        _campusDisplayName = criteria.CampusDisplayName;
        _minRentVnd = criteria.MinRentVnd;
        _maxRentVnd = criteria.MaxRentVnd;
        _minAreaM2 = criteria.MinAreaM2;
        _maxAreaM2 = criteria.MaxAreaM2;
        _maxDistanceKm = criteria.MaxDistanceKm;
        _sort = criteria.Sort;
        _roomTypes = criteria.RoomTypes.OrderBy(t => t).ToList();
    }

    private void SyncAllFromDraft()
    {
        _isSyncingInputs = true;
        try
        {
            RentSliderLower = Math.Clamp(_minRentVnd ?? FilterRangeValidator.MinRentBoundVnd, FilterRangeValidator.MinRentBoundVnd, FilterRangeValidator.MaxRentBoundVnd);
            RentSliderUpper = Math.Clamp(_maxRentVnd ?? FilterRangeValidator.MaxRentBoundVnd, FilterRangeValidator.MinRentBoundVnd, FilterRangeValidator.MaxRentBoundVnd);
            AreaSliderLower = Math.Clamp(_minAreaM2 ?? FilterRangeValidator.MinAreaBoundM2, FilterRangeValidator.MinAreaBoundM2, FilterRangeValidator.MaxAreaBoundM2);
            AreaSliderUpper = Math.Clamp(_maxAreaM2 ?? FilterRangeValidator.MaxAreaBoundM2, FilterRangeValidator.MinAreaBoundM2, FilterRangeValidator.MaxAreaBoundM2);
            MinRentText = _minRentVnd?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            MaxRentText = _maxRentVnd?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            MinAreaText = _minAreaM2?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty;
            MaxAreaText = _maxAreaM2?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty;
        }
        finally
        {
            _isSyncingInputs = false;
        }

        RefreshRentDerivedProperties();
        RefreshAreaDerivedProperties();
        RefreshSelectionProperties();
        Revalidate(scheduleCount: false);
    }

    private RoomSearchCriteria BuildDraftCriteria() => new()
    {
        CampusId = _campusId,
        CampusDisplayName = _campusDisplayName,
        MinRentVnd = _minRentVnd,
        MaxRentVnd = _maxRentVnd,
        MinAreaM2 = _minAreaM2,
        MaxAreaM2 = _maxAreaM2,
        RoomTypes = _roomTypes.ToArray(),
        MaxDistanceKm = _maxDistanceKm,
        Sort = _sort
    };

    private void Revalidate(bool scheduleCount)
    {
        _validation = FilterRangeValidator.Validate(
            _minRentVnd,
            _maxRentVnd,
            _minAreaM2,
            _maxAreaM2,
            _maxDistanceKm,
            _sort);

        if (_rentTextInvalid)
            _validation = _validation with { PriceErrorCode = ProblemCodes.InvalidPriceRange };

        if (_areaTextInvalid)
            _validation = _validation with { AreaErrorCode = ProblemCodes.InvalidAreaRange };

        RefreshValidationMessages();
        CanApply = _validation.IsValid;

        if (!_validation.IsValid)
        {
            CancelPendingCount();
            return;
        }

        if (scheduleCount)
            ScheduleLiveCount();
    }

    private void RefreshValidationMessages()
    {
        PriceErrorMessage = MessageFor(_validation.PriceErrorCode);
        AreaErrorMessage = MessageFor(_validation.AreaErrorCode);
        DistanceErrorMessage = MessageFor(_validation.DistanceErrorCode);
    }

    private static string MessageFor(string? errorCode) =>
        string.IsNullOrEmpty(errorCode) ? string.Empty : AppResources.GetString("Error_" + errorCode);

    private bool IsPresetSelected(long presetVnd) =>
        _minRentVnd is null && _maxRentVnd == presetVnd;

    private static bool TryParseRent(string? text, out long? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        var cleaned = new string(text.Where(ch => !char.IsWhiteSpace(ch) && ch != '.' && ch != ',').ToArray());
        if (cleaned.Length > 0 &&
            long.TryParse(cleaned, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseArea(string? text, out double? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        var cleaned = text.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) ||
            double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    private string FormatRentRangeLabel()
    {
        var chips = ActiveFilterChipBuilder.Build(new RoomSearchCriteria
        {
            MinRentVnd = _minRentVnd,
            MaxRentVnd = _maxRentVnd
        });
        return chips.Count == 0 ? AppResources.GetString("Filters_Range_Any") : chips[0].Label;
    }

    private string FormatAreaRangeLabel()
    {
        var chips = ActiveFilterChipBuilder.Build(new RoomSearchCriteria
        {
            MinAreaM2 = _minAreaM2,
            MaxAreaM2 = _maxAreaM2
        });
        return chips.Count == 0 ? AppResources.GetString("Filters_Range_Any") : chips[0].Label;
    }

    private static string FormatDistanceOption(double km) => string.Format(
        CultureInfo.CurrentCulture,
        AppResources.GetString("Filters_DistanceOption"),
        km.ToString("0.#", CultureInfo.CurrentCulture));

    private void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(ClearText));
        OnPropertyChanged(nameof(CloseDescription));
        OnPropertyChanged(nameof(MonthlyRentText));
        OnPropertyChanged(nameof(AreaText));
        OnPropertyChanged(nameof(RoomTypeText));
        OnPropertyChanged(nameof(SortByText));
        OnPropertyChanged(nameof(MinLabel));
        OnPropertyChanged(nameof(MaxLabel));
        OnPropertyChanged(nameof(MinRentPlaceholder));
        OnPropertyChanged(nameof(MaxRentPlaceholder));
        OnPropertyChanged(nameof(MinAreaPlaceholder));
        OnPropertyChanged(nameof(MaxAreaPlaceholder));
        OnPropertyChanged(nameof(PresetUnder1_5MText));
        OnPropertyChanged(nameof(PresetUnder2MText));
        OnPropertyChanged(nameof(PresetUnder3MText));
        OnPropertyChanged(nameof(SortDistanceText));
        OnPropertyChanged(nameof(SortPriceAscText));
        OnPropertyChanged(nameof(SortPriceDescText));
        OnPropertyChanged(nameof(SortNewestText));
        OnPropertyChanged(nameof(DistanceTitleText));
        OnPropertyChanged(nameof(Distance1Text));
        OnPropertyChanged(nameof(Distance2Text));
        OnPropertyChanged(nameof(Distance3Text));
        OnPropertyChanged(nameof(Distance5Text));
        OnPropertyChanged(nameof(Distance10Text));
        OnPropertyChanged(nameof(RoomTypeBoardingRoomText));
        OnPropertyChanged(nameof(RoomTypeMiniApartmentText));
        OnPropertyChanged(nameof(RoomTypeWholeHouseText));
        OnPropertyChanged(nameof(RoomTypeDormitoryText));
        OnPropertyChanged(nameof(RentRangeLabel));
        OnPropertyChanged(nameof(AreaRangeLabel));
        OnPropertyChanged(nameof(ShowRoomsText));
        RefreshValidationMessages();
    }

    protected override void OnLanguageChanged() => RefreshLocalizedProperties();

    partial void OnPriceErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasPriceError));

    partial void OnAreaErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasAreaError));

    partial void OnDistanceErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasDistanceError));

    partial void OnCanApplyChanged(bool value) => ApplyCommand.NotifyCanExecuteChanged();

    partial void OnIsCountingChanged(bool value) => OnPropertyChanged(nameof(ShowRoomsText));

    partial void OnHasLiveResultCountChanged(bool value) => OnPropertyChanged(nameof(ShowRoomsText));

    public void Dispose()
    {
        CancelPendingCount();
        GC.SuppressFinalize(this);
    }
}
