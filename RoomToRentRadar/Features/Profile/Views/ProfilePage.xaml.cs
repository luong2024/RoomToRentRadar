using RoomToRentRadar.Features.Profile.ViewModels;

namespace RoomToRentRadar.Features.Profile.Views;

public partial class ProfilePage : ContentPage
{
    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ProfileViewModel viewModel) await viewModel.LoadCommand.ExecuteAsync(null);
    }
}
