using System.Globalization;
using Microsoft.Maui.ApplicationModel;
using RoomToRentRadar.Helpers;

namespace RoomToRentRadar.Views;

public partial class DirectionsPage : ContentPage, IQueryAttributable
{
    private static readonly string[] AppSchemes =
        ["intent:", "geo:", "market:", "tel:", "mailto:", "sms:", "whatsapp:"];

    private double? _destLat;
    private double? _destLng;
    private double? _origLat;
    private double? _origLng;

    public DirectionsPage()
    {
        InitializeComponent();
        DirectionsWebView.Navigating += OnNavigating;
        DirectionsWebView.Navigated += OnNavigated;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!TryReadCoordinate(query, "to_lat", out var destinationLat)
            || !TryReadCoordinate(query, "to_lng", out var destinationLng))
        {
            DirectionsWebView.IsVisible = false;
            ErrorBanner.IsVisible = true;
            OpenExternalMapsButton.IsEnabled = false;
            RouteSubtitleLabel.Text = "Không xác định được tọa độ";
            return;
        }

        _destLat = destinationLat;
        _destLng = destinationLng;
        _origLat = TryReadCoordinate(query, "from_lat", out var fromLat) ? fromLat : null;
        _origLng = TryReadCoordinate(query, "from_lng", out var fromLng) ? fromLng : null;

        ErrorBanner.IsVisible = false;
        DirectionsWebView.IsVisible = true;
        OpenExternalMapsButton.IsEnabled = true;
        RouteSubtitleLabel.Text = _origLat.HasValue && _origLng.HasValue
            ? "Lộ trình từ trường học tới phòng trọ"
            : "Bản đồ vị trí phòng trọ";

        DirectionsWebView.Source = new HtmlWebViewSource
        {
            Html = DirectionsUrlBuilder.BuildPage(
                destinationLat,
                destinationLng,
                _origLat,
                _origLng),
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

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (Shell.Current is { } shell)
        {
            await shell.GoToAsync("..");
        }
    }

    private async void OnOpenExternalMapsClicked(object? sender, EventArgs e)
    {
        if (_destLat is not { } dLat || _destLng is not { } dLng)
            return;

        try
        {
            var url = _origLat is { } oLat && _origLng is { } oLng
                ? string.Create(CultureInfo.InvariantCulture, $"https://www.google.com/maps/dir/?api=1&origin={oLat},{oLng}&destination={dLat},{dLng}")
                : string.Create(CultureInfo.InvariantCulture, $"https://www.google.com/maps/search/?api=1&query={dLat},{dLng}");
            await Launcher.Default.OpenAsync(new Uri(url));
        }
        catch
        {
        }
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url is { } url
            && AppSchemes.Any(scheme => url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)))
        {
            e.Cancel = true;
        }
    }

    private void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
    }
}
