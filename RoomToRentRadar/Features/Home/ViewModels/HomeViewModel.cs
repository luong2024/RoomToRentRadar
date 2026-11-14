using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Constants;

namespace RoomToRentRadar.Features.Home.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly Func<string, Task>? _navigateAsync;

    public HomeViewModel() : this(null)
    {
    }

    internal HomeViewModel(Func<string, Task>? navigateAsync)
    {
        _navigateAsync = navigateAsync;
    }

    [ObservableProperty]
    public partial string Title { get; set; } = "RoomToRentRadar";

    [ObservableProperty]
    public partial string Subtitle { get; set; } = "Tìm phòng trọ lý tưởng quanh ĐH CNTT&TT (ICTU)";

    [ObservableProperty]
    public partial string QuickSearchText { get; set; } = string.Empty;

    [RelayCommand]
    public async Task NavigateToAuthAsync()
    {
        await NavigateToRouteAsync("//auth/login");
    }

    [RelayCommand]
    public async Task ExploreRoomsAsync()
    {
        await QuickFilterAsync(QuickSearchText);
    }

    [RelayCommand]
    public async Task SearchFromHomeAsync()
    {
        await QuickFilterAsync(QuickSearchText);
    }

    [RelayCommand]
    public async Task QuickFilterAsync(string? keyword)
    {
        var trimmed = keyword?.Trim() ?? string.Empty;
        var route = string.IsNullOrEmpty(trimmed)
            ? $"//{AppRoutes.RoomListings}"
            : $"//{AppRoutes.RoomListings}?search={Uri.EscapeDataString(trimmed)}";

        await NavigateToRouteAsync(route);
    }

    private async Task NavigateToRouteAsync(string route)
    {
        if (_navigateAsync is not null)
        {
            await _navigateAsync(route);
            return;
        }

#if ANDROID || IOS || MACCATALYST || WINDOWS
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync(route);
        }
#endif
    }
}
