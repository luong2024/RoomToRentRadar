namespace RoomToRentRadar.Features.Rooms
{
    public partial class PhotoGallery360Page : ContentPage
    {
        private readonly PhotoGallery360PageModel _viewModel;

        public PhotoGallery360Page(PhotoGallery360PageModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;

            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(PhotoGallery360PageModel.PanoramaHtmlContent) ||
                    e.PropertyName == nameof(PhotoGallery360PageModel.SelectedTab))
                {
                    UpdatePanoramaView();
                }
            };
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            UpdatePanoramaView();
        }

        private void UpdatePanoramaView()
        {
            if (_viewModel.Is360TabSelected && !string.IsNullOrWhiteSpace(_viewModel.PanoramaHtmlContent))
            {
                PanoramaWebView.Source = new HtmlWebViewSource
                {
                    Html = _viewModel.PanoramaHtmlContent
                };
            }
        }
    }
}

