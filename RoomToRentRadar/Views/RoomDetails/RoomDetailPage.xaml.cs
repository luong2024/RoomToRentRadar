using RoomToRentRadar.ViewModels.RoomDetails;

namespace RoomToRentRadar.Views.RoomDetails;

public partial class RoomDetailPage : ContentPage
{
    public RoomDetailPage(RoomDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
