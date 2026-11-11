using RoomToRentRadar.PageModels.Favorites;

namespace RoomToRentRadar.Pages.Favorites
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
            await _viewModel.LoadFavoritesCommand.ExecuteAsync(null);
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            await _viewModel.CommitPendingUndoAsync();
        }
    }
}

