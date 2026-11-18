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
        Assert.That(_viewModel.VertexCount, Is.EqualTo(0));
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
    }
}
