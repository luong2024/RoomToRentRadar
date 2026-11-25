namespace RoomToRentRadar.Pages.Controls.Search;

/// <summary>
/// Background painter for the joined 1/2/3/5/10 km segmented bar (US04 / T04.2 Filters panel).
/// Drawn with GraphicsView because WinUI mis-renders small rounded Borders and layout backgrounds
/// (fill blobs / partial-height paint), so the frame, dividers and selected-cell fill are painted
/// directly and the labels are layered on top by the page.
/// </summary>
public sealed class SegmentedFillControl : ContentView
{
    private const int SegmentCount = 5;
    private const float Radius = 12f;

    private readonly GraphicsView _canvas = new() { InputTransparent = true };
    private readonly SegmentDrawable _drawable = new();

    public static readonly BindableProperty IsOption1SelectedProperty = BindableProperty.Create(
        nameof(IsOption1Selected), typeof(bool), typeof(SegmentedFillControl), false,
        propertyChanged: OnVisualChanged);

    public static readonly BindableProperty IsOption2SelectedProperty = BindableProperty.Create(
        nameof(IsOption2Selected), typeof(bool), typeof(SegmentedFillControl), false,
        propertyChanged: OnVisualChanged);

    public static readonly BindableProperty IsOption3SelectedProperty = BindableProperty.Create(
        nameof(IsOption3Selected), typeof(bool), typeof(SegmentedFillControl), false,
        propertyChanged: OnVisualChanged);

    public static readonly BindableProperty IsOption5SelectedProperty = BindableProperty.Create(
        nameof(IsOption5Selected), typeof(bool), typeof(SegmentedFillControl), false,
        propertyChanged: OnVisualChanged);

    public static readonly BindableProperty IsOption10SelectedProperty = BindableProperty.Create(
        nameof(IsOption10Selected), typeof(bool), typeof(SegmentedFillControl), false,
        propertyChanged: OnVisualChanged);

    public static readonly BindableProperty FrameColorProperty = BindableProperty.Create(
        nameof(FrameColor), typeof(Color), typeof(SegmentedFillControl), Color.FromArgb("#CBD5E1"),
        propertyChanged: OnVisualChanged);

    public static readonly BindableProperty FillColorProperty = BindableProperty.Create(
        nameof(FillColor), typeof(Color), typeof(SegmentedFillControl), Colors.White,
        propertyChanged: OnVisualChanged);

    public static readonly BindableProperty SelectedFillColorProperty = BindableProperty.Create(
        nameof(SelectedFillColor), typeof(Color), typeof(SegmentedFillControl), Color.FromArgb("#163A5F"),
        propertyChanged: OnVisualChanged);

    public bool IsOption1Selected
    {
        get => (bool)GetValue(IsOption1SelectedProperty);
        set => SetValue(IsOption1SelectedProperty, value);
    }

    public bool IsOption2Selected
    {
        get => (bool)GetValue(IsOption2SelectedProperty);
        set => SetValue(IsOption2SelectedProperty, value);
    }

    public bool IsOption3Selected
    {
        get => (bool)GetValue(IsOption3SelectedProperty);
        set => SetValue(IsOption3SelectedProperty, value);
    }

    public bool IsOption5Selected
    {
        get => (bool)GetValue(IsOption5SelectedProperty);
        set => SetValue(IsOption5SelectedProperty, value);
    }

    public bool IsOption10Selected
    {
        get => (bool)GetValue(IsOption10SelectedProperty);
        set => SetValue(IsOption10SelectedProperty, value);
    }

    public Color FrameColor
    {
        get => (Color)GetValue(FrameColorProperty);
        set => SetValue(FrameColorProperty, value);
    }

    public Color FillColor
    {
        get => (Color)GetValue(FillColorProperty);
        set => SetValue(FillColorProperty, value);
    }

    public Color SelectedFillColor
    {
        get => (Color)GetValue(SelectedFillColorProperty);
        set => SetValue(SelectedFillColorProperty, value);
    }

    public SegmentedFillControl()
    {
        _canvas.Drawable = _drawable;
        Content = _canvas;
        UpdateDrawable();
    }

    private static void OnVisualChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((SegmentedFillControl)bindable).UpdateDrawable();

    private void UpdateDrawable()
    {
        _drawable.SelectedIndex = IsOption1Selected ? 0
            : IsOption2Selected ? 1
            : IsOption3Selected ? 2
            : IsOption5Selected ? 3
            : IsOption10Selected ? 4
            : -1;
        _drawable.Frame = FrameColor;
        _drawable.Fill = FillColor;
        _drawable.Selected = SelectedFillColor;
        _canvas.Invalidate();
    }

    private sealed class SegmentDrawable : IDrawable
    {
        public int SelectedIndex { get; set; } = -1;
        public Color Frame { get; set; } = Color.FromArgb("#CBD5E1");
        public Color Fill { get; set; } = Colors.White;
        public Color Selected { get; set; } = Color.FromArgb("#163A5F");

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var w = dirtyRect.Width;
            var h = dirtyRect.Height;
            if (w <= 2 || h <= 2)
                return;

            canvas.Antialias = true;

            var frame = RoundedRect(0.5f, 0.5f, w - 1f, h - 1f, Radius);
            canvas.FillColor = Fill;
            canvas.FillPath(frame);

            if (SelectedIndex >= 0 && SelectedIndex < SegmentCount)
            {
                var segW = (w - 1f) / SegmentCount;
                var x = 0.5f + SelectedIndex * segW;
                canvas.FillColor = Selected;

                if (SelectedIndex == 0)
                    canvas.FillPath(RoundedRect(x, 0.5f, segW, h - 1f, Radius, roundLeft: true, roundRight: false));
                else if (SelectedIndex == SegmentCount - 1)
                    canvas.FillPath(RoundedRect(x, 0.5f, segW, h - 1f, Radius, roundLeft: false, roundRight: true));
                else
                    canvas.FillRectangle(x, 0.5f, segW, h - 1f);
            }

            canvas.StrokeColor = Frame;
            canvas.StrokeSize = 1;
            var step = w / SegmentCount;
            for (var i = 1; i < SegmentCount; i++)
                canvas.DrawLine(i * step, 1f, i * step, h - 1f);

            canvas.DrawPath(frame);
        }

        private static PathF RoundedRect(float x, float y, float w, float h, float r, bool roundLeft = true, bool roundRight = true)
        {
            var path = new PathF();
            var rl = roundLeft ? r : 0f;
            var rr = roundRight ? r : 0f;

            path.MoveTo(x + rl, y);
            path.LineTo(x + w - rr, y);
            if (rr > 0)
                path.QuadTo(x + w, y, x + w, y + rr);
            path.LineTo(x + w, y + h - rr);
            if (rr > 0)
                path.QuadTo(x + w, y + h, x + w - rr, y + h);
            path.LineTo(x + rl, y + h);
            if (rl > 0)
                path.QuadTo(x, y + h, x, y + h - rl);
            path.LineTo(x, y + rl);
            if (rl > 0)
                path.QuadTo(x, y, x + rl, y);
            path.Close();
            return path;
        }
    }
}
