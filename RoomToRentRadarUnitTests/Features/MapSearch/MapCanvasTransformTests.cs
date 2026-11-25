using Microsoft.Maui.Graphics;
using RoomToRentRadar.Views.MapSearch;

namespace RoomToRentRadarUnitTests.Features.MapSearch;

[TestFixture]
public class MapCanvasTransformTests
{
    [Test]
    public void ToMapPixels_AppliesDensityAndCanvasOffset()
    {
        var transform = new MapCanvasTransform(2.625f, OffsetX: 12, OffsetY: -24);

        Assert.That(transform.ToMapPixels(new PointF(100, 160)), Is.EqualTo((274, 396)));
    }

    [Test]
    public void ToCanvasPoint_RoundTripsMapPixelsToTheSameCanvasPosition()
    {
        var transform = new MapCanvasTransform(2.625f, OffsetX: -6, OffsetY: 19);
        var drawnPoint = new PointF(16.25f, 42.5f);
        var (x, y) = transform.ToMapPixels(drawnPoint);

        var renderedPoint = transform.ToCanvasPoint(x, y);

        Assert.Multiple(() =>
        {
            Assert.That(renderedPoint.X, Is.EqualTo(drawnPoint.X).Within(0.5f / transform.Density));
            Assert.That(renderedPoint.Y, Is.EqualTo(drawnPoint.Y).Within(0.5f / transform.Density));
        });
    }
}
