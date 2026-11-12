using RoomToRentRadar.PageModels;

namespace RoomToRentRadar.Pages;

public partial class RoomDetailPage : ContentPage
{
    public RoomDetailPage(RoomDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public RoomDetailPage() : this(new RoomDetailViewModel())
    {
    }
}
