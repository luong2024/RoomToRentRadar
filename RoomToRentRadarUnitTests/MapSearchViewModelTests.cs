using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Services;
using RoomToRentRadar.Utilities;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class MapSearchViewModelTests
{
    private static List<double[]> CreateIctuBoundingRing()
    {
        var vertices = new List<(double Latitude, double Longitude)>
        {
            (21.5800, 105.8000),
            (21.5900, 105.8000),
            (21.5900, 105.8120),
            (21.5800, 105.8120)
        };

        return GeoPolygonHelper.BuildClosedGeoJsonRing(vertices);
    }

    [Test]
    public void ScreenPointToGeo_CenterPoint_ReturnsMapCenter()
    {
        var (lat, lng) = GeoPolygonHelper.ScreenPointToGeo(
            x: 200,
            y: 300,
            viewWidth: 400,
            viewHeight: 600,
            centerLat: 21.5850,
            centerLng: 105.8066,
            latDegrees: 0.02,
            lngDegrees: 0.02);

        Assert.Multiple(() =>
        {
            Assert.That(lat, Is.EqualTo(21.5850).Within(0.000001));
            Assert.That(lng, Is.EqualTo(105.8066).Within(0.000001));
        });
    }

    [Test]
    public void ScreenPointToGeo_ZeroViewport_FallsBackToCenter()
    {
        var (lat, lng) = GeoPolygonHelper.ScreenPointToGeo(
            x: 50,
            y: 50,
            viewWidth: 0,
            viewHeight: 0,
            centerLat: 21.5850,
            centerLng: 105.8066,
            latDegrees: 0.02,
            lngDegrees: 0.02);

        Assert.Multiple(() =>
        {
            Assert.That(lat, Is.EqualTo(21.5850));
            Assert.That(lng, Is.EqualTo(105.8066));
        });
    }

    [Test]
    public void BuildClosedGeoJsonRing_WithThreeVertices_ReturnsClosedLngLatRing()
    {
        var vertices = new List<(double Latitude, double Longitude)>
        {
            (21.5800, 105.8000),
            (21.5900, 105.8000),
            (21.5900, 105.8120)
        };

        var ring = GeoPolygonHelper.BuildClosedGeoJsonRing(vertices);

        Assert.Multiple(() =>
        {
            Assert.That(ring, Has.Count.EqualTo(4));
            Assert.That(ring[0][0], Is.EqualTo(105.8000));
            Assert.That(ring[0][1], Is.EqualTo(21.5800));
            Assert.That(ring[^1][0], Is.EqualTo(ring[0][0]));
            Assert.That(ring[^1][1], Is.EqualTo(ring[0][1]));
        });
    }

    [Test]
    public void BuildClosedGeoJsonRing_FewerThanThreeVertices_ReturnsEmpty()
    {
        var ring = GeoPolygonHelper.BuildClosedGeoJsonRing(
        [
            (21.5800, 105.8000),
            (21.5900, 105.8120)
        ]);

        Assert.That(ring, Is.Empty);
    }

    [Test]
    public void IsPointInPolygon_InsideAndOutsideIctuBox_ReturnsExpected()
    {
        var ring = CreateIctuBoundingRing();

        Assert.Multiple(() =>
        {
            Assert.That(GeoPolygonHelper.IsPointInPolygon(21.5850, 105.8066, ring), Is.True);
            Assert.That(GeoPolygonHelper.IsPointInPolygon(21.0285, 105.8542, ring), Is.False);
        });
    }

    [Test]
    public void ToggleDrawModeCommand_TogglesDrawingModeAndButtonText()
    {
        var vm = new MapSearchViewModel(new MockSpatialSearchService());

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsDrawingMode, Is.False);
            Assert.That(vm.DrawToggleButtonText, Is.EqualTo("Vẽ khu vực"));
        });

        vm.ToggleDrawModeCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsDrawingMode, Is.True);
            Assert.That(vm.DrawToggleButtonText, Is.EqualTo("Đang vẽ..."));
        });

        vm.ToggleDrawModeCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsDrawingMode, Is.False);
            Assert.That(vm.DrawToggleButtonText, Is.EqualTo("Vẽ khu vực"));
        });
    }

    [Test]
    public async Task ExecuteSpatialSearchAsync_ValidIctuPolygon_LoadsMatchedRoomsAndUpdatesState()
    {
        var vm = new MapSearchViewModel(new MockSpatialSearchService());
        var ring = CreateIctuBoundingRing();

        await vm.ExecuteSpatialSearchAsync(ring);

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasPolygon, Is.True);
            Assert.That(vm.IsDrawingMode, Is.False);
            Assert.That(vm.IsSearching, Is.False);
            Assert.That(vm.HasError, Is.False);
            Assert.That(vm.ResultCount, Is.EqualTo(8));
            Assert.That(vm.MatchedRooms, Has.Count.EqualTo(8));
            Assert.That(vm.CurrentPolygonGeoJson, Has.Count.EqualTo(5));
        });
    }

    [Test]
    public async Task ExecuteSpatialSearchAsync_TooFewPoints_ResetsDrawingModeWithoutSearching()
    {
        var vm = new MapSearchViewModel(new MockSpatialSearchService());
        vm.ToggleDrawModeCommand.Execute(null);

        await vm.ExecuteSpatialSearchAsync(
        [
            [105.8066, 21.5850],
            [105.8070, 21.5860]
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsDrawingMode, Is.False);
            Assert.That(vm.HasPolygon, Is.False);
            Assert.That(vm.MatchedRooms, Is.Empty);
            Assert.That(vm.StatusMessage, Does.Contain("ít nhất 3 điểm"));
        });
    }

    [Test]
    public async Task ClearPolygonCommand_AfterSearch_ResetsPolygonAndRooms()
    {
        var vm = new MapSearchViewModel(new MockSpatialSearchService());
        await vm.ExecuteSpatialSearchAsync(CreateIctuBoundingRing());

        vm.ClearPolygonCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasPolygon, Is.False);
            Assert.That(vm.IsDrawingMode, Is.False);
            Assert.That(vm.ResultCount, Is.EqualTo(0));
            Assert.That(vm.MatchedRooms, Is.Empty);
            Assert.That(vm.CurrentPolygonGeoJson, Is.Empty);
        });
    }

    [Test]
    public async Task ExecuteSpatialSearchAsync_WhenServiceFails_SetsErrorState()
    {
        var vm = new MapSearchViewModel(new FailingSpatialSearchService());

        await vm.ExecuteSpatialSearchAsync(CreateIctuBoundingRing());

        Assert.Multiple(() =>
        {
            Assert.That(vm.HasError, Is.True);
            Assert.That(vm.IsSearching, Is.False);
            Assert.That(vm.ErrorMessage, Does.Contain("Network timeout"));
        });
    }

    private sealed class FailingSpatialSearchService : ISpatialSearchService
    {
        public Task<SpatialSearchResponse> SearchInPolygonAsync(
            IReadOnlyList<double[]> closedGeoJsonRing,
            string sortBy = "distance",
            CancellationToken cancellationToken = default)
        {
            throw new HttpRequestException("Network timeout");
        }
    }
}