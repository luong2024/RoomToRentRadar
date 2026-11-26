using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using Microsoft.Maui.Controls.Shapes;
using RoomToRentRadar.Models.RoomDetails;
using RoomToRentRadar.ViewModels.RoomDetails;
using System.ComponentModel;
#if ANDROID || IOS || MACCATALYST
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
#endif

namespace RoomToRentRadar.Views.RoomDetails;

public partial class RoomDetailPage : ContentPage
{
#if ANDROID || IOS || MACCATALYST
    private readonly Microsoft.Maui.Controls.Maps.Map _destinationMap = new();
    private Pin? _destinationPin;
    private int _searchVersion;
#endif
    public RoomDetailPage(RoomDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.ReportDialogHandler = OpenReportDialogAsync;
#if ANDROID || IOS || MACCATALYST
        DestinationMapHost.Content = _destinationMap;
        _destinationMap.MapClicked += OnDestinationMapClicked;
        _destinationMap.Loaded += (_, _) => ShowRoomOnMap();
#else
        DestinationMapHost.IsVisible = false;
        DestinationSearch.IsVisible = false;
        MapStatus.Text = "Map selection is not supported on this platform.";
#endif
    }

    private async Task<ReportListingDialogResult?> OpenReportDialogAsync(string roomId, string roomTitle)
    {
        var availableWidth = Width > 0 ? Width : 640;
        var availableHeight = Height > 0 ? Height : 680;
        var popupWidth = Math.Clamp(availableWidth * 0.90, 280, 580);
        var popupMaxHeight = Math.Clamp(availableHeight * 0.86, 300, 620);
        var columnCount = popupWidth >= 460 ? 2 : 1;

        var popup = new ReportListingPopup(roomTitle, popupWidth, popupMaxHeight, columnCount);
        var options = new PopupOptions
        {
            Shape = new RoundRectangle { CornerRadius = new CornerRadius(20) },
            PageOverlayColor = Color.FromArgb("#80000000")
        };
        _ = this.ShowPopupAsync(popup, options);
        return await popup.WaitForResultAsync();
    }

    protected override void OnDisappearing()
    {
#if ANDROID || IOS || MACCATALYST
        _searchVersion++;
        if (BindingContext is RoomDetailViewModel vm) vm.PropertyChanged -= OnViewModelPropertyChanged;
#endif
        (BindingContext as RoomDetailViewModel)?.PauseTravelInfo();
        base.OnDisappearing();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is RoomDetailViewModel viewModel)
        {
#if ANDROID || IOS || MACCATALYST
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ShowRoomOnMap();
#endif
            viewModel.ResumeTravelInfo();
            if (viewModel.Room is not null) _ = viewModel.LoadTravelInfoAsync();
        }
    }

    private async void OnDestinationSearch(object? sender, EventArgs e)
    {
#if ANDROID || IOS || MACCATALYST
        var search = DestinationSearch.Text?.Trim();
        if (string.IsNullOrEmpty(search)) return;
        var version = ++_searchVersion;
        MapStatus.Text = "Searching...";
        try
        {
            var locations = await Geocoding.Default.GetLocationsAsync(search);
            if (version != _searchVersion) return;
            var location = locations?.FirstOrDefault();
            if (location is null)
            {
                MapStatus.Text = "No matching place found. Move the map and tap a location.";
                return;
            }
            _destinationMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(1)));
            MapStatus.Text = "Tap the map to select your destination.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Destination search failed: {ex.Message}");
            if (version == _searchVersion) MapStatus.Text = "Search unavailable. Move the map and tap a location.";
        }
#else
        await Task.CompletedTask;
#endif
    }

#if ANDROID || IOS || MACCATALYST
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomDetailViewModel.Room))
        {
            _searchVersion++;
            ShowRoomOnMap();
        }
    }

    private void ShowRoomOnMap()
    {
        if (BindingContext is not RoomDetailViewModel { Room: { } room } ||
            room.Latitude is not double lat || room.Longitude is not double lon ||
            !double.IsFinite(lat) || !double.IsFinite(lon) || lat is < -90 or > 90 || lon is < -180 or > 180)
        {
            _destinationMap.Pins.Clear();
            _destinationPin = null;
            MapStatus.Text = "Room location unavailable.";
            return;
        }

        var location = new Location(lat, lon);
        _destinationMap.Pins.Clear();
        _destinationMap.Pins.Add(new Pin { Label = "Room", Location = location });
        if (_destinationPin is not null) _destinationMap.Pins.Add(_destinationPin);
        _destinationMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromKilometers(2)));
        MapStatus.Text = "Tap the map to select your destination.";
    }

    private async void OnDestinationMapClicked(object? sender, MapClickedEventArgs e)
    {
        if (BindingContext is not RoomDetailViewModel vm || vm.Room is null) return;
        _searchVersion++;
        if (_destinationPin is not null) _destinationMap.Pins.Remove(_destinationPin);
        _destinationPin = new Pin { Label = "Destination", Location = e.Location };
        _destinationMap.Pins.Add(_destinationPin);
        MapStatus.Text = "Destination selected.";
        await vm.SelectDestinationAsync(e.Location.Latitude, e.Location.Longitude);
    }
#endif
}
