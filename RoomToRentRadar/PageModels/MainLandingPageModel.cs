using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.PageModels.Base;
using RoomToRentRadar.Pages.Controls.Cards.Models;
using RoomToRentRadar.Ports.Contracts.Requests.Rooms;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadar.PageModels;

public partial class MainLandingPageModel : BasePageModel
{
    private readonly IRoomGateway _roomGateway;
    private readonly ISessionStore _sessionStore;
    private readonly INavigationService _navigation;

    public ObservableCollection<RoomCardDisplayModel> Rooms { get; } = [];

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

#if DEBUG
    public bool IsDebugMode => true;
#else
    public bool IsDebugMode => false;
#endif

    public MainLandingPageModel(IRoomGateway roomGateway, ISessionStore sessionStore, INavigationService navigation)
    {
        _roomGateway = roomGateway ?? throw new ArgumentNullException(nameof(roomGateway));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));

        LocalizedStrings.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ExploreErrorTitleText));
            OnPropertyChanged(nameof(ExploreEmptyTitleText));
            OnPropertyChanged(nameof(ExploreEmptyMessageText));
            OnPropertyChanged(nameof(ExploreEmptyActionText));
        };
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
    private Task ClearFiltersAsync() => RunAsync(LoadRoomsAsync);

    private async Task<bool> LoadRoomsAsync()
    {
        var page = await _roomGateway.SearchRoomsAsync(new RoomSearchRequest());

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