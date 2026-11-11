using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RoomToRentRadar.Features.Home.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; } = "RoomToRentRadar";

    [ObservableProperty]
    public partial string Subtitle { get; set; } = "Student Room Rental Radar — ICTU";

    [RelayCommand]
    private async Task NavigateToAuthAsync()
    {
        await Shell.Current.GoToAsync("//auth/login");
    }
}
