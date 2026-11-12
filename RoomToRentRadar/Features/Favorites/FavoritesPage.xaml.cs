namespace RoomToRentRadar.Features.Favorites
{
    public partial class FavoritesPage : ContentPage
    {
        private readonly FavoritesPageModel _viewModel;

        public FavoritesPage(FavoritesPageModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadFavoritesAsync();
        }
    }
}

