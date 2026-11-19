using System.Globalization;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.Features.RoomDetail.Views;

public partial class DirectionsPage : ContentPage, IQueryAttributable
{
    public DirectionsPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!TryReadCoordinate(query, "to_lat", out var destinationLat)
            || !TryReadCoordinate(query, "to_lng", out var destinationLng))
        {
            return;
        }

        double? originLat = TryReadCoordinate(query, "from_lat", out var fromLat) ? fromLat : null;
        double? originLng = TryReadCoordinate(query, "from_lng", out var fromLng) ? fromLng : null;

        MapWebView.Source = DirectionsUrlBuilder.Build(
            destinationLat,
            destinationLng,
            originLat,
            originLng);
    }

    private static bool TryReadCoordinate(IDictionary<string, object> query, string key, out double value)
    {
        value = default;

        return query.TryGetValue(key, out var raw)
            && double.TryParse(
                Convert.ToString(raw, CultureInfo.InvariantCulture),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e) => SetLoading(true);

    private void OnNavigated(object? sender, WebNavigatedEventArgs e) => SetLoading(false);

    private void SetLoading(bool isLoading)
    {
        LoadingPanel.IsVisible = isLoading;
    }
}
