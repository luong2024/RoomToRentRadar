using System.ComponentModel;
using System.Collections.Specialized;
using BruTile;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Maui;
using NetTopologySuite.Geometries;
using RoomToRentRadar.Features.Map.ViewModels;
using RoomToRentRadar.Models;
using MapsuiBrush = Mapsui.Styles.Brush;
using MapsuiColor = Mapsui.Styles.Color;
using NtsPoint = NetTopologySuite.Geometries.Point;

namespace RoomToRentRadar.Features.Map.Views;

public partial class MapSearchPage : ContentPage
{
    private const double PinHitRadiusPixels = 36;

    private static readonly MapsuiColor AccentColor = new(35, 79, 64, 255);
    private static readonly MapsuiColor AccentFill = new(35, 79, 64, 18);
    private static readonly MapsuiColor SurfaceColor = new(255, 255, 255, 255);
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    private readonly MapSearchPageModel _model;
    private bool _observing;
    private bool _renderQueued;
    private bool _pinsDirty = true;
    private GeoPoint[] _renderedVertices = [];
    private bool _renderedClosed;
    private Guid? _renderedSelection;
    private readonly Dictionary<Guid, GeometryFeature> _pinFeatures = new();
    // A MemoryLayer falls back to BaseLayer's white VectorStyle, which would paint
    // an opaque white polygon over the basemap; only the feature styles should draw.
    private readonly MemoryLayer _draftLayer = new("Khu vực đang vẽ") { Style = null };
    private readonly MemoryLayer _pinsLayer = new("Phòng trong khu vực") { Style = null };

    public MapSearchPage(MapSearchPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;

        var map = MapView.Map;
        // tile.openstreetmap.org is unreachable on some networks; Esri's ArcGIS
        // street basemap needs no key and keeps the pins readable.
        map.Layers.Add(new TileLayer(new HttpTileSource(
            new GlobalSphericalMercator(),
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}",
            name: "Esri World Street Map",
            attribution: new Attribution(
                "Esri, HERE, Garmin, © OpenStreetMap contributors",
                "https://www.esri.com/en-us/legal/terms/full-maps-use-terms"),
            configureHttpRequestMessage: request =>
                request.Headers.UserAgent.ParseAdd("RoomToRentRadar/1.0"))));
        map.Layers.Add(_draftLayer);
        map.Layers.Add(_pinsLayer);
        var (x, y) = SphericalMercator.FromLonLat(106.7009, 10.7769);
        // Mapsui takes metres per pixel here, not a zoom level. Start near a
        // neighbourhood so phone taps can place distinct street-scale vertices.
        map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 8);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_observing)
            return;
        _observing = true;
        _model.MapDataChanged += OnMapDataChanged;
        _model.FocusRequested += OnFocusRequested;
        _model.PropertyChanged += OnModelPropertyChanged;
        _model.Listings.CollectionChanged += OnListingsChanged;
        MapView.Map.Tapped += OnMapTapped;
        _pinsDirty = true;
        QueueRender();
        StartMotion();
    }

    protected override void OnDisappearing()
    {
        StopMotion();
        _observing = false;
        _model.MapDataChanged -= OnMapDataChanged;
        _model.FocusRequested -= OnFocusRequested;
        _model.PropertyChanged -= OnModelPropertyChanged;
        _model.Listings.CollectionChanged -= OnListingsChanged;
        MapView.Map.Tapped -= OnMapTapped;
        _model.CancelPendingSearch();
        base.OnDisappearing();
    }

    private void OnMapDataChanged(object? sender, EventArgs e) => QueueRender();

    private void OnListingsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _pinsDirty = true;
        QueueRender();
    }

    private void QueueRender()
    {
        if (!_observing || _renderQueued)
            return;
        _renderQueued = true;
        Dispatcher.Dispatch(() =>
        {
            _renderQueued = false;
            if (_observing)
                UpdateMapLayers();
        });
    }

    private void OnFocusRequested(object? sender, EventArgs e) => FocusDraft();

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MapSearchPageModel.SelectedListing)
            || _model.SelectedListing is not { } listing)
            return;

        var (x, y) = SphericalMercator.FromLonLat(listing.Listing.Lng, listing.Listing.Lat);
        MapView.Map.Navigator.CenterOn(new MPoint(x, y));
        Dispatcher.Dispatch(() =>
        {
            if (_observing && ResultCards.Handler is not null && _model.Listings.Contains(listing))
                ResultCards.ScrollTo(listing, position: ScrollToPosition.Center, animate: false);
        });
    }

    private void OnMapTapped(object? sender, MapEventArgs e)
    {
        if (_model.IsDrawing)
        {
            var (lng, lat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
            _model.AddVertex(lat, lng);
            e.Handled = true;
            return;
        }

        var viewport = MapView.Map.Navigator.Viewport;
        MapListingItem? nearest = null;
        var nearestDistance = PinHitRadiusPixels;
        foreach (var listing in _model.Listings)
        {
            var (x, y) = SphericalMercator.FromLonLat(listing.Listing.Lng, listing.Listing.Lat);
            var (screenX, screenY) = viewport.WorldToScreenXY(x, y);
            var dx = screenX - e.ScreenPosition.X;
            var dy = screenY - e.ScreenPosition.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= nearestDistance)
            {
                nearest = listing;
                nearestDistance = distance;
            }
        }

        _model.SelectListing(nearest);
        e.Handled = nearest is not null;
    }

    private void FocusDraft()
    {
        var vertices = _model.Draft.Vertices;
        if (vertices.Count == 0)
            return;

        var points = vertices
            .Select(vertex =>
            {
                var (x, y) = SphericalMercator.FromLonLat(vertex.Lng, vertex.Lat);
                return new MPoint(x, y);
            })
            .ToArray();
        var bounds = new MRect(
            points.Min(p => p.X),
            points.Min(p => p.Y),
            points.Max(p => p.X),
            points.Max(p => p.Y));

        if (bounds.Width < 100 || bounds.Height < 100)
            bounds = bounds.Grow(250);

        MapView.Map.Navigator.ZoomToBox(bounds, MBoxFit.Fit);
    }

    private void UpdateMapLayers()
    {
        // Taps only change the draft. Selection only changes two pin styles.
        // Keep the other layer and its geometries intact on each interaction.
        if (_renderedClosed != _model.Draft.IsClosed
            || !_renderedVertices.SequenceEqual(_model.Draft.Vertices))
        {
            UpdateDraftLayer();
            _renderedVertices = _model.Draft.Vertices.ToArray();
            _renderedClosed = _model.Draft.IsClosed;
        }
        if (_pinsDirty)
        {
            UpdatePinsLayer();
            _pinsDirty = false;
        }
        else
        {
            var selectedId = _model.SelectedListing?.Listing.Id;
            if (_renderedSelection != selectedId)
            {
                if (_renderedSelection is { } oldId && _pinFeatures.TryGetValue(oldId, out var oldPin))
                    SetPinStyle(oldPin, false);
                if (selectedId is { } newId && _pinFeatures.TryGetValue(newId, out var newPin))
                    SetPinStyle(newPin, true);
                OrderSelectedPinLast();
                _pinsLayer.FeaturesWereModified();
            }
        }
        _renderedSelection = _model.SelectedListing?.Listing.Id;
        MapView.Map.RefreshGraphics();
    }

    private void UpdateDraftLayer()
    {
        var features = new List<IFeature>();
        var vertices = _model.Draft.Vertices;

        if (vertices.Count > 0)
        {
            var coordinates = vertices
                .Select(vertex =>
                {
                    var (x, y) = SphericalMercator.FromLonLat(vertex.Lng, vertex.Lat);
                    return new Coordinate(x, y);
                })
                .ToArray();

            if (_model.Draft.IsClosed)
            {
                var ring = _model.Draft.Ring
                    .Select(vertex =>
                    {
                        var (x, y) = SphericalMercator.FromLonLat(vertex.Lng, vertex.Lat);
                        return new Coordinate(x, y);
                    })
                    .ToArray();
                var area = new GeometryFeature(GeometryFactory.CreatePolygon(ring));
                area.Styles.Add(new VectorStyle
                {
                    Fill = new MapsuiBrush(AccentFill),
                    Outline = new Pen(AccentColor, 1.4)
                });
                features.Add(area);
            }
            else if (coordinates.Length >= 2)
            {
                var line = new GeometryFeature(GeometryFactory.CreateLineString(coordinates));
                line.Styles.Add(new VectorStyle { Line = new Pen(AccentColor, 1.4) });
                features.Add(line);
            }

            var vertexStyle = new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                SymbolScale = 0.4,
                Fill = new MapsuiBrush(SurfaceColor),
                Outline = new Pen(AccentColor, 1.8)
            };
            for (var index = 0; index < coordinates.Length; index++)
            {
                var vertex = new GeometryFeature(new NtsPoint(coordinates[index]));
                vertex.Styles.Add(vertexStyle);
                features.Add(vertex);
            }
        }

        _draftLayer.Features = features;
        _draftLayer.FeaturesWereModified();
    }

    private void UpdatePinsLayer()
    {
        var features = new List<IFeature>();
        _pinFeatures.Clear();
        foreach (var listing in _model.Listings)
        {
            var (x, y) = SphericalMercator.FromLonLat(listing.Listing.Lng, listing.Listing.Lat);
            var isSelected = ReferenceEquals(listing, _model.SelectedListing);
            var pin = new GeometryFeature(new NtsPoint(x, y));
            pin["PriceLabel"] = listing.MapPriceText;
            SetPinStyle(pin, isSelected);
            _pinFeatures[listing.Listing.Id] = pin;
            features.Add(pin);
        }

        _pinsLayer.Features = features;
        OrderSelectedPinLast();
        _pinsLayer.FeaturesWereModified();
    }

    private void OrderSelectedPinLast()
    {
        if (_model.SelectedListing is { } listing && _pinFeatures.TryGetValue(listing.Listing.Id, out var selected))
            _pinsLayer.Features = _pinFeatures.Values.Where(pin => !ReferenceEquals(pin, selected)).Append(selected).ToArray();
        else
            _pinsLayer.Features = _pinFeatures.Values.ToArray();
    }

    private static void SetPinStyle(GeometryFeature pin, bool isSelected)
    {
        pin.Styles.Clear();
        pin.Styles.Add(new LabelStyle
        {
            Text = $"\u00a0 {pin["PriceLabel"]} \u00a0",
            ForeColor = isSelected ? SurfaceColor : AccentColor,
            BackColor = new MapsuiBrush(isSelected ? AccentColor : SurfaceColor),
            BorderColor = isSelected ? SurfaceColor : new MapsuiColor(213, 225, 217, 255),
            BorderThickness = isSelected ? 2 : 1,
            CornerRounding = 12,
            Font = new Mapsui.Styles.Font { Size = 12, Bold = true },
            Halo = null,
            HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
            VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
            CollisionDetection = false
        });
    }
}
