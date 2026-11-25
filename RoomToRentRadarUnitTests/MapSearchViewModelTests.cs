using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using RoomToRentRadar.Models.Contracts;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Services;
using RoomToRentRadar.Utilities;
using NUnit.Framework;

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

    [Test]
    public async Task ExecuteSpatialSearchAsync_ValidIctuPolygon_PopulatesMapPinsAndAutoSelectsFirstPinForPreviewCard()
    {
        var vm = new MapSearchViewModel(new MockSpatialSearchService());
        int pinChangedEvents = 0;
        vm.MapPinsChanged += () => pinChangedEvents++;

        await vm.ExecuteSpatialSearchAsync(CreateIctuBoundingRing());

        Assert.Multiple(() =>
        {
            Assert.That(vm.MapPins, Has.Count.EqualTo(8));
            Assert.That(vm.HasMatchedPins, Is.True);
            Assert.That(vm.HasSelectedPin, Is.True);
            Assert.That(vm.SelectedPin, Is.SameAs(vm.MapPins[0]));
            Assert.That(vm.SelectedPin!.IsSelected, Is.True);
            Assert.That(vm.SelectedPinPositionText, Is.EqualTo("Ghim 1/8"));
            Assert.That(vm.CanSaveCurrentArea, Is.True);
            Assert.That(pinChangedEvents, Is.GreaterThanOrEqualTo(1));
        });
    }

    [Test]
    public async Task SelectPinCommand_AndSelectPinById_SwitchSelectedPinAndHighlightState()
    {
        var vm = new MapSearchViewModel(new MockSpatialSearchService());
        await vm.ExecuteSpatialSearchAsync(CreateIctuBoundingRing());
        var thirdPin = vm.MapPins[2];

        vm.SelectPinCommand.Execute(thirdPin);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedPin, Is.SameAs(thirdPin));
            Assert.That(vm.MapPins[0].IsSelected, Is.False);
            Assert.That(thirdPin.IsSelected, Is.True);
            Assert.That(thirdPin.PinBackgroundHex, Is.EqualTo("#512BD4"));
            Assert.That(vm.SelectedPinPositionText, Is.EqualTo("Ghim 3/8"));
        });

        vm.SelectPinById(vm.MapPins[4].Id);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedPin, Is.SameAs(vm.MapPins[4]));
            Assert.That(thirdPin.IsSelected, Is.False);
            Assert.That(vm.MapPins[4].IsSelected, Is.True);
            Assert.That(vm.SelectedPinPositionText, Is.EqualTo("Ghim 5/8"));
        });
    }

    [Test]
    public async Task SelectNextAndPreviousPinCommands_CycleThroughPinsAndClosePreviewClearsSelection()
    {
        var vm = new MapSearchViewModel(new MockSpatialSearchService());
        await vm.ExecuteSpatialSearchAsync(CreateIctuBoundingRing());

        vm.SelectNextPinCommand.Execute(null);
        Assert.That(vm.SelectedPin, Is.SameAs(vm.MapPins[1]));

        vm.SelectPreviousPinCommand.Execute(null);
        vm.SelectPreviousPinCommand.Execute(null);
        Assert.That(vm.SelectedPin, Is.SameAs(vm.MapPins[^1]));

        vm.ClosePinPreviewCommand.Execute(null);
        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedPin, Is.Null);
            Assert.That(vm.HasSelectedPin, Is.False);
            Assert.That(vm.SelectedPinPositionText, Is.Empty);
        });
    }

    [Test]
    public async Task OpenSelectedPinDetailCommand_SetsLastOpenedRoomDetailIdAndFiresEvent()
    {
        var vm = new MapSearchViewModel(new MockSpatialSearchService());
        MapPinItemModel? navigatedPin = null;
        vm.RoomDetailNavigationRequested += pin => navigatedPin = pin;
        await vm.ExecuteSpatialSearchAsync(CreateIctuBoundingRing());
        var targetPin = vm.MapPins[1];
        vm.SelectPinCommand.Execute(targetPin);

        vm.OpenSelectedPinDetailCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.LastOpenedRoomDetailId, Is.EqualTo(targetPin.Id));
            Assert.That(navigatedPin, Is.SameAs(targetPin));
            Assert.That(vm.StatusMessage, Does.Contain(targetPin.Title));
        });
    }

    [Test]
    public async Task SaveCurrentAreaCommand_WithActivePolygon_AddsNewSavedAreaToTopOfList()
    {
        var vm = new MapSearchViewModel(new MockSpatialSearchService());
        int initialCount = vm.SavedAreas.Count;
        await vm.ExecuteSpatialSearchAsync(CreateIctuBoundingRing());
        vm.SavedAreaNameInput = "Khu vực Z115 yêu thích";

        vm.SaveCurrentAreaCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SavedAreas, Has.Count.EqualTo(initialCount + 1));
            Assert.That(vm.SavedAreas[0].Name, Is.EqualTo("Khu vực Z115 yêu thích"));
            Assert.That(vm.SavedAreas[0].MatchedRoomCount, Is.EqualTo(8));
            Assert.That(vm.SavedAreas[0].IsActive, Is.True);
            Assert.That(vm.SavedAreaNameInput, Is.Empty);
        });
    }

    [Test]
    public void SaveCurrentAreaCommand_WithoutActivePolygon_DoesNotAddSavedArea()
    {
        var vm = new MapSearchViewModel(new MockSpatialSearchService());
        int initialCount = vm.SavedAreas.Count;
        vm.SavedAreaNameInput = "Không hợp lệ";

        vm.SaveCurrentAreaCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(vm.CanSaveCurrentArea, Is.False);
            Assert.That(vm.SavedAreas, Has.Count.EqualTo(initialCount));
            Assert.That(vm.StatusMessage, Does.Contain("trước khi lưu"));
        });
    }

    [Test]
    public async Task ApplySavedAreaCommand_LoadsSavedPolygonPinsAndMarksAreaActive()
    {
        var vm = new MapSearchViewModel(new MockSpatialSearchService());
        IReadOnlyList<double[]>? appliedRing = null;
        vm.SavedAreaPolygonApplied += ring => appliedRing = ring;
        var targetArea = vm.SavedAreas[0];

        await vm.ApplySavedAreaCommand.ExecuteAsync(targetArea);

        Assert.Multiple(() =>
        {
            Assert.That(targetArea.IsActive, Is.True);
            Assert.That(vm.HasPolygon, Is.True);
            Assert.That(vm.MatchedRooms, Is.Not.Empty);
            Assert.That(vm.MapPins, Has.Count.EqualTo(vm.MatchedRooms.Count));
            Assert.That(vm.HasSelectedPin, Is.True);
            Assert.That(appliedRing, Is.SameAs(targetArea.PolygonGeoJson));
            Assert.That(vm.StatusMessage, Does.Contain(targetArea.Name));
        });
    }

    [Test]
    public void RemoveSavedAreaCommand_RemovesTargetSavedArea()
    {
        var vm = new MapSearchViewModel(new MockSpatialSearchService());
        var areaToRemove = vm.SavedAreas[0];
        int initialCount = vm.SavedAreas.Count;

        vm.RemoveSavedAreaCommand.Execute(areaToRemove);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SavedAreas, Has.Count.EqualTo(initialCount - 1));
            Assert.That(vm.SavedAreas, Does.Not.Contain(areaToRemove));
            Assert.That(vm.StatusMessage, Does.Contain(areaToRemove.Name));
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