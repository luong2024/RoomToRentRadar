using RoomToRentRadar.PageModels.Rooms;

namespace RoomToRentRadar.Pages.Rooms
{
    public partial class RoomListPage : ContentPage
    {
        private readonly RoomListPageModel _viewModel;

        public RoomListPage(RoomListPageModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_viewModel.Rooms.Count == 0)
            {
                await _viewModel.LoadRoomsCommand.ExecuteAsync(null);
            }
        }
    }
}

