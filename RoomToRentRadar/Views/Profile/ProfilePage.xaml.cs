using RoomToRentRadar.ViewModels.Profile;

namespace RoomToRentRadar.Views.Profile;

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
        if (BindingContext is ProfileViewModel viewModel && viewModel.ShouldLoadOnAppearing)
        {
            await viewModel.LoadCommand.ExecuteAsync(null);
        }
    }
}
