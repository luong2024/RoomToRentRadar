using RoomToRentRadar.ViewModels.RoomDetails;

namespace RoomToRentRadar.Views.RoomDetails;

public partial class RoomDetailPage : ContentPage
{
    public RoomDetailPage(RoomDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnDisappearing()
    {
        (BindingContext as RoomDetailViewModel)?.PauseTravelInfo();
        base.OnDisappearing();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RoomDetailViewModel viewModel)
        {
            viewModel.ResumeTravelInfo();
            if (viewModel.Room is not null) _ = viewModel.LoadTravelInfoAsync();
        }
    }
}
