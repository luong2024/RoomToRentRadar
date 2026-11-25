using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Maps;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Pages.Controls;
using RoomToRentRadar.Utilities;
using MapPolygon = Microsoft.Maui.Controls.Maps.Polygon;

namespace RoomToRentRadar.Pages;

public partial class MapSearchPage : ContentPage
{
    private static readonly Location IctuCenter = new(21.5850, 105.8066);
    private readonly MapSearchViewModel _viewModel;
    private readonly PolygonOverlayDrawable _drawable = new();

    public MapSearchPage(MapSearchViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        PolygonCanvas.Drawable = _drawable;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.MapPinsChanged += OnMapPinsChanged;
        _viewModel.SavedAreaPolygonApplied += OnSavedAreaPolygonApplied;
        _viewModel.RoomDetailNavigationRequested += OnRoomDetailNavigationRequested;

        CenterMapOnIctu();
    }

    private void CenterMapOnIctu()
    {
        try
        {
            CampusMap.MoveToRegion(MapSpan.FromCenterAndRadius(IctuCenter, Distance.FromKilometers(1.2)));
        }
        catch
        {
            // Ignore map camera errors when running in headless or unsupported preview contexts
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapSearchViewModel.IsDrawingMode))
        {
            bool drawing = _viewModel.IsDrawingMode;
            // Khóa kéo/zoom Map trên Android khi đang vẽ khu vực
            CampusMap.IsScrollEnabled = !drawing;
            CampusMap.IsZoomEnabled = !drawing;
            PolygonCanvas.InputTransparent = !drawing;
        }
    }

    private void OnPolygonCanvasStartInteraction(object? sender, TouchEventArgs e)
    {
        if (!_viewModel.IsDrawingMode || e.Touches.Length == 0)
            return;

        CampusMap.MapElements.Clear();
        CampusMap.Pins.Clear();
        _drawable.Reset(e.Touches[0]);
        PolygonCanvas.Invalidate();
    }

    private void OnPolygonCanvasDragInteraction(object? sender, TouchEventArgs e)
    {
        if (!_viewModel.IsDrawingMode || e.Touches.Length == 0)
            return;

        if (_drawable.TryAddPoint(e.Touches[0], minDistancePx: 12f))
        {
            PolygonCanvas.Invalidate();
        }
    }

    private async void OnPolygonCanvasEndInteraction(object? sender, TouchEventArgs e)
    {
        if (!_viewModel.IsDrawingMode)
            return;

        var screenPoints = _drawable.Points;
        if (screenPoints.Count < 3 || PolygonCanvas.Width <= 0 || PolygonCanvas.Height <= 0)
        {
            _drawable.Clear();
            PolygonCanvas.Invalidate();
            await _viewModel.ExecuteSpatialSearchAsync([]);
            return;
        }

        var region = CampusMap.VisibleRegion
            ?? MapSpan.FromCenterAndRadius(IctuCenter, Distance.FromKilometers(1.2));

        var vertices = new List<(double Latitude, double Longitude)>(screenPoints.Count);
        var mapPolygon = new MapPolygon
        {
            StrokeColor = Color.FromArgb("#1D4ED8"),
            StrokeWidth = 6,
            FillColor = Color.FromRgba(29, 78, 216, 48)
        };

        foreach (var pt in screenPoints)
        {
            var (lat, lng) = GeoPolygonHelper.ScreenPointToGeo(
                pt.X,
                pt.Y,
                PolygonCanvas.Width,
                PolygonCanvas.Height,
                region.Center.Latitude,
                region.Center.Longitude,
                region.LatitudeDegrees,
                region.LongitudeDegrees);

            vertices.Add((lat, lng));
            mapPolygon.Geopath.Add(new Location(lat, lng));
        }

        var closedGeoJsonRing = GeoPolygonHelper.BuildClosedGeoJsonRing(vertices);

        CampusMap.MapElements.Clear();
        CampusMap.MapElements.Add(mapPolygon);
        _drawable.Clear();
        PolygonCanvas.Invalidate();

        await _viewModel.ExecuteSpatialSearchAsync(closedGeoJsonRing);
    }

    private void OnMapPinsChanged()
    {
        try
        {
            CampusMap.Pins.Clear();
            foreach (var item in _viewModel.MapPins)
            {
                var capturedRoomId = item.Id;
                var mapPin = new Pin
                {
                    Label = $"{item.PriceShortLabel} • {item.Title}",
                    Address = item.Address,
                    Type = PinType.Place,
                    Location = new Location(item.Latitude, item.Longitude)
                };

                mapPin.MarkerClicked += (_, args) =>
                {
                    args.HideInfoWindow = false;
                    _viewModel.SelectPinById(capturedRoomId);
                };

                mapPin.InfoWindowClicked += (_, args) =>
                {
                    args.HideInfoWindow = true;
                    _viewModel.SelectPinById(capturedRoomId);
                    if (_viewModel.OpenSelectedPinDetailCommand.CanExecute(null))
                    {
                        _viewModel.OpenSelectedPinDetailCommand.Execute(null);
                    }
                };

                CampusMap.Pins.Add(mapPin);
            }
        }
        catch
        {
            // Ignore native map pin rendering failures in headless/Windows environments
        }
    }

    private void OnSavedAreaPolygonApplied(IReadOnlyList<double[]> ring)
    {
        if (ring.Count < 4)
            return;

        try
        {
            var mapPolygon = new MapPolygon
            {
                StrokeColor = Color.FromArgb("#512BD4"),
                StrokeWidth = 6,
                FillColor = Color.FromRgba(81, 43, 212, 48)
            };

            double sumLat = 0;
            double sumLng = 0;
            int vertexCount = 0;

            foreach (var coord in ring)
            {
                if (coord.Length < 2)
                    continue;

                var loc = new Location(coord[1], coord[0]);
                mapPolygon.Geopath.Add(loc);
                sumLat += coord[1];
                sumLng += coord[0];
                vertexCount++;
            }

            CampusMap.MapElements.Clear();
            CampusMap.MapElements.Add(mapPolygon);
            _drawable.Clear();
            PolygonCanvas.Invalidate();

            if (vertexCount > 0)
            {
                var center = new Location(sumLat / vertexCount, sumLng / vertexCount);
                CampusMap.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromKilometers(0.9)));
            }
        }
        catch
        {
            // Ignore native map polygon failures in headless/Windows environments
        }
    }

    private async void OnRoomDetailNavigationRequested(MapPinItemModel pin)
    {
        try
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync($"//room-detail?id={Uri.EscapeDataString(pin.Id)}");
            }
        }
        catch
        {
            // Ignore navigation errors when hosted outside Shell
        }
    }

    private void OnClearPolygonClicked(object? sender, EventArgs e)
    {
        CampusMap.MapElements.Clear();
        CampusMap.Pins.Clear();
        _drawable.Clear();
        PolygonCanvas.Invalidate();
    }

    private void OnCenterIctuTapped(object? sender, TappedEventArgs e)
    {
        CenterMapOnIctu();
    }
}
