namespace RoomToRentRadar.Features.Map
{
    public partial class RoomMapPage : ContentPage
    {
        private readonly RoomMapPageModel _viewModel;

        public RoomMapPage(RoomMapPageModel viewModel)
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

        private void OnMapNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Url))
                return;

            if (e.Url.StartsWith("rtr://pin", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;

                int queryIndex = e.Url.IndexOf("roomId=", StringComparison.OrdinalIgnoreCase);
                if (queryIndex >= 0)
                {
                    string idPart = e.Url[(queryIndex + 7)..];
                    int ampIndex = idPart.IndexOf('&');
                    if (ampIndex >= 0) idPart = idPart[..ampIndex];

                    if (int.TryParse(idPart, out int roomId))
                    {
                        _viewModel.SelectPinCommand.Execute(roomId);
                    }
                }
            }
        }
    }
}
