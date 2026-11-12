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
    private const double PinHitRadiusPixels = 30;

    private static readonly MapsuiColor AccentColor = new(53, 95, 80, 255);
    private static readonly MapsuiColor AccentFill = new(53, 95, 80, 32);
    private static readonly MapsuiColor SelectedColor = new(198, 83, 60, 255);
    private static readonly MapsuiColor SurfaceColor = new(255, 255, 255, 255);
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    private readonly MapSearchPageModel _model;
    // A MemoryLayer falls back to BaseLayer's white VectorStyle, which would paint
    // an opaque white polygon over the basemap; only the feature styles should draw.
    private readonly MemoryLayer _draftLayer = new("Khu vực đang vẽ") { Style = null };
    private readonly MemoryLayer _pinsLayer = new("Phòng trong khu vực") { Style = null };

    public MapSearchPage(MapSearchPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;

        _model.MapDataChanged += (_, _) => UpdateMapLayers();
        _model.FocusRequested += (_, _) => FocusDraft();

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
        map.Tapped += OnMapTapped;

        var (x, y) = SphericalMercator.FromLonLat(106.7009, 10.7769);
        map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 12);
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
        UpdateDraftLayer();
        UpdatePinsLayer();
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
                    Outline = new Pen(AccentColor, 2)
                });
                features.Add(area);
            }
            else if (coordinates.Length >= 2)
            {
                var line = new GeometryFeature(GeometryFactory.CreateLineString(coordinates));
                line.Styles.Add(new VectorStyle { Line = new Pen(AccentColor, 2) });
                features.Add(line);
            }

            var vertexStyle = new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                SymbolScale = 0.3,
                Fill = new MapsuiBrush(SurfaceColor),
                Outline = new Pen(AccentColor, 2)
            };
            foreach (var coordinate in coordinates)
            {
                var vertex = new GeometryFeature(new NtsPoint(coordinate));
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
        foreach (var listing in _model.Listings)
        {
            var (x, y) = SphericalMercator.FromLonLat(listing.Listing.Lng, listing.Listing.Lat);
            var isSelected = ReferenceEquals(listing, _model.SelectedListing);
            var pin = new GeometryFeature(new NtsPoint(x, y));
            pin.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Triangle,
                SymbolScale = isSelected ? 0.6 : 0.45,
                SymbolRotation = 180,
                Fill = new MapsuiBrush(isSelected ? SelectedColor : AccentColor),
                Outline = new Pen(SurfaceColor, 1.5)
            });
            features.Add(pin);
        }

        _pinsLayer.Features = features;
        _pinsLayer.FeaturesWereModified();
    }
}
