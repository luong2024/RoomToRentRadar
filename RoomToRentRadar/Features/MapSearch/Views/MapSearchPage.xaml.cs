using System.ComponentModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using RoomToRentRadar.Features.MapSearch.Models;
using RoomToRentRadar.Features.MapSearch.ViewModels;

namespace RoomToRentRadar.Features.MapSearch.Views;

public partial class MapSearchPage : ContentPage
{
    private readonly DrawingCanvasDrawable _drawable = new();
    private Microsoft.Maui.Controls.Maps.Polygon? _activeMapPolygon;
    private int _draggedAnchorIndex = -1;
    private MapSearchViewModel? _viewModel;

    /// <summary>
    /// Parameterless constructor for Shell / DataTemplate instantiation with DI service resolution fallback.
    /// </summary>
    public MapSearchPage() : this(
        Application.Current?.Handler?.MauiContext?.Services?.GetService<MapSearchViewModel>()
        ?? IPlatformApplication.Current?.Services?.GetService<MapSearchViewModel>()
        ?? new MapSearchViewModel())
    {
    }

    public MapSearchPage(MapSearchViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        viewModel.OnNavigateBackRequested = async () =>
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("//home");
            }
        };

        CanvasView.Drawable = _drawable;

        SearchMap.Loaded += OnSearchMapLoaded;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Cancel active interactions when navigating away
        if (_viewModel is not null)
        {
            if (_viewModel.IsDrawingMode || _viewModel.IsEditingMode)
            {
                _viewModel.CancelDrawingCommand.Execute(null);
            }
        }
    }

    private void OnSearchMapLoaded(object? sender, EventArgs e)
    {
        var initialSpan = MapSpan.FromCenterAndRadius(
            new Location(MapSearchViewModel.DefaultLatitude, MapSearchViewModel.DefaultLongitude),
            Distance.FromKilometers(MapSearchViewModel.DefaultDistanceKm));
        SearchMap.MoveToRegion(initialSpan);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null) return;

        if (e.PropertyName == nameof(MapSearchViewModel.IsEditingMode))
        {
            OnEditingModeChanged(_viewModel.IsEditingMode);
        }
        else if (e.PropertyName == nameof(MapSearchViewModel.HasDrawnPolygon) && !_viewModel.HasDrawnPolygon)
        {
            if (_activeMapPolygon is not null && SearchMap.MapElements.Contains(_activeMapPolygon))
            {
                SearchMap.MapElements.Remove(_activeMapPolygon);
            }
            _activeMapPolygon = null;
            _drawable.Clear();
            _drawable.SetAnchors(Enumerable.Empty<PointF>());
            CanvasView.Invalidate();
        }
    }

    private void OnEditingModeChanged(bool isEditing)
    {
        _drawable.IsEditingMode = isEditing;
        if (isEditing && _viewModel is not null)
        {
            // Temporarily hide map polygon and transfer vertices to screen anchors on canvas
            if (_activeMapPolygon is not null && SearchMap.MapElements.Contains(_activeMapPolygon))
            {
                SearchMap.MapElements.Remove(_activeMapPolygon);
            }

            var screenPoints = ConvertGeoLocationsToScreenPoints(_viewModel.PolygonCoordinates);
            _drawable.SetAnchors(screenPoints);
            CanvasView.Invalidate();
        }
        else if (!isEditing && _viewModel is not null)
        {
            // Finish editing -> re-render polygon on map
            _drawable.SetAnchors(Enumerable.Empty<PointF>());
            CanvasView.Invalidate();
            RenderPolygonOnMap(_viewModel.PolygonCoordinates);
        }
    }

    private void OnCanvasStart(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0 || _viewModel is null) return;
        var pt = e.Touches[0];

        if (_viewModel.IsEditingMode)
        {
            _draggedAnchorIndex = _drawable.HitTestAnchor(pt, radius: 36f);
            _drawable.DraggingAnchorIndex = _draggedAnchorIndex;
            CanvasView.Invalidate();
        }
        else if (_viewModel.IsDrawingMode)
        {
            _drawable.Clear();
            _drawable.AddPoint(pt);
            CanvasView.Invalidate();
        }
    }

    private void OnCanvasDrag(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0 || _viewModel is null) return;
        var pt = e.Touches[0];

        if (_viewModel.IsEditingMode && _draggedAnchorIndex >= 0)
        {
            _drawable.UpdateAnchor(_draggedAnchorIndex, pt);
            CanvasView.Invalidate();

            var newGeo = ConvertSingleScreenPointToGeo(pt);
            _viewModel.UpdateAnchorPosition(_draggedAnchorIndex, newGeo);
        }
        else if (_viewModel.IsDrawingMode)
        {
            _drawable.AddPoint(pt);
            CanvasView.Invalidate();
        }
    }

    private void OnCanvasEnd(object? sender, TouchEventArgs e)
    {
        if (_viewModel is null) return;

        if (_viewModel.IsEditingMode)
        {
            _draggedAnchorIndex = -1;
            _drawable.DraggingAnchorIndex = -1;
            CanvasView.Invalidate();
        }
        else if (_viewModel.IsDrawingMode)
        {
            var points = _drawable.GetPoints();
            if (points.Count >= 3)
            {
                var geoLocations = ConvertScreenPointsToGeoLocations(points);
                _viewModel.CompleteDrawing(geoLocations);
                RenderPolygonOnMap(_viewModel.PolygonCoordinates);
            }
            else
            {
                _viewModel.CompleteDrawing(Enumerable.Empty<GeoCoordinate>());
            }

            _drawable.Clear();
            CanvasView.Invalidate();
        }
    }

    private List<GeoCoordinate> ConvertScreenPointsToGeoLocations(IReadOnlyList<PointF> screenPoints)
    {
        var geoPoints = new List<GeoCoordinate>();
        if (SearchMap.VisibleRegion is null || CanvasView.Width <= 0 || CanvasView.Height <= 0)
            return geoPoints;

        var region = SearchMap.VisibleRegion;
        double centerLat = region.Center.Latitude;
        double centerLon = region.Center.Longitude;
        double latSpan = region.LatitudeDegrees;
        double lonSpan = region.LongitudeDegrees;

        if (latSpan <= 0.0000001 || lonSpan <= 0.0000001)
            return geoPoints;

        double width = CanvasView.Width;
        double height = CanvasView.Height;

        foreach (var pt in screenPoints)
        {
            double relX = (pt.X / width) - 0.5;
            double relY = 0.5 - (pt.Y / height);

            double lat = centerLat + (relY * latSpan);
            double lon = centerLon + (relX * lonSpan);

            lat = Math.Clamp(lat, -89.9999, 89.9999);
            lon = Math.Clamp(lon, -179.9999, 179.9999);

            geoPoints.Add(new GeoCoordinate(lat, lon));
        }

        return geoPoints;
    }

    private GeoCoordinate ConvertSingleScreenPointToGeo(PointF pt)
    {
        if (SearchMap.VisibleRegion is null || CanvasView.Width <= 0 || CanvasView.Height <= 0)
            return new GeoCoordinate(MapSearchViewModel.DefaultLatitude, MapSearchViewModel.DefaultLongitude);

        var region = SearchMap.VisibleRegion;
        if (region.LatitudeDegrees <= 0.0000001 || region.LongitudeDegrees <= 0.0000001)
            return new GeoCoordinate(region.Center.Latitude, region.Center.Longitude);

        double relX = (pt.X / CanvasView.Width) - 0.5;
        double relY = 0.5 - (pt.Y / CanvasView.Height);

        double lat = region.Center.Latitude + (relY * region.LatitudeDegrees);
        double lon = region.Center.Longitude + (relX * region.LongitudeDegrees);

        lat = Math.Clamp(lat, -89.9999, 89.9999);
        lon = Math.Clamp(lon, -179.9999, 179.9999);

        return new GeoCoordinate(lat, lon);
    }

    private List<PointF> ConvertGeoLocationsToScreenPoints(IEnumerable<GeoCoordinate> coords)
    {
        var screenPoints = new List<PointF>();
        if (SearchMap.VisibleRegion is null || CanvasView.Width <= 0 || CanvasView.Height <= 0)
            return screenPoints;

        var region = SearchMap.VisibleRegion;
        double centerLat = region.Center.Latitude;
        double centerLon = region.Center.Longitude;
        double latSpan = region.LatitudeDegrees;
        double lonSpan = region.LongitudeDegrees;

        if (latSpan <= 0.0000001 || lonSpan <= 0.0000001)
            return screenPoints;

        double width = CanvasView.Width;
        double height = CanvasView.Height;

        foreach (var c in coords)
        {
            double relY = (c.Latitude - centerLat) / latSpan;
            double relX = (c.Longitude - centerLon) / lonSpan;

            float x = (float)((relX + 0.5) * width);
            float y = (float)((0.5 - relY) * height);

            if (!float.IsNaN(x) && !float.IsNaN(y))
            {
                screenPoints.Add(new PointF(x, y));
            }
        }

        return screenPoints;
    }

    private void RenderPolygonOnMap(IEnumerable<GeoCoordinate> locations)
    {
        if (_activeMapPolygon is not null && SearchMap.MapElements.Contains(_activeMapPolygon))
        {
            SearchMap.MapElements.Remove(_activeMapPolygon);
        }

        var polygon = new Microsoft.Maui.Controls.Maps.Polygon
        {
            StrokeColor = Color.FromArgb("#2563EB"),
            StrokeWidth = 3,
            FillColor = Color.FromArgb("#332563EB")
        };

        foreach (var loc in locations)
        {
            polygon.Geopath.Add(new Location(loc.Latitude, loc.Longitude));
        }

        _activeMapPolygon = polygon;
        SearchMap.MapElements.Add(_activeMapPolygon);
    }
}
