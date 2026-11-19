namespace RoomToRentRadar.Features.Rooms
{
    public partial class RoomDetailPage : ContentPage
    {
        public RoomDetailPage(RoomDetailPageModel viewModel)
        {
            InitializeComponent();

            BindingContext = viewModel;
        }
    }
}