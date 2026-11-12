using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Maui.Controls;
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

        CenterMapOnIctu();
    }

    private void CenterMapOnIctu()
    {
        CampusMap.MoveToRegion(MapSpan.FromCenterAndRadius(IctuCenter, Distance.FromKilometers(1.2)));
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

    private void OnClearPolygonClicked(object? sender, EventArgs e)
    {
        CampusMap.MapElements.Clear();
        _drawable.Clear();
        PolygonCanvas.Invalidate();
    }

    private void OnCenterIctuTapped(object? sender, TappedEventArgs e)
    {
        CenterMapOnIctu();
    }
}