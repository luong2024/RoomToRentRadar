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
using RoomToRentRadar.ViewModels;
using RoomToRentRadar.Models;
using MapsuiBrush = Mapsui.Styles.Brush;
using MapsuiColor = Mapsui.Styles.Color;
using NtsPoint = NetTopologySuite.Geometries.Point;

namespace RoomToRentRadar.Views;

public partial class MapSearchPage : ContentPage
{
    private const double PinHitRadiusPixels = 36;

    private static readonly MapsuiColor AccentColor = new(0, 102, 255, 255);
    private static readonly MapsuiColor SelectedPinColor = new(6, 182, 212, 255);
    private static readonly MapsuiColor AccentFill = new(0, 102, 255, 38);
    private static readonly MapsuiColor InkColor = new(11, 21, 38, 255);
    private static readonly MapsuiColor SurfaceColor = new(255, 255, 255, 255);
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    private readonly MapSearchViewModel _model;
    private readonly TileLayer _baseMapLayer;
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

    private const string OfflineNotice = "Bạn đang ngoại tuyến. Bản đồ và kết quả mới cần kết nối Internet; khu vực đã vẽ vẫn được giữ.";
    private const string LocatingNotice = "Đang xác định vị trí của bạn…";
    private CancellationTokenSource? _locationCts;
    private bool _sortSheetOpen;

    private readonly Dictionary<VisualElement, int> _motionVersions = new();
    private bool _motionActive;
    private bool _motionRefreshQueued;
    private bool _previewWasShown;
    private bool _savedWasShown;
    private bool _notificationsWereShown;
    private bool _resultsWereShown;

    public MapSearchPage(MapSearchViewModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;

        var map = MapView.Map;
        // tile.openstreetmap.org is unreachable on some networks; Esri's ArcGIS
        // street basemap needs no key and keeps the pins readable.
        _baseMapLayer = new TileLayer(new HttpTileSource(
            new GlobalSphericalMercator(),
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}",
            name: "Esri World Street Map",
            attribution: new Attribution(
                "Esri, HERE, Garmin, © OpenStreetMap contributors",
                "https://www.esri.com/en-us/legal/terms/full-maps-use-terms"),
            configureHttpRequestMessage: request =>
                request.Headers.UserAgent.ParseAdd("RoomToRentRadar/1.0")));
        map.Layers.Add(_baseMapLayer);
        _baseMapLayer.DataChanged += (_, args) =>
        {
            if (args.Error is not null)
                Dispatcher.Dispatch(() =>
                {
                    if (_observing)
                        _model.MapNotice = "Chưa tải được một phần bản đồ. Kiểm tra kết nối rồi di chuyển bản đồ để thử lại.";
                });
        };
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
        _baseMapLayer.PropertyChanged += OnBaseMapPropertyChanged;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        UpdateConnectivityNotice();
        _model.IsMapLoading = _baseMapLayer.Busy;
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
        _baseMapLayer.PropertyChanged -= OnBaseMapPropertyChanged;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _locationCts?.Cancel();
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
        if (e.PropertyName != nameof(MapSearchViewModel.SelectedListing)
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
                    Outline = new Pen(AccentColor, 2.3)
                });
                features.Add(area);
            }
            else if (coordinates.Length >= 2)
            {
                var line = new GeometryFeature(GeometryFactory.CreateLineString(coordinates));
                line.Styles.Add(new VectorStyle { Line = new Pen(AccentColor, 2.3) });
                features.Add(line);
            }

            var vertexStyle = new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                SymbolScale = 0.42,
                Fill = new MapsuiBrush(AccentColor),
                Outline = new Pen(SurfaceColor, 2)
            };
            for (var index = 0; index < coordinates.Length; index++)
            {
                var vertex = new GeometryFeature(new NtsPoint(coordinates[index]));
                // The latest point is the current drawing focus. Keep its white
                // rim readable on both streets and the translucent blue region.
                vertex.Styles.Add(_model.IsDrawing && index == coordinates.Length - 1
                    ? new SymbolStyle
                    {
                        SymbolType = SymbolType.Ellipse,
                        SymbolScale = 0.62,
                        Fill = new MapsuiBrush(AccentColor),
                        Outline = new Pen(SurfaceColor, 3)
                    }
                    : vertexStyle);
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
            ForeColor = isSelected ? SurfaceColor : InkColor,
            BackColor = new MapsuiBrush(isSelected ? SelectedPinColor : SurfaceColor),
            BorderColor = isSelected ? SurfaceColor : AccentColor,
            BorderThickness = isSelected ? 2.2 : 1.4,
            CornerRounding = 14,
            Font = new Mapsui.Styles.Font { Size = 12.5, Bold = true },
            Halo = null,
            HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
            VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
            CollisionDetection = !isSelected
        });
    }

    private void OnBaseMapPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(_baseMapLayer.Busy))
            Dispatcher.Dispatch(() =>
            {
                if (_observing)
                    _model.IsMapLoading = _baseMapLayer.Busy;
            });
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs args)
        => Dispatcher.Dispatch(UpdateConnectivityNotice);

    private void UpdateConnectivityNotice()
    {
        if (!_observing)
            return;
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            _model.MapNotice = OfflineNotice;
        else if (_model.MapNotice == OfflineNotice)
            _model.MapNotice = "";
    }

    private void OnFocusAreaClicked(object? sender, EventArgs args) => FocusDraft();
    private void OnDismissNoticeClicked(object? sender, EventArgs args) => _model.MapNotice = "";

    private async void OnSortClicked(object? sender, EventArgs args)
    {
        if (_sortSheetOpen)
            return;
        _sortSheetOpen = true;
        try
        {
            var options = _model.SortOptions.Select((option, index) =>
                index == _model.SelectedSortIndex ? $"✓ {option.Label}" : option.Label).ToArray();
            var selected = await DisplayActionSheetAsync("Sắp xếp phòng", "Hủy", null, options);
            var index = Array.IndexOf(options, selected);
            if (_observing && index >= 0)
                _model.SelectedSortIndex = index;
        }
        finally
        {
            _sortSheetOpen = false;
        }
    }

    private async void OnDeleteSavedAreaClicked(object? sender, EventArgs args)
    {
        if (_model.SelectedSavedArea is not { } saved)
            return;
        if (await DisplayAlertAsync("Xóa khu vực đã lưu?", $"Khu vực “{saved.Name}” sẽ bị xóa khỏi danh sách đã lưu.", "Xóa khu vực", "Giữ lại"))
        {
            // The selected row may have changed while the confirmation was open.
            if (_model.SelectedSavedArea?.Id == saved.Id)
                _model.DeleteSavedAreaCommand.Execute(null);
        }
    }

    private async void OnLocateClicked(object? sender, EventArgs args)
    {
        if (_locationCts is not null)
            return;
        using var cts = new CancellationTokenSource();
        _locationCts = cts;
        LocateButton.IsEnabled = false;
        _model.MapNotice = LocatingNotice;
        try
        {
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
                permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (!_observing || cts.IsCancellationRequested)
                return;
            if (permission != PermissionStatus.Granted)
            {
                _model.MapNotice = "Chưa có quyền vị trí. Cấp quyền Vị trí cho Room Radar trong Cài đặt rồi thử lại; bạn vẫn có thể vẽ khu vực.";
                return;
            }

            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(12)), cts.Token);
            if (!_observing || cts.IsCancellationRequested)
                return;
            if (location is null)
            {
                _model.MapNotice = "Chưa xác định được vị trí. Hãy thử lại ở nơi có tín hiệu GPS tốt hơn.";
                return;
            }

            var (x, y) = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
            MapView.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 4);
            _model.MapNotice = "Đã đến vị trí của bạn. Vẽ khu vực quanh đây để tìm phòng.";
        }
        catch (OperationCanceledException) { }
        catch (FeatureNotEnabledException)
        {
            if (_observing)
                _model.MapNotice = "GPS đang tắt. Bật dịch vụ Vị trí trên thiết bị rồi nhấn định vị lại.";
        }
        catch (FeatureNotSupportedException)
        {
            if (_observing)
                _model.MapNotice = "Thiết bị này chưa hỗ trợ định vị. Di chuyển bản đồ và vẽ khu vực bạn muốn tìm.";
        }
        catch (PermissionException)
        {
            if (_observing)
                _model.MapNotice = "Chưa có quyền vị trí. Cấp quyền trong Cài đặt hoặc tiếp tục vẽ khu vực trên bản đồ.";
        }
        catch (Exception)
        {
            if (_observing)
                _model.MapNotice = "Chưa thể lấy vị trí hiện tại. Kiểm tra GPS và kết nối rồi thử lại.";
        }
        finally
        {
            if (_model.MapNotice == LocatingNotice)
                _model.MapNotice = "";
            _locationCts = null;
            LocateButton.IsEnabled = true;
        }
    }

    private void StartMotion()
    {
        if (_motionActive)
            return;

        _motionActive = true;
        _previewWasShown = false;
        _savedWasShown = false;
        _notificationsWereShown = false;
        _resultsWereShown = false;
        _model.PropertyChanged += OnMotionPropertyChanged;
        _ = RevealPanelAsync(HeaderPanel, -6);
        QueueMotionRefresh();
    }

    private void StopMotion()
    {
        _motionActive = false;
        _motionRefreshQueued = false;
        _model.PropertyChanged -= OnMotionPropertyChanged;
        foreach (var panel in new VisualElement[] { HeaderPanel, PreviewPanel, SavedPanel, NotificationsPanel, ResultsPanel })
            ResetPanelMotion(panel);
    }

    private void OnMotionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapSearchViewModel.ShowPreviewCard)
            or nameof(MapSearchViewModel.IsSavedPanelOpen)
            or nameof(MapSearchViewModel.IsNotificationsPanelOpen)
            or nameof(MapSearchViewModel.HasResults))
            QueueMotionRefresh();
    }

    private void QueueMotionRefresh()
    {
        if (!_motionActive || _motionRefreshQueued)
            return;

        _motionRefreshQueued = true;
        // Allow compiled bindings to apply visibility before animating a panel.
        Dispatcher.Dispatch(() =>
        {
            _motionRefreshQueued = false;
            if (!_motionActive)
                return;

            UpdatePanelMotion(PreviewPanel, _model.ShowPreviewCard, ref _previewWasShown);
            UpdatePanelMotion(SavedPanel, _model.IsSavedPanelOpen, ref _savedWasShown);
            UpdatePanelMotion(NotificationsPanel, _model.IsNotificationsPanelOpen, ref _notificationsWereShown);
            UpdatePanelMotion(ResultsPanel,
                _model.HasResults && !_model.ShowPreviewCard && !_model.IsSavedPanelOpen && !_model.IsNotificationsPanelOpen,
                ref _resultsWereShown);
        });
    }

    private void UpdatePanelMotion(VisualElement panel, bool shown, ref bool wasShown)
    {
        if (shown == wasShown)
            return;

        wasShown = shown;
        if (shown)
            _ = RevealPanelAsync(panel, 10);
        else
            ResetPanelMotion(panel);
    }

    private void ResetPanelMotion(VisualElement panel)
    {
        _motionVersions[panel] = _motionVersions.GetValueOrDefault(panel) + 1;
        panel.CancelAnimations();
        panel.Opacity = 1;
        panel.TranslationX = 0;
        panel.TranslationY = 0;
    }

    private async Task RevealPanelAsync(VisualElement panel, double offset)
    {
        ResetPanelMotion(panel);
        if (!_motionActive || !panel.IsVisible || !MotionIsEnabled())
            return;

        var version = _motionVersions[panel];
        panel.Opacity = 0.82;
        panel.TranslationY = offset;
        try
        {
            await Task.WhenAll(
                panel.FadeToAsync(1, 180, Easing.CubicOut),
                panel.TranslateToAsync(0, 0, 180, Easing.CubicOut));
        }
        catch (OperationCanceledException)
        {
            // Closing, reopening or leaving the page supersedes the transition.
        }
        finally
        {
            if (_motionVersions.GetValueOrDefault(panel) == version)
            {
                panel.Opacity = 1;
                panel.TranslationY = 0;
            }
        }
    }

    private static bool MotionIsEnabled()
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            return Android.Animation.ValueAnimator.AreAnimatorsEnabled();
#endif
        return true;
    }
}
