using System.ComponentModel;
using System.Text.Json;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using RoomToRentRadar.Core.Constants;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Services;
using RoomToRentRadar.Models.MapSearch;
using RoomToRentRadar.ViewModels.MapSearch;
using RoomToRentRadar.Views.Authentication;
#if ANDROID
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Microsoft.Maui.Maps.Handlers;
#endif

namespace RoomToRentRadar.Views.MapSearch;

public partial class MapSearchPage : ContentPage
{
    private readonly DrawingCanvasDrawable _drawable = new();
    private int _draggedAnchorIndex = -1;
    private readonly MapSearchViewModel _viewModel;
    private readonly IAuthenticationSessionService _authenticationSession;
    private Func<Guid, Task>? _pendingSavedAreaAction;
    private bool _managingSavedAreas;
    private bool _openingRoomDetail;
#if ANDROID
    private GoogleMap? _lightMap;
#endif

    /// <summary>
    /// Parameterless constructor for Shell / DataTemplate instantiation with DI service resolution fallback.
    /// </summary>
    public MapSearchPage() : this(
        Application.Current?.Handler?.MauiContext?.Services
        ?? IPlatformApplication.Current?.Services
        ?? throw new InvalidOperationException("Map Search services are unavailable."))
    {
    }

    private MapSearchPage(IServiceProvider services) : this(
        services.GetRequiredService<MapSearchViewModel>(),
        services.GetRequiredService<IAuthenticationSessionService>())
    {
    }

    public MapSearchPage(MapSearchViewModel viewModel, IAuthenticationSessionService authenticationSession)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _authenticationSession = authenticationSession;
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
        SearchMap.PropertyChanged += OnMapPropertyChanged;
        CanvasView.SizeChanged += (_, _) => RefreshBoundary();
        RefreshBoundary();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        var action = _pendingSavedAreaAction;
        _pendingSavedAreaAction = null;
        if (action is not null && args.PreviousPage is AuthPage && _authenticationSession.IsAuthenticated)
            await RunSavedAreaActionAsync(action);
    }

    protected override bool OnBackButtonPressed()
    {
        _viewModel.HandleBack();
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.CancelSearch();
        if (_viewModel.IsInteractingMode)
            _viewModel.CancelDrawing();
    }

    private void OnSearchMapLoaded(object? sender, EventArgs e)
    {
        if (_viewModel.HasDrawnPolygon)
        {
            MoveToSavedBoundary(_viewModel.PolygonCoordinates);
            return;
        }

        var initialSpan = MapSpan.FromCenterAndRadius(
            new Location(MapSearchViewModel.DefaultLatitude, MapSearchViewModel.DefaultLongitude),
            Distance.FromKilometers(MapSearchViewModel.DefaultDistanceKm));
        SearchMap.MoveToRegion(initialSpan);

        _ = InitializeUserLocationAsync();
    }

    private async Task InitializeUserLocationAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status == PermissionStatus.Granted)
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
                var location = await Geolocation.Default.GetLocationAsync(request);
                if (location is not null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var userSpan = MapSpan.FromCenterAndRadius(
                            location,
                            Distance.FromKilometers(MapSearchViewModel.DefaultDistanceKm));
                        if (!_viewModel.HasDrawnPolygon && !_viewModel.IsInteractingMode)
                            SearchMap.MoveToRegion(userSpan);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to retrieve user location: {ex.Message}");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapSearchViewModel.Rooms) or nameof(MapSearchViewModel.IsInteractingMode))
            RefreshRoomPins();

        if (e.PropertyName is nameof(MapSearchViewModel.IsDrawingMode) or
            nameof(MapSearchViewModel.IsEditingMode) or nameof(MapSearchViewModel.HasDrawnPolygon))
        {
            if (_viewModel?.IsEditingMode != true)
            {
                _draggedAnchorIndex = -1;
                _drawable.DraggingAnchorIndex = -1;
            }

            RefreshBoundary();
        }
    }

    private void RefreshRoomPins() =>
        SearchMap.ReplacePins(_viewModel.IsInteractingMode ? [] : _viewModel.Rooms.Select(CreateRoomPin));

    private Pin CreateRoomPin(MapSearchRoom room)
    {
#if ANDROID
        Pin pin = new RoomPricePin(room.PriceTag);
#else
        var pin = new Pin();
#endif
        pin.Type = PinType.SearchResult;
        pin.Label = room.Title;
        pin.Address = room.Address;
        pin.Location = new Location(room.Latitude, room.Longitude);
        pin.MarkerClicked += (_, args) =>
        {
            args.HideInfoWindow = true;
            _viewModel.SelectedRoom = room;
        };
        return pin;
    }

    private void OnClosePreviewClicked(object? sender, EventArgs e) => _viewModel.SelectedRoom = null;

    private async void OnRoomPreviewTapped(object? sender, TappedEventArgs e)
    {
        if (_openingRoomDetail || _viewModel.SelectedRoom is not { } room || Shell.Current is not { } shell)
            return;

        _openingRoomDetail = true;
        try
        {
            await shell.GoToAsync($"{AppRoutes.RoomDetail}?id={Uri.EscapeDataString(room.Id)}");
        }
        finally
        {
            _openingRoomDetail = false;
        }
    }

    private async void OnSaveAreaClicked(object? sender, EventArgs e) =>
        await RunSavedAreaActionAsync(async accountId =>
        {
            var name = await DisplayPromptAsync("Save area", "Name this search area.", "Save", maxLength: 60);
            if (!string.IsNullOrWhiteSpace(name) && CanAccessSavedAreas(accountId) &&
                await _viewModel.SaveCurrentAreaAsync(accountId, name))
            {
                await DisplayAlertAsync("Area saved", name.Trim(), "OK");
            }
        });

    private async void OnSavedAreasClicked(object? sender, EventArgs e) =>
        await RunSavedAreaActionAsync(async accountId =>
        {
            var areas = await _viewModel.GetSavedAreasAsync(accountId);
            if (!CanAccessSavedAreas(accountId))
                return;

            if (areas.Count == 0)
            {
                await DisplayAlertAsync("Saved areas", "Save a confirmed area to search it again later.", "OK");
                return;
            }

            var labels = areas.Select((area, index) => $"{index + 1}. {area.Name}").ToArray();
            var selection = await DisplayActionSheetAsync("Saved areas", "Cancel", null, labels);
            int index = Array.IndexOf(labels, selection);
            if (index < 0 || !CanAccessSavedAreas(accountId))
                return;

            var area = areas[index];
            var action = await DisplayActionSheetAsync(area.Name, "Cancel", "Delete", "Open", "Rename");
            if (!CanAccessSavedAreas(accountId))
                return;

            switch (action)
            {
                case "Open":
                    var search = _viewModel.OpenSavedAreaAsync(area);
                    MoveToSavedBoundary(area.Boundary);
                    await search;
                    break;
                case "Rename":
                    var name = await DisplayPromptAsync("Rename area", "Enter a new name.", "Save",
                        maxLength: 60, initialValue: area.Name);
                    if (!string.IsNullOrWhiteSpace(name) && CanAccessSavedAreas(accountId))
                        await _viewModel.RenameSavedAreaAsync(accountId, area.Id, name);
                    break;
                case "Delete":
                    if (await DisplayAlertAsync("Delete saved area?", area.Name, "Delete", "Cancel") &&
                        CanAccessSavedAreas(accountId))
                        await _viewModel.DeleteSavedAreaAsync(accountId, area.Id);
                    break;
            }
        });

    private bool CanAccessSavedAreas(Guid accountId) =>
        _authenticationSession.IsAuthenticated && _authenticationSession.CurrentUser?.Id == accountId;

    private async Task RunSavedAreaActionAsync(Func<Guid, Task> action)
    {
        if (_managingSavedAreas)
            return;

        _managingSavedAreas = true;
        try
        {
            await _authenticationSession.EnsureInitializedAsync();
            if (_authenticationSession.CurrentUser is not { } user || !CanAccessSavedAreas(user.Id))
            {
                if (Shell.Current is not { } shell)
                    return;

                _pendingSavedAreaAction = action;
                try
                {
                    await shell.GoToAsync(RouteGuardService.BuildLoginRedirectRoute($"//{AppRoutes.MapSearch}"));
                }
                catch
                {
                    _pendingSavedAreaAction = null;
                    throw;
                }
                return;
            }

            await action(user.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            await DisplayAlertAsync("Saved areas", "Couldn’t complete the action. Try again.", "OK");
        }
        finally
        {
            _managingSavedAreas = false;
        }
    }

    private void MoveToSavedBoundary(IReadOnlyList<GeoCoordinate> boundary)
    {
        double south = boundary.Min(point => point.Latitude);
        double north = boundary.Max(point => point.Latitude);
        double west = boundary.Min(point => point.Longitude);
        double east = boundary.Max(point => point.Longitude);
        var center = new Location((south + north) / 2, (west + east) / 2);
        SearchMap.MoveToRegion(new MapSpan(center, (north - south) * 1.5, (east - west) * 1.5));
    }

    private void OnMapPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchMap.VisibleRegion))
        {
#if ANDROID
            if (SearchMap.Handler is MapHandler { Map: { } map } && !ReferenceEquals(map, _lightMap))
            {
                _lightMap = map;
                map.MapColorScheme = MapColorScheme.Light;
            }
#endif
            RefreshBoundary();
        }
    }

    private void RefreshBoundary()
    {
        if (_viewModel is null)
            return;

        _drawable.IsEditingMode = _viewModel.IsEditingMode;
        CanvasView.InputTransparent = !_viewModel.IsInteractingMode;
        CanvasView.IsVisible = _viewModel.IsInteractingMode || _viewModel.HasDrawnPolygon;
        CanvasView.BackgroundColor = _viewModel.IsInteractingMode
            ? Color.FromArgb("#0A2563EB")
            : Colors.Transparent;

        if (!_viewModel.IsDrawingMode)
            _drawable.Clear();

        var boundary = _viewModel.HasDrawnPolygon && !_viewModel.IsDrawingMode
            ? ConvertGeoLocationsToScreenPoints(_viewModel.PolygonCoordinates)
            : new List<PointF>();
        _drawable.SetAnchors(boundary);
        CanvasView.Invalidate();
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
            if (ConvertSingleScreenPointToGeo(pt) is not { } newGeo)
                return;

            _drawable.UpdateAnchor(_draggedAnchorIndex, pt);
            CanvasView.Invalidate();
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
            }
            else
            {
                _viewModel.CompleteDrawing(Enumerable.Empty<GeoCoordinate>());
            }

            _drawable.Clear();
            CanvasView.Invalidate();
        }
    }

#if ANDROID
    private (Projection Projection, MapCanvasTransform Transform)? GetAndroidProjection()
    {
        if (SearchMap.Handler is not MapHandler { Map: { } map, PlatformView: { Width: > 0, Height: > 0 } mapView } ||
            CanvasView.Handler?.PlatformView is not global::Android.Views.View canvas ||
            canvas.Resources?.DisplayMetrics?.Density is not float density || density <= 0f)
            return null;

        var mapOrigin = new int[2];
        var canvasOrigin = new int[2];
        mapView.GetLocationInWindow(mapOrigin);
        canvas.GetLocationInWindow(canvasOrigin);

        return (map.Projection,
            new MapCanvasTransform(density, canvasOrigin[0] - mapOrigin[0], canvasOrigin[1] - mapOrigin[1]));
    }

    private static GeoCoordinate? ProjectCanvasPoint(Projection projection, MapCanvasTransform transform, PointF point)
    {
        var (x, y) = transform.ToMapPixels(point);
        using var pixels = new global::Android.Graphics.Point(x, y);
        using var location = projection.FromScreenLocation(pixels);
        return location is null ? null : new GeoCoordinate(location.Latitude, location.Longitude);
    }

    private List<GeoCoordinate> ConvertScreenPointsToGeoLocations(IReadOnlyList<PointF> screenPoints)
    {
        if (GetAndroidProjection() is not { } native)
            return [];

        using var projection = native.Projection;
        var boundary = new List<GeoCoordinate>(screenPoints.Count);
        foreach (var point in screenPoints)
        {
            if (ProjectCanvasPoint(projection, native.Transform, point) is not { } coordinate)
                return [];

            boundary.Add(coordinate);
        }

        return boundary;
    }

    private GeoCoordinate? ConvertSingleScreenPointToGeo(PointF point)
    {
        if (GetAndroidProjection() is not { } native)
            return null;

        using var projection = native.Projection;
        return ProjectCanvasPoint(projection, native.Transform, point);
    }

    private List<PointF> ConvertGeoLocationsToScreenPoints(IEnumerable<GeoCoordinate> coordinates)
    {
        if (GetAndroidProjection() is not { } native)
            return [];

        using var projection = native.Projection;
        var boundary = new List<PointF>();
        foreach (var coordinate in coordinates)
        {
            using var location = new LatLng(coordinate.Latitude, coordinate.Longitude);
            using var pixels = projection.ToScreenLocation(location);
            if (pixels is null)
                return [];

            boundary.Add(native.Transform.ToCanvasPoint(pixels.X, pixels.Y));
        }

        return boundary;
    }
#else
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

        double topLat = centerLat + (latSpan / 2.0);
        double bottomLat = centerLat - (latSpan / 2.0);
        double topMercatorY = MapSearchViewModel.LatitudeToMercatorY(topLat);
        double bottomMercatorY = MapSearchViewModel.LatitudeToMercatorY(bottomLat);
        double mercatorSpanY = topMercatorY - bottomMercatorY;

        if (Math.Abs(mercatorSpanY) <= 0.0000001)
            return geoPoints;

        foreach (var pt in screenPoints)
        {
            double relX = (pt.X / width) - 0.5;
            double tY = pt.Y / height;

            double mercatorY = topMercatorY - (tY * mercatorSpanY);
            double lat = MapSearchViewModel.MercatorYToLatitude(mercatorY);
            double lon = centerLon + (relX * lonSpan);

            lat = Math.Clamp(lat, -85.0511, 85.0511);
            lon = Math.Clamp(lon, -179.9999, 179.9999);

            geoPoints.Add(new GeoCoordinate(lat, lon));
        }

        return geoPoints;
    }

    private GeoCoordinate? ConvertSingleScreenPointToGeo(PointF pt)
    {
        if (SearchMap.VisibleRegion is null || CanvasView.Width <= 0 || CanvasView.Height <= 0)
            return null;

        var region = SearchMap.VisibleRegion;
        if (region.LatitudeDegrees <= 0.0000001 || region.LongitudeDegrees <= 0.0000001)
            return null;

        double width = CanvasView.Width;
        double height = CanvasView.Height;

        double topLat = region.Center.Latitude + (region.LatitudeDegrees / 2.0);
        double bottomLat = region.Center.Latitude - (region.LatitudeDegrees / 2.0);
        double topMercatorY = MapSearchViewModel.LatitudeToMercatorY(topLat);
        double bottomMercatorY = MapSearchViewModel.LatitudeToMercatorY(bottomLat);
        double mercatorSpanY = topMercatorY - bottomMercatorY;
        if (Math.Abs(mercatorSpanY) <= 0.0000001)
            return null;

        double relX = (pt.X / width) - 0.5;
        double tY = pt.Y / height;

        double mercatorY = topMercatorY - (tY * mercatorSpanY);
        double lat = MapSearchViewModel.MercatorYToLatitude(mercatorY);
        double lon = region.Center.Longitude + (relX * region.LongitudeDegrees);

        lat = Math.Clamp(lat, -85.0511, 85.0511);
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

        double topLat = centerLat + (latSpan / 2.0);
        double bottomLat = centerLat - (latSpan / 2.0);
        double topMercatorY = MapSearchViewModel.LatitudeToMercatorY(topLat);
        double bottomMercatorY = MapSearchViewModel.LatitudeToMercatorY(bottomLat);
        double mercatorSpanY = topMercatorY - bottomMercatorY;

        if (Math.Abs(mercatorSpanY) <= 0.0000001)
            return screenPoints;

        foreach (var c in coords)
        {
            double pointMercatorY = MapSearchViewModel.LatitudeToMercatorY(c.Latitude);
            double tY = (topMercatorY - pointMercatorY) / mercatorSpanY;
            double relX = (c.Longitude - centerLon) / lonSpan;

            float x = (float)((relX + 0.5) * width);
            float y = (float)(tY * height);

            if (!float.IsNaN(x) && !float.IsNaN(y))
            {
                screenPoints.Add(new PointF(x, y));
            }
        }

        return screenPoints;
    }
#endif
}
