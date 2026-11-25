namespace RoomToRentRadar.Pages.Controls.Search;

/// <summary>
/// Pure-MAUI dual-thumb range slider (Filters.dc.html). Thumb hit areas are 48x48 dp;
/// external values are honoured as-is (typed input may exceed the slider range) and only
/// drag gestures snap to <see cref="Step"/>.
/// </summary>
public partial class RangeSliderControl : ContentView
{
    public const double ThumbHitSize = 48;

    private readonly SliderDrawable _drawable = new();
    private double _dragStartValue;

    public RangeSliderControl()
    {
        InitializeComponent();
        Canvas.Drawable = _drawable;
        UpdateVisual();
    }

    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(
        nameof(Minimum), typeof(double), typeof(RangeSliderControl), 0d,
        propertyChanged: OnVisualSettingChanged);

    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(
        nameof(Maximum), typeof(double), typeof(RangeSliderControl), 100d,
        propertyChanged: OnVisualSettingChanged);

    public static readonly BindableProperty StepProperty = BindableProperty.Create(
        nameof(Step), typeof(double), typeof(RangeSliderControl), 1d,
        propertyChanged: OnVisualSettingChanged);

    public static readonly BindableProperty LowerValueProperty = BindableProperty.Create(
        nameof(LowerValue), typeof(double), typeof(RangeSliderControl), 0d,
        BindingMode.TwoWay, propertyChanged: OnValueChanged);

    public static readonly BindableProperty UpperValueProperty = BindableProperty.Create(
        nameof(UpperValue), typeof(double), typeof(RangeSliderControl), 100d,
        BindingMode.TwoWay, propertyChanged: OnValueChanged);

    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(
        nameof(TrackColor), typeof(Color), typeof(RangeSliderControl), Color.FromArgb("#E2E8F0"),
        propertyChanged: OnVisualSettingChanged);

    public static readonly BindableProperty ActiveTrackColorProperty = BindableProperty.Create(
        nameof(ActiveTrackColor), typeof(Color), typeof(RangeSliderControl), Color.FromArgb("#163A5F"),
        propertyChanged: OnVisualSettingChanged);

    public static readonly BindableProperty ThumbFillColorProperty = BindableProperty.Create(
        nameof(ThumbFillColor), typeof(Color), typeof(RangeSliderControl), Colors.White,
        propertyChanged: OnVisualSettingChanged);

    public static readonly BindableProperty ThumbStrokeColorProperty = BindableProperty.Create(
        nameof(ThumbStrokeColor), typeof(Color), typeof(RangeSliderControl), Color.FromArgb("#163A5F"),
        propertyChanged: OnVisualSettingChanged);

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public double LowerValue
    {
        get => (double)GetValue(LowerValueProperty);
        set => SetValue(LowerValueProperty, value);
    }

    public double UpperValue
    {
        get => (double)GetValue(UpperValueProperty);
        set => SetValue(UpperValueProperty, value);
    }

    public Color TrackColor
    {
        get => (Color)GetValue(TrackColorProperty);
        set => SetValue(TrackColorProperty, value);
    }

    public Color ActiveTrackColor
    {
        get => (Color)GetValue(ActiveTrackColorProperty);
        set => SetValue(ActiveTrackColorProperty, value);
    }

    public Color ThumbFillColor
    {
        get => (Color)GetValue(ThumbFillColorProperty);
        set => SetValue(ThumbFillColorProperty, value);
    }

    public Color ThumbStrokeColor
    {
        get => (Color)GetValue(ThumbStrokeColorProperty);
        set => SetValue(ThumbStrokeColorProperty, value);
    }

    private static void OnVisualSettingChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((RangeSliderControl)bindable).UpdateVisual();

    private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (RangeSliderControl)bindable;
        control.CoerceValues();
        control.UpdateVisual();
    }

    private void CoerceValues()
    {
        var low = Minimum;
        var high = Maximum <= Minimum ? Minimum + 1 : Maximum;

        var lower = Math.Clamp(LowerValue, low, high);
        var upper = Math.Clamp(UpperValue, low, high);
        if (lower > upper)
            lower = upper;

        if (Math.Abs(lower - LowerValue) > double.Epsilon)
            LowerValue = lower;

        if (Math.Abs(upper - UpperValue) > double.Epsilon)
            UpperValue = upper;
    }

    private void OnHostSizeChanged(object? sender, EventArgs e) => UpdateVisual();

    private void UpdateVisual()
    {
        var width = Width;
        var usable = Math.Max(1, width - ThumbHitSize);
        var span = Math.Max(1, Maximum - Minimum);

        var lowerRatio = Math.Clamp((LowerValue - Minimum) / span, 0d, 1d);
        var upperRatio = Math.Clamp((UpperValue - Minimum) / span, 0d, 1d);
        if (lowerRatio > upperRatio)
            (lowerRatio, upperRatio) = (upperRatio, lowerRatio);

        LowerThumbHost.TranslationX = lowerRatio * usable;
        UpperThumbHost.TranslationX = upperRatio * usable;

        _drawable.LowerRatio = lowerRatio;
        _drawable.UpperRatio = upperRatio;
        _drawable.TrackColor = TrackColor;
        _drawable.ActiveColor = ActiveTrackColor;
        _drawable.ThumbFill = ThumbFillColor;
        _drawable.ThumbStroke = ThumbStrokeColor;
        Canvas.Invalidate();
    }

    private sealed class SliderDrawable : IDrawable
    {
        public double LowerRatio { get; set; }
        public double UpperRatio { get; set; }
        public Color TrackColor { get; set; } = Color.FromArgb("#E2E8F0");
        public Color ActiveColor { get; set; } = Color.FromArgb("#163A5F");
        public Color ThumbFill { get; set; } = Colors.White;
        public Color ThumbStroke { get; set; } = Color.FromArgb("#163A5F");

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var inset = (float)(ThumbHitSize / 2);
            var y = dirtyRect.Height / 2f;
            var left = inset;
            var right = dirtyRect.Width - inset;
            var usable = Math.Max(1f, right - left);

            canvas.Antialias = true;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeSize = 4;

            canvas.StrokeColor = TrackColor;
            canvas.DrawLine(left, y, right, y);

            var lowerX = left + (float)(LowerRatio * usable);
            var upperX = left + (float)(UpperRatio * usable);
            if (upperX > lowerX)
            {
                canvas.StrokeColor = ActiveColor;
                canvas.DrawLine(lowerX, y, upperX, y);
            }

            DrawThumb(canvas, lowerX, y);
            DrawThumb(canvas, upperX, y);
        }

        private void DrawThumb(ICanvas canvas, float x, float y)
        {
            const float radius = 11f;
            canvas.FillColor = ThumbFill;
            canvas.FillCircle(x, y, radius);
            canvas.StrokeSize = 2;
            canvas.StrokeColor = ThumbStroke;
            canvas.DrawCircle(x, y, radius);
        }
    }

    private void OnLowerPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _dragStartValue = LowerValue;
                break;
            case GestureStatus.Running:
                LowerValue = Snap(Math.Clamp(_dragStartValue + DeltaFor(e.TotalX), Minimum, UpperValue));
                break;
        }
    }

    private void OnUpperPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _dragStartValue = UpperValue;
                break;
            case GestureStatus.Running:
                UpperValue = Snap(Math.Clamp(_dragStartValue + DeltaFor(e.TotalX), LowerValue, Maximum));
                break;
        }
    }

    private double DeltaFor(double totalX)
    {
        var usable = Math.Max(1, Width - ThumbHitSize);
        return totalX / usable * (Maximum - Minimum);
    }

    private double Snap(double value)
    {
        if (Step <= 0)
            return value;

        return Math.Round(value / Step, MidpointRounding.AwayFromZero) * Step;
    }
}
