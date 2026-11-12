namespace RoomToRentRadar.Features.Map
{
    public partial class RoomMapPage : ContentPage
    {
        private readonly RoomMapPageModel _viewModel;

        public RoomMapPage(RoomMapPageModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;

            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(RoomMapPageModel.MapHtmlContent))
                {
                    UpdateMapHtml();
                }
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadMapRoomsAsync();
            UpdateMapHtml();
        }

        private void UpdateMapHtml()
        {
            if (!string.IsNullOrWhiteSpace(_viewModel.MapHtmlContent))
            {
                MapWebView.Source = new HtmlWebViewSource
                {
                    Html = _viewModel.MapHtmlContent
                };
            }
        }

        /// <summary>
        /// US06 AC2: Intercepts custom URL scheme from Leaflet map marker click and triggers preview card.
        /// </summary>
        private void OnMapNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (e.Url.StartsWith("roommap://select?id="))
            {
                e.Cancel = true;
                var idStr = e.Url.Replace("roommap://select?id=", "");
                if (int.TryParse(idStr, out int roomId))
                {
                    _viewModel.SelectRoomById(roomId);
                }
            }
        }

        private async void OnBackTapped(object? sender, TappedEventArgs e)
        {
            try
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch
            {
            }
        }
    }
}

