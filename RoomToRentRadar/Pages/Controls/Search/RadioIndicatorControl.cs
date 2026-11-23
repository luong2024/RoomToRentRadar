namespace RoomToRentRadar.Pages.Controls.Search;

/// <summary>
/// Sort-radio indicator for the Filters panel (US04 / T04.2). Painted with GraphicsView because
/// WinUI mis-renders tiny rounded Borders (only the top-left corner is rounded), regardless of
/// stroke or corner radius.
/// </summary>
public sealed class RadioIndicatorControl : ContentView
{
    private const float BoxSize = 20f;

    private readonly GraphicsView _canvas = new() { WidthRequest = BoxSize, HeightRequest = BoxSize };
    private readonly SliderDrawable _drawable = new();

    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected), typeof(bool), typeof(RadioIndicatorControl), false,
        propertyChanged: OnVisualChanged);

    public static readonly BindableProperty RingColorProperty = BindableProperty.Create(
        nameof(RingColor), typeof(Color), typeof(RadioIndicatorControl), Color.FromArgb("#94A3B8"),
        propertyChanged: OnVisualChanged);

    public static readonly BindableProperty SelectedColorProperty = BindableProperty.Create(
        nameof(SelectedColor), typeof(Color), typeof(RadioIndicatorControl), Color.FromArgb("#163A5F"),
        propertyChanged: OnVisualChanged);

    public static readonly BindableProperty HoleColorProperty = BindableProperty.Create(
        nameof(HoleColor), typeof(Color), typeof(RadioIndicatorControl), Colors.White,
        propertyChanged: OnVisualChanged);

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public Color RingColor
    {
        get => (Color)GetValue(RingColorProperty);
        set => SetValue(RingColorProperty, value);
    }

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public Color HoleColor
    {
        get => (Color)GetValue(HoleColorProperty);
        set => SetValue(HoleColorProperty, value);
    }

    public RadioIndicatorControl()
    {
        WidthRequest = BoxSize;
        HeightRequest = BoxSize;
        _canvas.Drawable = _drawable;
        Content = _canvas;
        UpdateDrawable();
    }

    private static void OnVisualChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((RadioIndicatorControl)bindable).UpdateDrawable();

    private void UpdateDrawable()
    {
        _drawable.Ring = IsSelected ? SelectedColor : RingColor;
        _drawable.Hole = HoleColor;
        _drawable.Dot = SelectedColor;
        _drawable.ShowDot = IsSelected;
        _canvas.Invalidate();
    }

    private sealed class SliderDrawable : IDrawable
    {
        public Color Ring { get; set; } = Color.FromArgb("#94A3B8");
        public Color Hole { get; set; } = Colors.White;
        public Color Dot { get; set; } = Color.FromArgb("#163A5F");
        public bool ShowDot { get; set; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var cx = dirtyRect.Width / 2f;
            var cy = dirtyRect.Height / 2f;

            canvas.Antialias = true;

            canvas.FillColor = Ring;
            canvas.FillCircle(cx, cy, 9.5f);

            canvas.FillColor = Hole;
            canvas.FillCircle(cx, cy, 7.5f);

            if (ShowDot)
            {
                canvas.FillColor = Dot;
                canvas.FillCircle(cx, cy, 5f);
            }
        }
    }
}
