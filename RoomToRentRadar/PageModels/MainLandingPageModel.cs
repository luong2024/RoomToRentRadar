using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.PageModels.Base;
using RoomToRentRadar.Pages.Controls.Cards.Models;
using RoomToRentRadar.Ports.Contracts.Common;
using RoomToRentRadar.Ports.Contracts.Responses.Rooms;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;
using RoomToRentRadar.Services.Search.Core;
using ViewState = RoomToRentRadar.PageModels.Base.ViewState;

namespace RoomToRentRadar.PageModels;

/// <summary>Keeps the singleton store from rooting the PageModel (US04 AC2/AC3, memory-leak guard).</summary>
internal sealed class WeakCriteriaSubscription
{
    private readonly WeakReference<MainLandingPageModel> _target;
    private readonly ISearchCriteriaStore _store;

    public WeakCriteriaSubscription(MainLandingPageModel target, ISearchCriteriaStore store)
    {
        _target = new WeakReference<MainLandingPageModel>(target);
        _store = store;
        _store.CriteriaChanged += OnCriteriaChanged;
    }

    private void OnCriteriaChanged(object? sender, RoomSearchCriteria criteria)
    {
        if (_target.TryGetTarget(out var target))
        {
            target.OnCriteriaChanged(criteria);
            return;
        }

        _store.CriteriaChanged -= OnCriteriaChanged;
    }
}

/// <summary>
/// SCR-HOME-01 Explore list (US01): verified rooms sorted by distance from the selected campus, loaded
/// 20 per page (BR-SRCH-05).
/// </summary>
public partial class MainLandingPageModel : BasePageModel
{
    private const int PageSize = 20;

    private readonly IRoomGateway _roomGateway;
    private readonly INavigationService _navigation;
    private readonly ISearchCriteriaStore _searchCriteriaStore;

    // The loaded results, kept so cards can be rebuilt in another language (US15).
    private readonly List<RoomCardDto> _loadedRooms = [];
    private readonly HashSet<string> _loadedIds = new(StringComparer.Ordinal);

    // Criteria of the list on screen: further pages must belong to it even if a newer reload failed.
    private RoomSearchCriteria? _loadedCriteria;
    private int _loadedPage;
    private bool _hasMorePages;

    // Changes whenever the list is being replaced, so late results for the old list are dropped.
    private int _listVersion;

    public ObservableCollection<RoomCardDisplayModel> Rooms { get; } = [];

    public ObservableCollection<ActiveFilterChipModel> ActiveFilterChips { get; } = [];

    [ObservableProperty]
    public partial int TotalRoomCount { get; set; }

    [ObservableProperty]
    public partial int ActiveFilterCount { get; set; }

    [ObservableProperty]
    public partial bool HasActiveFilters { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CampusChipDescription))]
    public partial string CampusName { get; set; } = "ICTU";

    /// <summary>Unread dot on the bell; stays off until notifications supply data (T07.3).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotificationsDescription))]
    public partial bool HasUnreadNotifications { get; set; }

    /// <summary>Rooms picked for comparison; the tray stays hidden until selection exists (T10.x).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCompareSelection))]
    [NotifyPropertyChangedFor(nameof(CompareCountText))]
    public partial int CompareCount { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    public partial bool IsLoadMoreFailed { get; set; }

    public bool HasCompareSelection => CompareCount > 0;

    /// <summary>
    /// First load failed with nothing to show. The page draws this itself with a filled button: the shared
    /// error card's outline button is unreadable on a light surface.
    /// </summary>
    public bool IsLoadFailed => State == ViewState.Error && !HasContent;

    public bool IsBodyVisible => !IsLoadFailed;

    public string ExploreErrorTitleText => AppResources.GetString("Explore_Error_Title");
    public string ExploreEmptyTitleText => AppResources.GetString("Explore_Empty_Title");
    public string ExploreEmptyMessageText => AppResources.GetString("Explore_Empty_Message");
    public string ExploreEmptyActionText => AppResources.GetString("Explore_Empty_Action");

    public string FiltersButtonText => AppResources.GetString("Explore_FiltersButton");

    public string ClearAllText => AppResources.GetString("Explore_ClearAll");

    public string SortButtonDescription => AppResources.GetString("Explore_SortButtonDescription");

    public string FiltersButtonDescription => string.Format(
        CultureInfo.CurrentCulture,
        AppResources.GetString("Explore_FiltersButtonDescription"),
        ActiveFilterCount);

    public string ResultCountText => TotalRoomCount == 1
        ? AppResources.GetString("Explore_RoomsCountSingular")
        : string.Format(
            CultureInfo.CurrentCulture,
            AppResources.GetString("Explore_RoomsCountPlural"),
            TotalRoomCount);

    public string VerifiedOnlySuffixText => AppResources.GetString("Explore_VerifiedOnlySuffix");

    public string NearText => AppResources.GetString("Explore_Near");

    public string CampusChipDescription => string.Format(
        CultureInfo.CurrentCulture,
        AppResources.GetString("Explore_CampusChipDescription"),
        CampusName);

    public string NotificationsDescription => AppResources.GetString(
        HasUnreadNotifications ? "Explore_NotificationsUnreadDescription" : "Explore_NotificationsDescription");

    public string MapDescription => AppResources.GetString("Explore_MapDescription");

    public string SaveSearchText => AppResources.GetString("Explore_SaveSearch");

    public string CompareCountText => string.Format(
        CultureInfo.CurrentCulture,
        AppResources.GetString("Explore_CompareCount"),
        CompareCount);

    public string CompareNowText => AppResources.GetString("Explore_CompareNow");

    public string LoadMoreErrorText => AppResources.GetString("Explore_LoadMoreError");

    public string RetryText => AppResources.GetString("State_Retry");

    public string CurrentSortLabel => _searchCriteriaStore.Current.EffectiveSort switch
    {
        "price_asc" => AppResources.GetString("Sort_PriceAsc_Short"),
        "price_desc" => AppResources.GetString("Sort_PriceDesc_Short"),
        "newest" => AppResources.GetString("Sort_Newest_Short"),
        _ => AppResources.GetString("Sort_Distance_Short")
    };

    public MainLandingPageModel(
        IRoomGateway roomGateway,
        INavigationService navigation,
        ISearchCriteriaStore? searchCriteriaStore = null)
    {
        _roomGateway = roomGateway ?? throw new ArgumentNullException(nameof(roomGateway));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _searchCriteriaStore = searchCriteriaStore ?? new SearchCriteriaStore();
        _ = new WeakCriteriaSubscription(this, _searchCriteriaStore);
        RefreshFilterState();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(State) or nameof(HasContent))
        {
            OnPropertyChanged(nameof(IsLoadFailed));
            OnPropertyChanged(nameof(IsBodyVisible));
        }
    }

    protected override void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(ExploreErrorTitleText));
        OnPropertyChanged(nameof(ExploreEmptyTitleText));
        OnPropertyChanged(nameof(ExploreEmptyMessageText));
        OnPropertyChanged(nameof(ExploreEmptyActionText));
        OnPropertyChanged(nameof(FiltersButtonText));
        OnPropertyChanged(nameof(ClearAllText));
        OnPropertyChanged(nameof(SortButtonDescription));
        OnPropertyChanged(nameof(FiltersButtonDescription));
        OnPropertyChanged(nameof(ResultCountText));
        OnPropertyChanged(nameof(VerifiedOnlySuffixText));
        OnPropertyChanged(nameof(CurrentSortLabel));
        OnPropertyChanged(nameof(NearText));
        OnPropertyChanged(nameof(CampusChipDescription));
        OnPropertyChanged(nameof(NotificationsDescription));
        OnPropertyChanged(nameof(MapDescription));
        OnPropertyChanged(nameof(SaveSearchText));
        OnPropertyChanged(nameof(CompareCountText));
        OnPropertyChanged(nameof(CompareNowText));
        OnPropertyChanged(nameof(LoadMoreErrorText));
        OnPropertyChanged(nameof(RetryText));
        RefreshFilterState();

        for (var index = 0; index < _loadedRooms.Count; index++)
        {
            Rooms[index] = ToCard(_loadedRooms[index]);
        }
    }

    /// <summary>
    /// Called whenever the screen appears. Coming back to an up-to-date list keeps it and its scroll
    /// position; criteria changes reload through the store.
    /// </summary>
    public Task InitializeAsync() =>
        State == ViewState.Content && _searchCriteriaStore.Current.IsEquivalentTo(_loadedCriteria)
            ? Task.CompletedTask
            : RunAsync(LoadRoomsAsync);

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(LoadRoomsAsync, isRefresh: true);

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        // After a failure only the Retry button asks again, so scrolling cannot loop on an error.
        if (!_hasMorePages || IsLoadingMore || IsLoading || IsRefreshing || IsLoadMoreFailed
            || _loadedCriteria is null)
        {
            return;
        }

        var version = _listVersion;
        var pageNumber = _loadedPage + 1;
        IsLoadingMore = true;
        try
        {
            var page = await _roomGateway.SearchRoomsAsync(
                _loadedCriteria.ToRoomSearchRequest(pageNumber, PageSize));
            if (version == _listVersion)
            {
                Append(page, pageNumber);
            }
        }
        catch (Exception)
        {
            if (version == _listVersion)
            {
                IsLoadMoreFailed = true;
            }
        }
        finally
        {
            if (version == _listVersion)
            {
                IsLoadingMore = false;
            }
        }
    }

    [RelayCommand]
    private Task RetryLoadMoreAsync()
    {
        if (!IsLoadMoreFailed)
        {
            return Task.CompletedTask;
        }

        IsLoadMoreFailed = false;
        return LoadMoreAsync();
    }

    [RelayCommand]
    private Task ClearFiltersAsync()
    {
        var current = _searchCriteriaStore.Current;
        var cleared = current.ClearFiltersPreservingCampus();
        _searchCriteriaStore.ClearFilters();

        // Reload explicitly only when nothing changed (no CriteriaChanged event fired).
        return current.IsEquivalentTo(cleared)
            ? RunAsync(LoadRoomsAsync)
            : Task.CompletedTask;
    }

    [RelayCommand]
    private Task OpenFiltersAsync() => _navigation.GoToAsync(AppRoutes.SearchFilters);

    [RelayCommand]
    private void RemoveFilterChip(ActiveFilterChipModel? chip)
    {
        if (chip is null)
            return;

        _searchCriteriaStore.RemoveChip(chip.Kind, chip.RoomType);
    }

    [RelayCommand]
    private void ApplySort(string? sortKey) => _searchCriteriaStore.SetSort(sortKey);

    /// <summary>Set by the page to show the native sort ActionSheet.</summary>
    public Func<Task>? SortSheetRequested { get; set; }

    [RelayCommand]
    private async Task OpenSortAsync()
    {
        if (SortSheetRequested is not null)
            await SortSheetRequested();
    }

    [RelayCommand]
    private Task OpenRoomAsync(string? roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Task.CompletedTask;
        }

        var query = new Dictionary<string, object> { ["roomId"] = roomId };
        if (_loadedCriteria?.CampusId is { } campusId)
        {
            query["campusId"] = campusId;
        }

        return _navigation.GoToAsync(AppRoutes.RoomDetail, query);
    }

    [RelayCommand]
    private Task OpenCampusAsync() => _navigation.GoToAsync(AppRoutes.AccountCampus);

    [RelayCommand]
    private Task OpenNotificationsAsync() => OpenForSignedInUserAsync(AppRoutes.Notifications);

    [RelayCommand]
    private Task SaveSearchAsync() => OpenForSignedInUserAsync(AppRoutes.SavedSearches);

    [RelayCommand]
    private Task OpenMapAsync() => _navigation.GoToTabAsync(AppRoutes.Map);

    [RelayCommand]
    private Task OpenCompareAsync() => _navigation.GoToAsync(AppRoutes.Compare);

    // BR-AUTH-07: guests are asked to sign in first and come back to Explore.
    private async Task OpenForSignedInUserAsync(string route)
    {
        if (await _navigation.RequireSignInAsync("//" + AppRoutes.Explore))
        {
            await _navigation.GoToAsync(route);
        }
    }

    private async Task<bool> LoadRoomsAsync()
    {
        _listVersion++;
        IsLoadingMore = false;
        var criteria = _searchCriteriaStore.Current;

        var page = await _roomGateway.SearchRoomsAsync(criteria.ToRoomSearchRequest(pageNumber: 1, PageSize));

        _listVersion++;
        _loadedCriteria = criteria;
        _loadedRooms.Clear();
        _loadedIds.Clear();
        Rooms.Clear();
        IsLoadingMore = false;
        IsLoadMoreFailed = false;
        Append(page, pageNumber: 1);

        return Rooms.Count > 0;
    }

    private void Append(PagedResult<RoomCardDto> page, int pageNumber)
    {
        foreach (var dto in page.Items)
        {
            // Listings can move between pages while scrolling; show each room once.
            if (_loadedIds.Add(dto.Id))
            {
                _loadedRooms.Add(dto);
                Rooms.Add(ToCard(dto));
            }
        }

        _loadedPage = pageNumber;
        _hasMorePages = page.Items.Count > 0 && (long)pageNumber * PageSize < page.TotalCount;
        TotalRoomCount = page.TotalCount;
    }

    private RoomCardDisplayModel ToCard(RoomCardDto dto) => new()
    {
        Id = dto.Id,
        Title = dto.Title,
        PricePerMonth = dto.MonthlyRentVnd,
        AreaSquareMeters = dto.AreaM2,
        DistanceMeters = dto.DistanceKm * 1000,
        ThumbnailUrl = dto.CoverThumbUrl ?? string.Empty,
        IsVerified = dto.IsVerified,
        IsBookmarked = dto.IsFavourite,
        RoomType = dto.RoomType,
        Furnishing = dto.Furnishing,
        CampusName = _loadedCriteria?.CampusDisplayName ?? CampusName,
        Language = AppResources.Culture?.Name ?? string.Empty
    };

    internal void OnCriteriaChanged(RoomSearchCriteria criteria)
    {
        RefreshFilterState();
        _ = RunAsync(LoadRoomsAsync);
    }

    private void RefreshFilterState()
    {
        var criteria = _searchCriteriaStore.Current;
        ActiveFilterCount = criteria.ActiveFilterCount;
        HasActiveFilters = criteria.HasActiveFilters;
        CampusName = criteria.CampusDisplayName;

        ActiveFilterChips.Clear();
        foreach (var chip in ActiveFilterChipBuilder.Build(criteria))
            ActiveFilterChips.Add(chip);

        OnPropertyChanged(nameof(CurrentSortLabel));
    }

    partial void OnActiveFilterCountChanged(int value) =>
        OnPropertyChanged(nameof(FiltersButtonDescription));

    partial void OnTotalRoomCountChanged(int value) =>
        OnPropertyChanged(nameof(ResultCountText));
}
