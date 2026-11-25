using System.ComponentModel;
using System.Globalization;
using BruTile;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using RoomToRentRadar.Models;
using RoomToRentRadar.Validations;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class ListingEditorPage : ContentPage
{
    private readonly ListingEditorViewModel _model;
    private TileLayer? _tileLayer;
    private readonly MemoryLayer _locationLayer = new("Vị trí phòng") { Style = null };
    private bool _mapInitialized;
    private bool _observing;
    private bool _disposed;
    private bool _mapUpdateQueued;
    private double? _pendingZoomResolution;

    public ListingEditorPage(ListingEditorViewModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
    }

    private void InitializeMapIfNeeded()
    {
        if (_mapInitialized || _disposed)
            return;

        _mapInitialized = true;
        _tileLayer = new TileLayer(new HttpTileSource(new GlobalSphericalMercator(),
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}",
            name: "Esri World Street Map", attribution: new Attribution(
                "Esri, HERE, Garmin, © OpenStreetMap contributors",
                "https://www.esri.com/en-us/legal/terms/full-maps-use-terms"),
            configureHttpRequestMessage: request => request.Headers.UserAgent.ParseAdd("RoomToRentRadar/1.0")));
        LocationMap.Map.Layers.Add(_tileLayer);
        LocationMap.Map.Layers.Add(_locationLayer);
        var (x, y) = SphericalMercator.FromLonLat(106.7009, 10.7769);
        LocationMap.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 8);
        QueueMapPinUpdate(null);
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("..");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_disposed) return;
        if (!_observing)
        {
            _observing = true;
            LocationMap.Map.Tapped += OnMapTapped;
            _model.LatitudeField.PropertyChanged += OnLocationChanged;
            _model.LongitudeField.PropertyChanged += OnLocationChanged;
            _model.CurrentLocationResolved += OnCurrentLocationResolved;
        }
        Dispatcher.Dispatch(InitializeMapIfNeeded);
        if (_model.LoadCommand.CanExecute(null)) _model.LoadCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        _observing = false;
        LocationMap.Map.Tapped -= OnMapTapped;
        _model.LatitudeField.PropertyChanged -= OnLocationChanged;
        _model.LongitudeField.PropertyChanged -= OnLocationChanged;
        _model.CurrentLocationResolved -= OnCurrentLocationResolved;
        base.OnDisappearing();
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        // Login and tab changes retain this page in its navigation stack.
        // Only release temporary photos and map resources after this editor has been removed.
        if (!Navigation.NavigationStack.Contains(this))
        {
            if (!_disposed)
            {
                _disposed = true;
                _observing = false;
                LocationMap.IsVisible = false;
                try { LocationMap.Map.Layers.Clear(); } catch { }
                try { _tileLayer?.Dispose(); } catch { }
            }

            _model.ReleaseSelectedPhotos();
        }
    }

    private void OnMapTapped(object? sender, MapEventArgs args)
    {
        if (!_observing || _disposed || _model.IsBusy || _model.IsLocating) return;
        var (longitude, latitude) = SphericalMercator.ToLonLat(args.WorldPosition.X, args.WorldPosition.Y);
        _model.LatitudeField.Text = latitude.ToString("F6", CultureInfo.InvariantCulture);
        _model.LongitudeField.Text = longitude.ToString("F6", CultureInfo.InvariantCulture);
        args.Handled = true;
    }

    private void OnCurrentLocationResolved(object? sender, (double Latitude, double Longitude) coords)
    {
        QueueMapPinUpdate(1.0);
    }

    private void OnLocationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ListingFormField.Text) || _model.IsLocating) return;
        QueueMapPinUpdate(null);
    }

    private void QueueMapPinUpdate(double? zoomResolution)
    {
        if (!_observing || _disposed) return;
        if (zoomResolution is { } targetZoom &&
            (_pendingZoomResolution is null || targetZoom < _pendingZoomResolution.Value))
        {
            _pendingZoomResolution = targetZoom;
        }

        if (_mapUpdateQueued) return;
        _mapUpdateQueued = true;

        Dispatcher.Dispatch(() =>
        {
            _mapUpdateQueued = false;
            var requestedZoom = _pendingZoomResolution;
            _pendingZoomResolution = null;

            if (!_observing || _disposed) return;
            if (!ListingFormValidator.TryNumber(_model.LatitudeField.Text, out var latitude)
                || !ListingFormValidator.TryNumber(_model.LongitudeField.Text, out var longitude)
                || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            {
                return;
            }

            try
            {
                var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
                UpdatePin(x, y);
                if (requestedZoom is { } zoom)
                {
                    LocationMap.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), zoom);
                }
                else if (LocationMap.Map.Navigator.Viewport.Resolution > 2.5)
                {
                    LocationMap.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 2.0);
                }
                else
                {
                    LocationMap.Map.Navigator.CenterOn(new MPoint(x, y));
                }

                LocationMap.Refresh();
            }
            catch
            {
                // Ignore native SkiaSharp surface disposal races on teardown.
            }
        });
    }

    private void UpdatePin(double x, double y)
    {
        var pin = new GeometryFeature { Geometry = new NetTopologySuite.Geometries.Point(x, y) };
        pin.Styles.Add(new SymbolStyle
        {
            SymbolScale = 0.85,
            Fill = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(0, 114, 188)),
            Outline = new Pen(Mapsui.Styles.Color.White, 2.5)
        });
        _locationLayer.Features = new[] { pin };
        _locationLayer.DataHasChanged();
    }
}
