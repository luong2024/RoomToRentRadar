using NUnit.Framework;
using RoomToRentRadar.Models.MapSearch;
using RoomToRentRadar.ViewModels.MapSearch;

namespace RoomToRentRadarUnitTests.Features.MapSearch;

[TestFixture]
public class MapSearchViewModelTests
{
    private MapSearchViewModel _viewModel = null!;

    [TestCase(.01, true)]
    [TestCase(1.0, true)]
    [TestCase(.000102, false)]
    public void CompleteDrawing_WithDenseCircle_EnforcesEditAnchorLimit(double radius, bool accepted)
    {
        var points = Enumerable.Range(0, 180).Select(i =>
        {
            double angle = i * 2 * Math.PI / 180;
            return new GeoCoordinate(radius * Math.Sin(angle), radius * Math.Cos(angle));
        });

        _viewModel.CompleteDrawing(points);
        _viewModel.StartEditing();

        Assert.That(_viewModel.HasDrawnPolygon, Is.EqualTo(accepted));
        if (accepted)
            Assert.That(_viewModel.EditAnchorPins.Count, Is.InRange(3, 10));
        else
        {
            Assert.That(_viewModel.IsDrawingMode, Is.True);
            Assert.That(_viewModel.PolygonCoordinates, Is.Empty);
            Assert.That(_viewModel.StatusMessage, Does.Contain("fewer turns"));
        }
    }

    [Test]
    public async Task HandleBack_DuringInvalidEdit_RestoresConfirmedBoundary()
    {
        GeoCoordinate[] original = [new(0, 0), new(0, .01), new(.01, 0)];
        _viewModel.AlertHandler = (_, _, _) => Task.CompletedTask;
        _viewModel.CompleteDrawing(original);
        await _viewModel.ConfirmAreaAsync();
        _viewModel.StartEditing();
        _viewModel.UpdateAnchorPosition(1, original[0]);

        _viewModel.FinishEditing();
        Assert.That(_viewModel.IsEditingMode, Is.True);
        _viewModel.HandleBack();

        Assert.That(_viewModel.IsEditingMode, Is.False);
        Assert.That(_viewModel.IsAreaConfirmed, Is.True);
        Assert.That(_viewModel.PolygonCoordinates, Is.EqualTo(original));
        Assert.That(_viewModel.CurrentArea.Vertices, Is.EqualTo(original));
    }

    [Test]
    public async Task CollapsedBoundary_StaysEditableAndCannotBeConfirmed()
    {
        _viewModel.CompleteDrawing(new List<GeoCoordinate> { new(0, 0), new(0, .01), new(.01, 0) });
        _viewModel.StartEditing();
        _viewModel.UpdateAnchorPosition(1, _viewModel.PolygonCoordinates[0]);

        _viewModel.FinishEditing();
        await _viewModel.ConfirmAreaAsync();

        Assert.That(_viewModel.IsEditingMode, Is.True);
        Assert.That(_viewModel.IsAreaConfirmed, Is.False);
        _viewModel.UpdateAnchorPosition(1, new(0, .01));
        _viewModel.FinishEditing();
        Assert.That(_viewModel.IsEditingMode, Is.False);
    }

    [Test]
    public void CompleteDrawing_ShortOvershootAtStart_KeepsMainLoop()
    {
        _viewModel.CompleteDrawing(new List<GeoCoordinate>
        {
            new(0, 0), new(0, .01), new(.01, .01), new(.01, 0),
            new(-.0001, .0001)
        });

        Assert.That(_viewModel.HasDrawnPolygon, Is.True);
        Assert.That(_viewModel.VertexCount, Is.EqualTo(4));
    }

    [Test]
    public void IsSelfIntersecting_SegmentsSeparatedBySmallGap_ReturnsFalse()
    {
        GeoCoordinate[] points =
        {
            new(0, 0), new(0, .000003), new(.000020, .000015),
            new(.000020, .0000025), new(.00000025, .0000015), new(.000010, .0000015)
        };
        Assert.That(MapSearchViewModel.IsSelfIntersecting(points), Is.False);
    }

    [Test]
    public void CompleteDrawing_OpenNarrowRectangle_PreservesValidAreaAfterSimplification()
    {
        var points = Enumerable.Range(0, 15)
            .Select(i => new GeoCoordinate(0, .0009 * i / 14)).ToList();
        points.Add(new GeoCoordinate(.000045, .0009));
        points.Add(new GeoCoordinate(.000045, 0));
        _viewModel.CompleteDrawing(points);

        Assert.That(_viewModel.HasDrawnPolygon, Is.True);
        Assert.That(_viewModel.VertexCount, Is.LessThanOrEqualTo(10));
        Assert.That(MapSearchViewModel.CalculatePolygonAreaSquareMeters(_viewModel.PolygonCoordinates),
            Is.GreaterThanOrEqualTo(400));
    }
    [Test]
    public void FailedStroke_AllowsImmediateRetryAndClearRemovesBoundary()
    {
        _viewModel.StartDrawing();
        _viewModel.CompleteDrawing(Array.Empty<GeoCoordinate>());
        Assert.That(_viewModel.IsDrawingMode, Is.True);
        Assert.That(_viewModel.IsInteractingMode, Is.True);

        // An eight with a sampled vertex exactly at the shared junction.
        _viewModel.CompleteDrawing(new List<GeoCoordinate>
        {
            new(0, 0), new(.01, .01), new(.01, -.01), new(0, 0),
            new(-.01, .01), new(-.01, -.01)
        });
        Assert.That(_viewModel.IsDrawingMode, Is.False);
        Assert.That(MapSearchViewModel.IsSelfIntersecting(_viewModel.PolygonCoordinates), Is.True);
        _viewModel.ClearPolygon();
        Assert.That(_viewModel.PolygonCoordinates, Is.Empty);
    }
    [SetUp]
    public void SetUp()
    {
        _viewModel = new MapSearchViewModel();
    }

    [Test]
    public void StartDrawing_ShouldEnableDrawingModeAndSetMessage()
    {
        // Act
        _viewModel.StartDrawingCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.IsDrawingMode, Is.True);
        Assert.That(_viewModel.CanStartDrawing, Is.False);
        Assert.That(_viewModel.BackButtonText, Is.EqualTo("Back"));
        Assert.That(_viewModel.StatusMessage, Does.Contain("Draw a freehand loop"));
    }

    [Test]
    public void CancelDrawing_WhenNotDrawn_ShouldDisableDrawingModeAndResetStatus()
    {
        // Arrange
        _viewModel.StartDrawingCommand.Execute(null);

        // Act
        _viewModel.CancelDrawingCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.IsDrawingMode, Is.False);
        Assert.That(_viewModel.HasDrawnPolygon, Is.False);
        Assert.That(_viewModel.BackButtonText, Is.EqualTo("Home"));
    }

    [Test]
    public void CompleteDrawing_WithValidPolygon_ShouldStoreCoordinatesAndEnableHasDrawnPolygon()
    {
        // Arrange: triangle with realistic area around Thai Nguyen
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5950, 105.8200),
            new(21.5750, 105.8150),
            new(21.5855, 105.8075)
        };

        _viewModel.StartDrawing();

        // Act
        _viewModel.CompleteDrawing(testPoints);

        // Assert
        Assert.That(_viewModel.IsDrawingMode, Is.False);
        Assert.That(_viewModel.HasDrawnPolygon, Is.True);
        Assert.That(_viewModel.VertexCount, Is.GreaterThanOrEqualTo(3));
        Assert.That(_viewModel.PolygonCoordinates.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(_viewModel.CurrentArea.IsValid, Is.True);
        Assert.That(_viewModel.BackButtonText, Is.EqualTo("Home"));
        Assert.That(_viewModel.ShowCompletedToolbar, Is.True);
    }

    [Test]
    public void CompleteDrawing_WithLessThanThreePoints_ShouldNotCreatePolygon()
    {
        // Arrange
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5890, 105.8110)
        };

        // Act
        _viewModel.CompleteDrawing(testPoints);

        // Assert
        Assert.That(_viewModel.HasDrawnPolygon, Is.False);
        Assert.That(_viewModel.VertexCount, Is.EqualTo(0));
        Assert.That(_viewModel.CurrentArea.IsValid, Is.False);
        Assert.That(_viewModel.ShowCompletedToolbar, Is.False);
    }

    [Test]
    public void CompleteDrawing_WithFlatOrTinyArea_ShouldReject()
    {
        // Arrange: collinear points (straight line, zero area)
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5850, 105.8070),
            new(21.5851, 105.8071),
            new(21.5852, 105.8072)
        };

        // Act
        _viewModel.CompleteDrawing(testPoints);

        // Assert
        Assert.That(_viewModel.HasDrawnPolygon, Is.False);
        Assert.That(_viewModel.StatusMessage, Does.Contain("flat or small"));
    }

    [Test]
    public void ClearPolygon_ShouldResetAllDrawingState()
    {
        // Arrange
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5950, 105.8200),
            new(21.5750, 105.8150),
            new(21.5855, 105.8075)
        };
        _viewModel.CompleteDrawing(testPoints);

        // Act
        _viewModel.ClearPolygonCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.HasDrawnPolygon, Is.False);
        Assert.That(_viewModel.PolygonCoordinates, Is.Empty);
        Assert.That(_viewModel.CurrentArea.Vertices, Is.Empty);
        Assert.That(_viewModel.EditAnchorPins, Is.Empty);
        Assert.That(_viewModel.VertexCount, Is.EqualTo(0));
        Assert.That(_viewModel.CanStartDrawing, Is.True);
    }

    [Test]
    public void DrawAgain_ShouldClearExistingAndStartDrawingMode()
    {
        // Arrange
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5950, 105.8200),
            new(21.5750, 105.8150),
            new(21.5855, 105.8075)
        };
        _viewModel.CompleteDrawing(testPoints);

        // Act
        _viewModel.DrawAgainCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.IsDrawingMode, Is.True);
        Assert.That(_viewModel.HasDrawnPolygon, Is.False);
        Assert.That(_viewModel.PolygonCoordinates, Is.Empty);
        Assert.That(_viewModel.BackButtonText, Is.EqualTo("Back"));
    }

    [Test]
    public void StartEditing_WhenHasPolygon_ShouldPopulateAnchorsAndSetEditingMode()
    {
        // Arrange
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5950, 105.8200),
            new(21.5750, 105.8150),
            new(21.5855, 105.8075)
        };
        _viewModel.CompleteDrawing(testPoints);

        // Act
        _viewModel.StartEditingCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.IsEditingMode, Is.True);
        Assert.That(_viewModel.EditAnchorPins.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(_viewModel.BackButtonText, Is.EqualTo("Back"));
        Assert.That(_viewModel.StatusMessage, Does.Contain("Drag pins"));
    }

    [Test]
    public void FinishEditing_ShouldTurnOffEditingModeAndKeepPolygon()
    {
        // Arrange
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5950, 105.8200),
            new(21.5750, 105.8150),
            new(21.5855, 105.8075)
        };
        _viewModel.CompleteDrawing(testPoints);
        _viewModel.StartEditing();

        // Act
        _viewModel.FinishEditingCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.IsEditingMode, Is.False);
        Assert.That(_viewModel.HasDrawnPolygon, Is.True);
        Assert.That(_viewModel.BackButtonText, Is.EqualTo("Home"));
        Assert.That(_viewModel.ShowCompletedToolbar, Is.True);
    }

    [Test]
    public void UpdateAnchorPosition_ShouldUpdatePolygonCoordinatesAndAreaVertices()
    {
        // Arrange
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5950, 105.8200),
            new(21.5750, 105.8150),
            new(21.5855, 105.8075)
        };
        _viewModel.CompleteDrawing(testPoints);
        _viewModel.StartEditing();

        var updatedCoord = new GeoCoordinate(21.5900, 105.8150);

        // Act
        _viewModel.UpdateAnchorPosition(1, updatedCoord);

        // Assert
        Assert.That(_viewModel.PolygonCoordinates[1], Is.EqualTo(updatedCoord));
        Assert.That(_viewModel.CurrentArea.Vertices[1], Is.EqualTo(updatedCoord));
        Assert.That(_viewModel.EditAnchorPins[1].Latitude, Is.EqualTo(updatedCoord.Latitude));
        Assert.That(_viewModel.EditAnchorPins[1].Longitude, Is.EqualTo(updatedCoord.Longitude));
    }

    [Test]
    public void HandleBack_WhenDrawing_ShouldExitDrawingModeOnly()
    {
        // Arrange
        bool navigationInvoked = false;
        _viewModel.OnNavigateBackRequested = () => navigationInvoked = true;
        _viewModel.StartDrawing();

        // Act
        _viewModel.HandleBackCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.IsDrawingMode, Is.False);
        Assert.That(navigationInvoked, Is.False);
    }

    [Test]
    public void HandleBack_WhenEditing_ShouldExitEditingModeOnly()
    {
        // Arrange
        bool navigationInvoked = false;
        _viewModel.OnNavigateBackRequested = () => navigationInvoked = true;
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5950, 105.8200),
            new(21.5750, 105.8150),
            new(21.5855, 105.8075)
        };
        _viewModel.CompleteDrawing(testPoints);
        _viewModel.StartEditing();

        // Act
        _viewModel.HandleBackCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.IsEditingMode, Is.False);
        Assert.That(navigationInvoked, Is.False);
    }

    [Test]
    public void HandleBack_WhenIdle_ShouldInvokeNavigationCallback()
    {
        // Arrange
        bool navigationInvoked = false;
        _viewModel.OnNavigateBackRequested = () => navigationInvoked = true;

        // Act
        _viewModel.HandleBackCommand.Execute(null);

        // Assert
        Assert.That(navigationInvoked, Is.True);
    }

    [Test]
    public async Task ViewPropertiesAsync_WhenPolygonValid_ShouldTriggerAlertAndReportStatus()
    {
        // Arrange
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5950, 105.8200),
            new(21.5750, 105.8150),
            new(21.5855, 105.8075)
        };
        _viewModel.CompleteDrawing(testPoints);

        bool alertCalled = false;
        _viewModel.AlertHandler = (title, message, cancel) =>
        {
            alertCalled = true;
            Assert.That(title, Does.Contain("Confirmed"));
            Assert.That(message, Does.Contain("T05.2"));
            return Task.CompletedTask;
        };

        // Act
        await _viewModel.ViewPropertiesCommand.ExecuteAsync(null);

        // Assert
        Assert.That(alertCalled, Is.True);
        Assert.That(_viewModel.StatusMessage, Does.Contain("confirmed"));
    }

    [Test]
    public async Task ViewPropertiesAsync_WhenNoPolygon_ShouldRejectWithWarning()
    {
        // Arrange
        bool alertCalled = false;
        _viewModel.AlertHandler = (_, _, _) =>
        {
            alertCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await _viewModel.ViewPropertiesCommand.ExecuteAsync(null);

        // Assert
        Assert.That(alertCalled, Is.False);
        Assert.That(_viewModel.StatusMessage, Does.Contain("Please draw a valid area"));
    }

    [Test]
    public void RamerDouglasPeucker_WithCollinearPoints_ShouldReduceToStartAndEnd()
    {
        // Arrange: 5 perfectly collinear points
        var points = new List<GeoCoordinate>
        {
            new(21.0, 105.0),
            new(21.1, 105.1),
            new(21.2, 105.2),
            new(21.3, 105.3),
            new(21.4, 105.4)
        };

        // Act
        var result = MapSearchViewModel.RamerDouglasPeucker(points, 0.001);

        // Assert: Collinear points should simplify to just start and end (2 points)
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0], Is.EqualTo(points[0]));
        Assert.That(result[1], Is.EqualTo(points[^1]));
    }

    [Test]
    public void DeduplicatePoints_ShouldRemoveConsecutiveIdenticalPoints()
    {
        // Arrange
        var points = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5855, 105.8075), // duplicate
            new(21.5950, 105.8200),
            new(21.5950, 105.8200)  // duplicate
        };

        // Act
        var result = MapSearchViewModel.DeduplicatePoints(points);

        // Assert
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public void CalculatePolygonArea_WithKnownTriangle_ShouldReturnCorrectMetricArea()
    {
        // Arrange: Right-angled triangle with sides 0.001 deg (~111.2m each)
        // Area ~ 0.5 * 111.195 * 111.195 ~ 6182 m²
        var triangle = new List<GeoCoordinate>
        {
            new(0.0, 0.0),
            new(0.001, 0.0),
            new(0.0, 0.001)
        };

        // Act
        double area = MapSearchViewModel.CalculatePolygonArea(triangle);

        // Assert
        Assert.That(area, Is.EqualTo(6182.0).Within(50.0));
    }

    [Test]
    public async Task ConfirmArea_WhenPolygonIsValid_ShouldSetIsAreaConfirmedAndShowAlert()
    {
        // Arrange
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5950, 105.8075),
            new(21.5950, 105.8200)
        };
        _viewModel.CompleteDrawing(testPoints);

        bool alertCalled = false;
        _viewModel.AlertHandler = (title, message, cancel) =>
        {
            alertCalled = true;
            Assert.That(title, Is.EqualTo("Search Area Confirmed"));
            Assert.That(message, Does.Contain("3 vertices"));
            Assert.That(message, Does.Contain("T05.2"));
            return Task.CompletedTask;
        };

        // Act
        await _viewModel.ConfirmAreaCommand.ExecuteAsync(null);

        // Assert
        Assert.That(alertCalled, Is.True);
        Assert.That(_viewModel.IsAreaConfirmed, Is.True);
        Assert.That(_viewModel.StatusMessage, Does.Contain("Area confirmed"));
    }

    [Test]
    public async Task ConfirmArea_WhenNoPolygon_ShouldRejectAndNotConfirm()
    {
        // Arrange
        bool alertCalled = false;
        _viewModel.AlertHandler = (_, _, _) =>
        {
            alertCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await _viewModel.ConfirmAreaCommand.ExecuteAsync(null);

        // Assert
        Assert.That(alertCalled, Is.False);
        Assert.That(_viewModel.IsAreaConfirmed, Is.False);
        Assert.That(_viewModel.StatusMessage, Does.Contain("Please draw a valid area"));
    }

    [Test]
    public void LatitudeToMercatorY_AtEquator_ShouldReturnZero()
    {
        // Act
        double y = MapSearchViewModel.LatitudeToMercatorY(0.0);

        // Assert
        Assert.That(y, Is.EqualTo(0.0).Within(1e-9));
    }

    [TestCase(0.0)]
    [TestCase(21.5855)]
    [TestCase(-33.8688)]
    [TestCase(51.5074)]
    [TestCase(80.0)]
    public void MercatorProjection_RoundTrip_ShouldPreserveLatitude(double inputLat)
    {
        // Act
        double mercatorY = MapSearchViewModel.LatitudeToMercatorY(inputLat);
        double roundTripLat = MapSearchViewModel.MercatorYToLatitude(mercatorY);

        // Assert
        Assert.That(roundTripLat, Is.EqualTo(inputLat).Within(1e-7));
    }

    [Test]
    public void LatitudeToMercatorY_HigherLatitude_ShouldStretchNonLinearly()
    {
        // In Mercator projection, distance from 0 to 30 deg is smaller than 30 to 60 deg
        double y0 = MapSearchViewModel.LatitudeToMercatorY(0.0);
        double y30 = MapSearchViewModel.LatitudeToMercatorY(30.0);
        double y60 = MapSearchViewModel.LatitudeToMercatorY(60.0);

        double delta1 = y30 - y0;
        double delta2 = y60 - y30;

        // Assert non-linear stretch
        Assert.That(delta2, Is.GreaterThan(delta1));
    }

    [Test]
    public void CompleteDrawing_WithClosedSquare_ShouldStripClosingDuplicateAndBeValid()
    {
        // Arrange: A -> B -> C -> D -> A (square of side 0.01 deg)
        var square = new List<GeoCoordinate>
        {
            new(21.5800, 105.8000),
            new(21.5900, 105.8000),
            new(21.5900, 105.8100),
            new(21.5800, 105.8100),
            new(21.5800, 105.8000) // closing point identical to start
        };

        // Act
        _viewModel.CompleteDrawing(square);

        // Assert
        Assert.That(_viewModel.HasDrawnPolygon, Is.True);
        Assert.That(_viewModel.VertexCount, Is.EqualTo(4));
        Assert.That(_viewModel.PolygonCoordinates.Count, Is.EqualTo(4));
    }

    [Test]
    public void CompleteDrawing_WithDegenerateDuplicateVertices_ShouldRejectPolygon()
    {
        // Arrange: A -> B -> A (3 points in list, but only 2 distinct vertices)
        var degenerate = new List<GeoCoordinate>
        {
            new(21.5800, 105.8000),
            new(21.5900, 105.8000),
            new(21.5800, 105.8000)
        };

        // Act
        _viewModel.CompleteDrawing(degenerate);

        // Assert
        Assert.That(_viewModel.HasDrawnPolygon, Is.False);
        Assert.That(_viewModel.VertexCount, Is.EqualTo(0));
        Assert.That(_viewModel.StatusMessage, Does.Contain("too small").Or.Contain("too flat"));
    }

    [Test]
    public void CompleteDrawing_WithCollinearPointsAndClosedLoop_ShouldRejectDueToZeroArea()
    {
        // Arrange: points on a straight line going back and forth
        var lineLoop = new List<GeoCoordinate>
        {
            new(21.5800, 105.8000),
            new(21.5850, 105.8050),
            new(21.5900, 105.8100),
            new(21.5850, 105.8050),
            new(21.5800, 105.8000)
        };

        // Act
        _viewModel.CompleteDrawing(lineLoop);

        // Assert
        Assert.That(_viewModel.HasDrawnPolygon, Is.False);
        Assert.That(_viewModel.VertexCount, Is.EqualTo(0));
    }

    [Test]
    public void NormalizePolygonPoints_ShouldStripClosingDuplicatePoint()
    {
        // Arrange
        var points = new List<GeoCoordinate>
        {
            new(21.0, 105.0),
            new(21.1, 105.0),
            new(21.1, 105.1),
            new(21.0, 105.0) // duplicate closing point
        };

        // Act
        var normalized = MapSearchViewModel.NormalizePolygonPoints(points);

        // Assert
        Assert.That(normalized.Count, Is.EqualTo(3));
        Assert.That(normalized[0], Is.EqualTo(points[0]));
        Assert.That(normalized[2], Is.EqualTo(points[2]));
    }

    [Test]
    public void GetDistinctVertices_ShouldFilterNearDuplicatePoints()
    {
        // Arrange
        var points = new List<GeoCoordinate>
        {
            new(21.0, 105.0),
            new(21.0000001, 105.0000001), // within 1e-6 tolerance
            new(21.1, 105.0),
            new(21.1, 105.1)
        };

        // Act
        var distinct = MapSearchViewModel.GetDistinctVertices(points, 1e-6);

        // Assert
        Assert.That(distinct.Count, Is.EqualTo(3));
    }

    [Test]
    public void IsSelfIntersecting_WithBowTiePolygon_ShouldReturnTrue()
    {
        // Arrange: Bow-tie shape (hourglass) where non-adjacent edges cross in center
        var bowTie = new List<GeoCoordinate>
        {
            new(21.0, 105.0),
            new(21.1, 105.1),
            new(21.0, 105.1),
            new(21.1, 105.0)
        };

        // Act
        bool isSelfIntersecting = MapSearchViewModel.IsSelfIntersecting(bowTie);

        // Assert
        Assert.That(isSelfIntersecting, Is.True);
    }

    [Test]
    public void CompleteDrawing_WithBowTie_ShouldKeepOneOrderedBoundary()
    {
        // Arrange: Hourglass/bow-tie
        var bowTie = new List<GeoCoordinate>
        {
            new(21.5800, 105.8000),
            new(21.5900, 105.8100),
            new(21.5800, 105.8100),
            new(21.5900, 105.8000)
        };

        // Act
        _viewModel.CompleteDrawing(bowTie);

        // Assert
        Assert.That(_viewModel.HasDrawnPolygon, Is.True);
        Assert.That(MapSearchViewModel.IsSelfIntersecting(_viewModel.PolygonCoordinates), Is.True);
        Assert.That(_viewModel.PolygonCoordinates, Is.EqualTo(bowTie));
    }

    [Test]
    public void SimplifyPolygon_With24VertexCircle_ShouldPreserveContourAndStayWithinTargetCount()
    {
        // Arrange: Approximate circle with 24 vertices (radius ~ 200m)
        double centerLat = 21.5855;
        double centerLon = 105.8075;
        double radiusDeg = 0.002;
        var circlePoints = new List<GeoCoordinate>();
        int numPoints = 24;

        for (int i = 0; i < numPoints; i++)
        {
            double angle = (2.0 * Math.PI * i) / numPoints;
            circlePoints.Add(new GeoCoordinate(
                centerLat + radiusDeg * Math.Sin(angle),
                centerLon + radiusDeg * Math.Cos(angle)));
        }

        // Act
        var simplified = MapSearchViewModel.SimplifyPolygon(circlePoints, targetMaxCount: 16);

        // Assert
        Assert.That(simplified.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(simplified.Count, Is.LessThanOrEqualTo(16));

        // Enclosed area of simplified polygon should still be substantial (~100,000+ m²)
        double area = MapSearchViewModel.CalculatePolygonAreaSquareMeters(simplified);
        Assert.That(area, Is.GreaterThan(50000.0));
    }

    [Test]
    public void SimplifyPolygon_WithConcaveLShapedPolygon_ShouldPreserveConcaveCorner()
    {
        // Arrange: L-shaped polygon with inner concave reflex vertex
        var lShape = new List<GeoCoordinate>
        {
            new(21.5800, 105.8000), // bottom-left
            new(21.5900, 105.8000), // top-left
            new(21.5900, 105.8050), // top-middle
            new(21.5850, 105.8050), // inner concave corner
            new(21.5850, 105.8100), // right-middle
            new(21.5800, 105.8100)  // bottom-right
        };

        // Act
        var simplified = MapSearchViewModel.SimplifyPolygon(lShape, targetMaxCount: 16);

        // Assert: All 6 key vertices should be preserved
        Assert.That(simplified.Count, Is.EqualTo(6));
        Assert.That(simplified.Any(p => MapSearchViewModel.IsSameLocation(p, new GeoCoordinate(21.5850, 105.8050), 1e-4)), Is.True);
    }

    [Test]
    public void SimplifyClosedPolygonRDP_WithHighVariationNearClosingEdge_ShouldPreserveClosingContour()
    {
        // Arrange: Square where closing edge (from last to first point) has a prominent spike
        var points = new List<GeoCoordinate>
        {
            new(21.5800, 105.8000), // index 0 (start)
            new(21.5900, 105.8000),
            new(21.5900, 105.8100),
            new(21.5800, 105.8100), // index 3
            new(21.5750, 105.8050)  // index 4: sharp spike on closing segment returning to index 0
        };

        // Act
        var simplified = MapSearchViewModel.SimplifyClosedPolygonRDP(points, epsilon: 0.0001);

        // Assert: The prominent spike on closing contour should NOT be flattened
        Assert.That(simplified.Count, Is.EqualTo(5));
        Assert.That(simplified.Any(p => MapSearchViewModel.IsSameLocation(p, new GeoCoordinate(21.5750, 105.8050), 1e-4)), Is.True);
    }
}
