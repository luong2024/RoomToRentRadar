using NUnit.Framework;
using RoomToRentRadar.Features.MapSearch.Models;
using RoomToRentRadar.Features.MapSearch.ViewModels;

namespace RoomToRentRadarUnitTests.Features.MapSearch;

[TestFixture]
public class MapSearchViewModelTests
{
    private MapSearchViewModel _viewModel = null!;

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
        // Arrange
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5890, 105.8110),
            new(21.5840, 105.8060)
        };

        _viewModel.StartDrawing();

        // Act
        _viewModel.CompleteDrawing(testPoints);

        // Assert
        Assert.That(_viewModel.IsDrawingMode, Is.False);
        Assert.That(_viewModel.HasDrawnPolygon, Is.True);
        Assert.That(_viewModel.VertexCount, Is.EqualTo(3));
        Assert.That(_viewModel.PolygonCoordinates.Count, Is.EqualTo(3));
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
    public void ClearPolygon_ShouldResetAllDrawingState()
    {
        // Arrange
        var testPoints = new List<GeoCoordinate>
        {
            new(21.5855, 105.8075),
            new(21.5890, 105.8110),
            new(21.5840, 105.8060)
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
            new(21.5890, 105.8110),
            new(21.5840, 105.8060)
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
            new(21.5890, 105.8110),
            new(21.5840, 105.8060)
        };
        _viewModel.CompleteDrawing(testPoints);

        // Act
        _viewModel.StartEditingCommand.Execute(null);

        // Assert
        Assert.That(_viewModel.IsEditingMode, Is.True);
        Assert.That(_viewModel.EditAnchorPins.Count, Is.EqualTo(3));
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
            new(21.5890, 105.8110),
            new(21.5840, 105.8060)
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
            new(21.5890, 105.8110),
            new(21.5840, 105.8060)
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
            new(21.5890, 105.8110),
            new(21.5840, 105.8060)
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
    public void SimplifyPolygon_WithManyPoints_ShouldReduceToTargetCount()
    {
        // Arrange: 80 points along a circle
        var points = new List<GeoCoordinate>();
        for (int i = 0; i < 80; i++)
        {
            points.Add(new GeoCoordinate(21.58 + (i * 0.001), 105.80 + (i * 0.001)));
        }

        // Act
        var simplified = MapSearchViewModel.SimplifyPolygon(points, 16);

        // Assert
        Assert.That(simplified.Count, Is.EqualTo(16));
    }
}
