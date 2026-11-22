using RoomToRentRadar.Features.Profile.ViewModels;

namespace RoomToRentRadar.Features.Profile.Views;

public partial class ProfilePage : ContentPage
{
    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnOpenMenuTapped(object? sender, TappedEventArgs e)
    {
        if (Shell.Current is not null)
        {
            Shell.Current.FlyoutIsPresented = true;
        }
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
