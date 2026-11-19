namespace RoomToRentRadar.Features.MapSearch.Views;

/// <summary>
/// Real-time graphics canvas drawable for rendering freehand drawing paths and anchor pins for polygon editing.
/// </summary>
public class DrawingCanvasDrawable : IDrawable
{
    private readonly List<PointF> _touchPoints = new();
    private readonly List<PointF> _anchorPoints = new();

    public Color StrokeColor { get; set; } = Color.FromArgb("#2563EB"); // PrimaryBrand
    public float StrokeThickness { get; set; } = 3.5f;
    public bool IsEditingMode { get; set; }
    public int DraggingAnchorIndex { get; set; } = -1;

    public void AddPoint(PointF point)
    {
        if (!float.IsNaN(point.X) && !float.IsNaN(point.Y))
        {
            _touchPoints.Add(point);
        }
    }

    public void Clear()
    {
        _touchPoints.Clear();
    }

    public void SetAnchors(IEnumerable<PointF> anchors)
    {
        _anchorPoints.Clear();
        foreach (var anchor in anchors)
        {
            if (!float.IsNaN(anchor.X) && !float.IsNaN(anchor.Y))
            {
                _anchorPoints.Add(anchor);
            }
        }
    }

    public IReadOnlyList<PointF> GetPoints() => _touchPoints;
    public IReadOnlyList<PointF> GetAnchors() => _anchorPoints;

    public void UpdateAnchor(int index, PointF newPos)
    {
        if (index >= 0 && index < _anchorPoints.Count && !float.IsNaN(newPos.X) && !float.IsNaN(newPos.Y))
        {
            _anchorPoints[index] = newPos;
        }
    }

    public int HitTestAnchor(PointF touch, float radius = 28f)
    {
        if (float.IsNaN(touch.X) || float.IsNaN(touch.Y))
            return -1;

        for (int i = 0; i < _anchorPoints.Count; i++)
        {
            float dx = _anchorPoints[i].X - touch.X;
            float dy = _anchorPoints[i].Y - touch.Y;
            if ((dx * dx + dy * dy) <= (radius * radius))
            {
                return i;
            }
        }
        return -1;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // 1. Edit mode: Draw polygon connecting anchor points and render circular anchor pins
        if (IsEditingMode && _anchorPoints.Count >= 3)
        {
            canvas.SaveState();

            // Semi-transparent polygon fill
            canvas.FillColor = Color.FromArgb("#252563EB");
            var polyPath = new PathF();
            polyPath.MoveTo(_anchorPoints[0]);
            for (int i = 1; i < _anchorPoints.Count; i++)
            {
                polyPath.LineTo(_anchorPoints[i]);
            }
            polyPath.Close();
            canvas.FillPath(polyPath);

            // Polygon boundary outline
            canvas.StrokeColor = StrokeColor;
            canvas.StrokeSize = 2.5f;
            canvas.DrawPath(polyPath);

            // Render circular anchor pins with dark border (Rightmove style)
            for (int i = 0; i < _anchorPoints.Count; i++)
            {
                var pt = _anchorPoints[i];
                bool isDragging = (i == DraggingAnchorIndex);

                // Subtle shadow
                canvas.FillColor = Color.FromArgb("#40000000");
                canvas.FillCircle(pt.X, pt.Y + 1.5f, isDragging ? 10f : 8f);

                // Pin inner circle
                canvas.FillColor = isDragging ? Color.FromArgb("#EFF6FF") : Colors.White;
                canvas.FillCircle(pt.X, pt.Y, isDragging ? 9f : 7f);

                // Pin outer border
                canvas.StrokeColor = isDragging ? Color.FromArgb("#2563EB") : Color.FromArgb("#1E293B");
                canvas.StrokeSize = isDragging ? 2.5f : 1.8f;
                canvas.DrawCircle(pt.X, pt.Y, isDragging ? 9f : 7f);
            }

            canvas.RestoreState();
            return;
        }

        // 2. Freehand drawing mode: Render smooth path tracing user touch
        if (_touchPoints.Count < 2)
            return;

        canvas.SaveState();
        canvas.StrokeColor = StrokeColor;
        canvas.StrokeSize = StrokeThickness;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        var path = new PathF();
        path.MoveTo(_touchPoints[0]);

        for (int i = 1; i < _touchPoints.Count; i++)
        {
            path.LineTo(_touchPoints[i]);
        }

        canvas.DrawPath(path);
        canvas.RestoreState();
    }
}
