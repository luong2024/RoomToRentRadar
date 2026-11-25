namespace RoomToRentRadar.Features.Home
{
    public partial class HomePage : ContentPage
    {
        private readonly HomePageModel _viewModel;

        public HomePage(HomePageModel viewModel)
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
