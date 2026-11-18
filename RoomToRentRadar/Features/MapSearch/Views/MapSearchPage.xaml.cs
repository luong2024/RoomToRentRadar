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

    public MapSearchPage(MapSearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        viewModel.OnNavigateBackRequested = async () =>
        {
            // Trở về trang trước đó hoặc về Trang chủ
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync("//home");
            }
        };

        CanvasView.Drawable = _drawable;

        // Định vị vùng khởi tạo trên Map (quanh trường ICTU)
        SearchMap.Loaded += (_, _) =>
        {
            var initialSpan = MapSpan.FromCenterAndRadius(
                new Location(MapSearchViewModel.DefaultLatitude, MapSearchViewModel.DefaultLongitude),
                Distance.FromKilometers(MapSearchViewModel.DefaultDistanceKm));
            SearchMap.MoveToRegion(initialSpan);
        };

        // Lắng nghe thay đổi trạng thái ViewModel
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MapSearchViewModel.IsEditingMode))
            {
                OnEditingModeChanged(viewModel.IsEditingMode);
            }
            else if (e.PropertyName == nameof(MapSearchViewModel.HasDrawnPolygon) && !viewModel.HasDrawnPolygon)
            {
                // Khi Clear polygon
                if (_activeMapPolygon is not null && SearchMap.MapElements.Contains(_activeMapPolygon))
                {
                    SearchMap.MapElements.Remove(_activeMapPolygon);
                    _activeMapPolygon = null;
                }
                _drawable.Clear();
                _drawable.SetAnchors(Enumerable.Empty<PointF>());
                CanvasView.Invalidate();
            }
        };
    }

    private void OnEditingModeChanged(bool isEditing)
    {
        _drawable.IsEditingMode = isEditing;
        if (isEditing && BindingContext is MapSearchViewModel vm)
        {
            // Ẩn polygon map tạm thời, chuyển các đỉnh sang toạ độ màn hình để vẽ chốt neo trên canvas
            if (_activeMapPolygon is not null)
            {
                SearchMap.MapElements.Remove(_activeMapPolygon);
            }

            var screenPoints = ConvertGeoLocationsToScreenPoints(vm.PolygonCoordinates);
            _drawable.SetAnchors(screenPoints);
            CanvasView.Invalidate();
        }
        else if (!isEditing && BindingContext is MapSearchViewModel vmDone)
        {
            // Hoàn tất chỉnh sửa -> render lại polygon trên map
            _drawable.SetAnchors(Enumerable.Empty<PointF>());
            CanvasView.Invalidate();
            RenderPolygonOnMap(vmDone.PolygonCoordinates);
        }
    }

    private void OnCanvasStart(object? sender, TouchEventArgs e)
    {
        var pt = e.Touches.FirstOrDefault();
        if (BindingContext is not MapSearchViewModel vm) return;

        if (vm.IsEditingMode)
        {
            // Kiểm tra xem ngón tay có chạm trúng điểm neo nào không
            _draggedAnchorIndex = _drawable.HitTestAnchor(pt, radius: 36f);
            _drawable.DraggingAnchorIndex = _draggedAnchorIndex;
            CanvasView.Invalidate();
        }
        else if (vm.IsDrawingMode)
        {
            _drawable.Clear();
            _drawable.AddPoint(pt);
            CanvasView.Invalidate();
        }
    }

    private void OnCanvasDrag(object? sender, TouchEventArgs e)
    {
        var pt = e.Touches.FirstOrDefault();
        if (BindingContext is not MapSearchViewModel vm) return;

        if (vm.IsEditingMode && _draggedAnchorIndex >= 0)
        {
            // Kéo điểm neo theo ngón tay
            _drawable.UpdateAnchor(_draggedAnchorIndex, pt);
            CanvasView.Invalidate();

            // Cập nhật toạ độ địa lý tương ứng trong ViewModel
            var newGeo = ConvertSingleScreenPointToGeo(pt);
            vm.UpdateAnchorPosition(_draggedAnchorIndex, newGeo);
        }
        else if (vm.IsDrawingMode)
        {
            _drawable.AddPoint(pt);
            CanvasView.Invalidate();
        }
    }

    private void OnCanvasEnd(object? sender, TouchEventArgs e)
    {
        if (BindingContext is not MapSearchViewModel vm) return;

        if (vm.IsEditingMode)
        {
            _draggedAnchorIndex = -1;
            _drawable.DraggingAnchorIndex = -1;
            CanvasView.Invalidate();
        }
        else if (vm.IsDrawingMode)
        {
            var points = _drawable.GetPoints();
            if (points.Count >= 3)
            {
                var geoLocations = ConvertScreenPointsToGeoLocations(points);
                vm.CompleteDrawing(geoLocations);
                RenderPolygonOnMap(vm.PolygonCoordinates);
            }
            else
            {
                vm.CompleteDrawing(Enumerable.Empty<GeoCoordinate>());
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

        double width = CanvasView.Width;
        double height = CanvasView.Height;

        foreach (var pt in screenPoints)
        {
            double relX = (pt.X / width) - 0.5;
            double relY = 0.5 - (pt.Y / height);

            double lat = centerLat + (relY * latSpan);
            double lon = centerLon + (relX * lonSpan);

            geoPoints.Add(new GeoCoordinate(lat, lon));
        }

        return geoPoints;
    }

    private GeoCoordinate ConvertSingleScreenPointToGeo(PointF pt)
    {
        if (SearchMap.VisibleRegion is null || CanvasView.Width <= 0 || CanvasView.Height <= 0)
            return new GeoCoordinate(MapSearchViewModel.DefaultLatitude, MapSearchViewModel.DefaultLongitude);

        var region = SearchMap.VisibleRegion;
        double relX = (pt.X / CanvasView.Width) - 0.5;
        double relY = 0.5 - (pt.Y / CanvasView.Height);

        double lat = region.Center.Latitude + (relY * region.LatitudeDegrees);
        double lon = region.Center.Longitude + (relX * region.LongitudeDegrees);

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

        double width = CanvasView.Width;
        double height = CanvasView.Height;

        foreach (var c in coords)
        {
            double relY = (c.Latitude - centerLat) / latSpan;
            double relX = (c.Longitude - centerLon) / lonSpan;

            float x = (float)((relX + 0.5) * width);
            float y = (float)((0.5 - relY) * height);

            screenPoints.Add(new PointF(x, y));
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
