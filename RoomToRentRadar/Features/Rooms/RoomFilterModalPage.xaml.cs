namespace RoomToRentRadar.Features.Rooms
{
    public partial class RoomFilterModalPage : ContentPage
    {
        private readonly RoomFilterPageModel _viewModel;

        public RoomFilterModalPage(RoomFilterPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.AppearingCommand.ExecuteAsync(null);
        }
    }
}

