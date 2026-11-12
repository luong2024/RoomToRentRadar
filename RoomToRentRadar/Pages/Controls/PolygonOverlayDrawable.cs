using System.Collections.Generic;
using Microsoft.Maui.Graphics;

namespace RoomToRentRadar.Pages.Controls;

/// <summary>
/// Vẽ nét đa giác trực tiếp trên GraphicsView (PolygonCanvas) khi người dùng kéo tay trên màn hình.
/// </summary>
public sealed class PolygonOverlayDrawable : IDrawable
{
    private readonly List<PointF> _points = [];

    public IReadOnlyList<PointF> Points => _points;

    public void Reset(PointF firstPoint)
    {
        _points.Clear();
        _points.Add(firstPoint);
    }

    public bool TryAddPoint(PointF point, float minDistancePx = 10f)
    {
        if (_points.Count == 0)
        {
            _points.Add(point);
            return true;
        }

        var last = _points[^1];
        float dx = point.X - last.X;
        float dy = point.Y - last.Y;
        if ((dx * dx) + (dy * dy) < (minDistancePx * minDistancePx))
            return false;

        _points.Add(point);
        return true;
    }

    public void Clear() => _points.Clear();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_points.Count < 2)
            return;

        var path = new PathF();
        path.MoveTo(_points[0]);
        for (int i = 1; i < _points.Count; i++)
        {
            path.LineTo(_points[i]);
        }

        canvas.StrokeColor = Color.FromArgb("#1D4ED8");
        canvas.StrokeSize = 4f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.FillColor = Color.FromRgba(29, 78, 216, 45);

        if (_points.Count >= 3)
        {
            canvas.FillPath(path);
        }
        canvas.DrawPath(path);
    }
}