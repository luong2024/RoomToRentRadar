using System.Globalization;
using RoomToRentRadar.Helpers;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.Views;

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

        MapWebView.Source = new HtmlWebViewSource
        {
            Html = DirectionsUrlBuilder.BuildPage(
                destinationLat,
                destinationLng,
                originLat,
                originLng),
        };
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

    private static readonly string[] AppSchemes =
        ["intent:", "geo:", "market:", "tel:", "mailto:", "sms:", "whatsapp:"];

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url is { } url
            && AppSchemes.Any(scheme => url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)))
        {
            e.Cancel = true;
            return;
        }

        SetLoading(true);
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e) => SetLoading(false);

    private void SetLoading(bool isLoading)
    {
        LoadingPanel.IsVisible = isLoading;
    }
}
