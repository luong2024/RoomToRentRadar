using System.ComponentModel;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using RoomToRentRadar.Models.MapSearch;
using RoomToRentRadar.Models.RoomListings;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.ViewModels.MapSearch;
using RoomToRentRadar.ViewModels.RoomListings;
using Map = Microsoft.Maui.Controls.Maps.Map;

namespace RoomToRentRadar.Views.RoomListings;

public partial class ListingLocationPickerPage : ContentPage
{
    private readonly ListingLocationPickerViewModel _viewModel;
    private TaskCompletionSource<ListingLocationPickerResult>? _completion;
    private Map? _map;
    private Pin? _selectionPin;
    private MapSpan? _cameraRegion;
    private bool _mapRendered;
    private bool _popped;

#if ANDROID
    private AndroidMapRenderObserver? _androidObserver;
#elif IOS || MACCATALYST
    private MapKit.MKMapView? _appleMap;
#endif

    public ListingLocationPickerPage(ListingLocationPickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    internal async Task<ListingLocationPickerResult> PickAsync(Page owner, GeoCoordinate? initialLocation)
    {
        if (owner.Window is not { } window)
        {
            return new(ListingLocationPickerOutcome.Failed);
        }

        var navigation = owner.Navigation;
        var completion = new TaskCompletionSource<ListingLocationPickerResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _completion = completion;
        _viewModel.Initialize(initialLocation);
        _viewModel.ClosePickerAsync = CloseAsync;
        window.ModalPopped += OnModalPopped;
        window.Destroying += OnWindowDestroying;

        try
        {
            AttachMap();
            await navigation.PushModalAsync(this);
            var result = await completion.Task;

            // A modal can also be dismissed while Shell is replacing its current page.
            return ReferenceEquals(owner.Window, window) &&
                   window.Page is Shell shell && ReferenceEquals(shell.CurrentPage, owner)
                ? result
                : new(ListingLocationPickerOutcome.Cancelled);
        }
        finally
        {
            window.ModalPopped -= OnModalPopped;
            window.Destroying -= OnWindowDestroying;
            _completion = null;
            _viewModel.EndSession();
            DetachMap();
        }

        async Task<bool> CloseAsync(ListingLocationPickerResult result)
        {
            if (_popped || !ReferenceEquals(navigation.ModalStack.LastOrDefault(), this))
            {
                return false;
            }

            try
            {
                if (ReferenceEquals(await navigation.PopModalAsync(), this))
                {
                    completion.TrySetResult(result);
                    return true;
                }
            }
            catch (Exception)
            {
                if (_popped || !navigation.ModalStack.Contains(this))
                {
                    completion.TrySetResult(new(ListingLocationPickerOutcome.Failed));
                }
            }

            return false;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_completion is null)
        {
            return base.OnBackButtonPressed();
        }

        if (_viewModel.CancelCommand.CanExecute(null))
        {
            _viewModel.CancelCommand.Execute(null);
        }

        return true;
    }

    private void OnModalPopped(object? sender, ModalPoppedEventArgs e)
    {
        if (ReferenceEquals(e.Modal, this))
        {
            _popped = true;
            if (!_viewModel.IsClosing)
            {
                _completion?.TrySetResult(new(ListingLocationPickerOutcome.Cancelled));
            }
        }
    }

    private void OnWindowDestroying(object? sender, EventArgs e) =>
        _completion?.TrySetResult(new(ListingLocationPickerOutcome.Cancelled));

    private void AttachMap()
    {
#if ANDROID
        if (!HasAndroidMapsKey())
        {
            _viewModel.SetMapState(ListingMapState.MissingKey);
            return;
        }
#endif
#if ANDROID || IOS || MACCATALYST
        var center = _viewModel.DraftLocation ??
            new GeoCoordinate(MapSearchViewModel.DefaultLatitude, MapSearchViewModel.DefaultLongitude);
        _cameraRegion = MapSpan.FromCenterAndRadius(new Location(center.Latitude, center.Longitude),
            Distance.FromKilometers(MapSearchViewModel.DefaultDistanceKm));

        _map = new Map { IsShowingUser = false };
        _map.MapClicked += OnMapClicked;
        _map.HandlerChanged += OnMapHandlerChanged;
        _viewModel.PropertyChanged += OnPickerPropertyChanged;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        UpdateMapState();
        RenderSelection();
        MapHost.Content = _map;
#else
        _viewModel.SetMapState(ListingMapState.Unavailable);
#endif
    }

    private void DetachMap()
    {
        if (_map is not { } map)
        {
            return;
        }

        _map = null;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _viewModel.PropertyChanged -= OnPickerPropertyChanged;
        map.MapClicked -= OnMapClicked;
        map.HandlerChanged -= OnMapHandlerChanged;
        DetachRenderObserver();
        MapHost.Content = null;
    }

    private void OnMapHandlerChanged(object? sender, EventArgs e)
    {
        if (_map is not { } map || !ReferenceEquals(sender, map))
        {
            return;
        }

        DetachRenderObserver();
        _mapRendered = false;
        UpdateMapState();

        if (map.Handler is null)
        {
            _cameraRegion = map.VisibleRegion ?? _cameraRegion;
            return;
        }

#if ANDROID
        if (map.Handler.PlatformView is Android.Gms.Maps.MapView androidMap)
        {
            _androidObserver = new AndroidMapRenderObserver(this);
            androidMap.GetMapAsync(_androidObserver);
        }
#elif IOS || MACCATALYST
        if (map.Handler.PlatformView is MapKit.MKMapView appleMap)
        {
            _appleMap = appleMap;
            _appleMap.DidFinishRenderingMap += OnAppleMapRendered;
        }
#endif

        if (_cameraRegion is not null)
        {
            map.MoveToRegion(_cameraRegion);
        }
    }

    private void DetachRenderObserver()
    {
#if ANDROID
        _androidObserver?.Detach();
        _androidObserver = null;
#elif IOS || MACCATALYST
        if (_appleMap is not null)
        {
            _appleMap.DidFinishRenderingMap -= OnAppleMapRendered;
            _appleMap = null;
        }
#endif
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        Dispatcher.Dispatch(UpdateMapState);

    private void UpdateMapState()
    {
        if (_map is not null && _completion is { Task.IsCompleted: false })
        {
            _viewModel.SetMapState(Connectivity.Current.NetworkAccess is not NetworkAccess.Internet
                ? ListingMapState.Offline
                : _mapRendered ? ListingMapState.Ready : ListingMapState.Loading);
        }
    }

    private void OnMapClicked(object? sender, MapClickedEventArgs e)
    {
        if (_map is not null && ReferenceEquals(sender, _map) && _completion is { Task.IsCompleted: false })
        {
            _viewModel.Select(new GeoCoordinate(e.Location.Latitude, e.Location.Longitude));
        }
    }

    private void OnPickerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ListingLocationPickerViewModel.DraftLocation))
        {
            RenderSelection();
        }
    }

    private void RenderSelection()
    {
        if (_map is not { } map)
        {
            return;
        }

        if (_selectionPin is not null)
        {
            map.Pins.Remove(_selectionPin);
            _selectionPin = null;
        }

        if (_viewModel.DraftLocation is { } location)
        {
            _selectionPin = new Pin
            {
                Label = AppResources.GetString("ListingLocation_PinLabel"),
                Location = new Location(location.Latitude, location.Longitude)
            };
            map.Pins.Add(_selectionPin);
        }
    }

#if ANDROID
    private static bool HasAndroidMapsKey()
    {
        var context = Microsoft.Maui.ApplicationModel.Platform.AppContext;
        var manager = context.PackageManager;
        var packageName = context.PackageName;
        if (manager is null || packageName is null)
        {
            return false;
        }

        const Android.Content.PM.PackageInfoFlags metadata = Android.Content.PM.PackageInfoFlags.MetaData;
        var info = OperatingSystem.IsAndroidVersionAtLeast(33)
            ? manager.GetApplicationInfo(packageName, Android.Content.PM.PackageManager.ApplicationInfoFlags.Of((long)metadata))
            : manager.GetApplicationInfo(packageName, metadata);
        var key = info?.MetaData?.GetString("com.google.android.geo.API_KEY");
        return !string.IsNullOrWhiteSpace(key) && key is not ("NO_KEY" or "YOUR-ACTUAL-GOOGLE-MAPS-API-KEY-HERE");
    }

    private sealed class AndroidMapRenderObserver(ListingLocationPickerPage page) :
        Java.Lang.Object, Android.Gms.Maps.IOnMapReadyCallback, Android.Gms.Maps.GoogleMap.IOnMapLoadedCallback
    {
        private readonly WeakReference<ListingLocationPickerPage> _page = new(page);
        private Android.Gms.Maps.GoogleMap? _nativeMap;
        private bool _detached;

        public void OnMapReady(Android.Gms.Maps.GoogleMap googleMap)
        {
            if (!_detached)
            {
                _nativeMap = googleMap;
                googleMap.SetOnMapLoadedCallback(this);
            }
        }

        public void OnMapLoaded()
        {
            if (!_detached && _page.TryGetTarget(out var page) && ReferenceEquals(page._androidObserver, this))
            {
                page._mapRendered = true;
                page.UpdateMapState();
            }
        }

        internal void Detach()
        {
            _detached = true;
            _nativeMap?.SetOnMapLoadedCallback(null);
            _nativeMap = null;
        }
    }
#elif IOS || MACCATALYST
    private void OnAppleMapRendered(object? sender, MapKit.MKDidFinishRenderingMapEventArgs e)
    {
        if (_appleMap is not null && ReferenceEquals(sender, _appleMap) && e.FullyRendered)
        {
            _mapRendered = true;
            UpdateMapState();
        }
    }
#endif
}
