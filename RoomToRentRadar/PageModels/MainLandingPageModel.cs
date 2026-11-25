using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.PageModels.Base;
using RoomToRentRadar.Pages.Controls.Cards.Models;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;
using RoomToRentRadar.Services.Search.Core;

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

public partial class MainLandingPageModel : BasePageModel
{
    private readonly IRoomGateway _roomGateway;
    private readonly ISessionStore _sessionStore;
    private readonly INavigationService _navigation;
    private readonly ISearchCriteriaStore _searchCriteriaStore;

    public ObservableCollection<RoomCardDisplayModel> Rooms { get; } = [];

    public ObservableCollection<ActiveFilterChipModel> ActiveFilterChips { get; } = [];

    [ObservableProperty]
    public partial int TotalRoomCount { get; set; }

    [ObservableProperty]
    public partial int ActiveFilterCount { get; set; }

    [ObservableProperty]
    public partial bool HasActiveFilters { get; set; }

    [ObservableProperty]
    public partial string WelcomeMessage { get; set; } = "Welcome to RoomToRentRadar!";

    [ObservableProperty]
    public partial string UserDisplayName { get; set; } = "Student";

    [ObservableProperty]
    public partial string UserRole { get; set; } = "Guest";

    [ObservableProperty]
    public partial string UserEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsUserSignedIn { get; set; } = false;

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

    public string CurrentSortLabel => _searchCriteriaStore.Current.EffectiveSort switch
    {
        "price_asc" => AppResources.GetString("Sort_PriceAsc_Short"),
        "price_desc" => AppResources.GetString("Sort_PriceDesc_Short"),
        "newest" => AppResources.GetString("Sort_Newest_Short"),
        _ => AppResources.GetString("Sort_Distance_Short")
    };

#if DEBUG
    public bool IsDebugMode => true;
#else
    public bool IsDebugMode => false;
#endif

    public MainLandingPageModel(
        IRoomGateway roomGateway,
        ISessionStore sessionStore,
        INavigationService navigation,
        ISearchCriteriaStore? searchCriteriaStore = null)
    {
        _roomGateway = roomGateway ?? throw new ArgumentNullException(nameof(roomGateway));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _searchCriteriaStore = searchCriteriaStore ?? new SearchCriteriaStore();
        _ = new WeakCriteriaSubscription(this, _searchCriteriaStore);
        RefreshFilterState();
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
        RefreshFilterState();
    }

    public async Task InitializeAsync()
    {
        var user = await _sessionStore.GetCachedUserProfileAsync();
        if (user != null)
        {
            UserDisplayName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : user.Email;
            UserRole = user.Role;
            UserEmail = user.Email;
            WelcomeMessage = $"Welcome back, {UserDisplayName}!";
            IsUserSignedIn = true;
        }
        else
        {
            UserDisplayName = "Guest";
            UserRole = "Explorer";
            WelcomeMessage = "Welcome to RoomToRentRadar!";
            IsUserSignedIn = false;
        }

        await RunAsync(LoadRoomsAsync);
    }

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(LoadRoomsAsync, isRefresh: true);

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

    private async Task<bool> LoadRoomsAsync()
    {
        var page = await _roomGateway.SearchRoomsAsync(
            _searchCriteriaStore.Current.ToRoomSearchRequest(pageNumber: 1, pageSize: 20));

        TotalRoomCount = page.TotalCount;

        Rooms.Clear();
        foreach (var dto in page.Items)
        {
            Rooms.Add(new RoomCardDisplayModel
            {
                Id = dto.Id,
                Title = dto.Title,
                PricePerMonth = dto.MonthlyRentVnd,
                AreaSquareMeters = dto.AreaM2,
                DistanceMeters = dto.DistanceKm * 1000,
                ThumbnailUrl = dto.CoverThumbUrl ?? string.Empty,
                IsVerified = dto.IsVerified,
                IsBookmarked = dto.IsFavourite
            });
        }

        return Rooms.Count > 0;
    }

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

        ActiveFilterChips.Clear();
        foreach (var chip in ActiveFilterChipBuilder.Build(criteria))
            ActiveFilterChips.Add(chip);

        OnPropertyChanged(nameof(CurrentSortLabel));
    }

    partial void OnActiveFilterCountChanged(int value) =>
        OnPropertyChanged(nameof(FiltersButtonDescription));

    partial void OnTotalRoomCountChanged(int value) =>
        OnPropertyChanged(nameof(ResultCountText));

    [RelayCommand]
    private async Task ViewRoomDetailsAsync(string? roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        var route = $"{AppRoutes.RoomDetail}?roomId={Uri.EscapeDataString(roomId)}";
        var campusId = _searchCriteriaStore.Current.CampusId;
        if (!string.IsNullOrWhiteSpace(campusId))
        {
            route += $"&campusId={Uri.EscapeDataString(campusId)}";
        }

        await _navigation.GoToAsync(route);
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _sessionStore.ClearSessionAsync();
        await _navigation.SignOutToGuestAsync();
    }

    [RelayCommand]
    private async Task OpenUiCatalogAsync()
    {
        await _navigation.GoToAsync("//uicatalog");
    }
}