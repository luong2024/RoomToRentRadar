namespace RoomToRentRadar.Pages;

public partial class RoomListPage : ContentPage
{
    public RoomListPage(RoomListViewModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }
}