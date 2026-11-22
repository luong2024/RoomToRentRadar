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
    private readonly MemoryLayer _locationLayer = new("Vị trí phòng") { Style = null };
    private bool _observing;

    public ListingEditorPage(ListingEditorViewModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
        LocationMap.Map.Layers.Add(new TileLayer(new HttpTileSource(new GlobalSphericalMercator(),
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}",
            name: "Esri World Street Map", attribution: new Attribution(
                "Esri, HERE, Garmin, © OpenStreetMap contributors",
                "https://www.esri.com/en-us/legal/terms/full-maps-use-terms"),
            configureHttpRequestMessage: request => request.Headers.UserAgent.ParseAdd("RoomToRentRadar/1.0"))));
        LocationMap.Map.Layers.Add(_locationLayer);
        var (x, y) = SphericalMercator.FromLonLat(106.7009, 10.7769);
        LocationMap.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 8);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_observing)
        {
            _observing = true;
            LocationMap.Map.Tapped += OnMapTapped;
            _model.LatitudeField.PropertyChanged += OnLocationChanged;
            _model.LongitudeField.PropertyChanged += OnLocationChanged;
        }
        if (_model.LoadCommand.CanExecute(null)) _model.LoadCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        _observing = false;
        LocationMap.Map.Tapped -= OnMapTapped;
        _model.LatitudeField.PropertyChanged -= OnLocationChanged;
        _model.LongitudeField.PropertyChanged -= OnLocationChanged;
        base.OnDisappearing();
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        // Login and tab changes retain this page in its navigation stack.
        // Only release temporary photos after this editor has been removed.
        if (!Navigation.NavigationStack.Contains(this)) _model.ReleaseSelectedPhotos();
    }

    private void OnMapTapped(object? sender, MapEventArgs args)
    {
        if (_model.IsBusy) return;
        var (longitude, latitude) = SphericalMercator.ToLonLat(args.WorldPosition.X, args.WorldPosition.Y);
        _model.LatitudeField.Text = latitude.ToString("F6", CultureInfo.InvariantCulture);
        _model.LongitudeField.Text = longitude.ToString("F6", CultureInfo.InvariantCulture);
        args.Handled = true;
    }

    private void OnLocationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ListingFormField.Text)
            || !ListingFormValidator.TryNumber(_model.LatitudeField.Text, out var latitude)
            || !ListingFormValidator.TryNumber(_model.LongitudeField.Text, out var longitude)
            || latitude is < -90 or > 90 || longitude is < -180 or > 180) return;
        var (x, y) = SphericalMercator.FromLonLat(longitude, latitude);
        var pin = new GeometryFeature { Geometry = new NetTopologySuite.Geometries.Point(x, y) };
        pin.Styles.Add(new SymbolStyle
        {
            SymbolScale = 0.8,
            Fill = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(121, 84, 119)),
            Outline = new Pen(Mapsui.Styles.Color.White, 2)
        });
        _locationLayer.Features = new[] { pin };
        _locationLayer.DataHasChanged();
        LocationMap.Map.Navigator.CenterOn(new MPoint(x, y));
        LocationMap.Refresh();
    }
}
