namespace RoomToRentRadar.Pages.Controls.Cards;

public partial class MetricCardView : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(MetricCardView), string.Empty);

    public static readonly BindableProperty IconGlyphProperty =
        BindableProperty.Create(nameof(IconGlyph), typeof(string), typeof(MetricCardView), "📊");

    public static readonly BindableProperty ValueTextProperty =
        BindableProperty.Create(nameof(ValueText), typeof(string), typeof(MetricCardView), "0");

    public static readonly BindableProperty TrendTextProperty =
        BindableProperty.Create(nameof(TrendText), typeof(string), typeof(MetricCardView), string.Empty,
            propertyChanged: (b, o, n) => ((MetricCardView)b).UpdateTrend());

    public static readonly BindableProperty TrendColorProperty =
        BindableProperty.Create(nameof(TrendColor), typeof(Color), typeof(MetricCardView), Color.FromArgb("#4CB5AE"));

    public static readonly BindableProperty HasTrendProperty =
        BindableProperty.Create(nameof(HasTrend), typeof(bool), typeof(MetricCardView), false);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public string TrendText
    {
        get => (string)GetValue(TrendTextProperty);
        set => SetValue(TrendTextProperty, value);
    }

    public Color TrendColor
    {
        get => (Color)GetValue(TrendColorProperty);
        set => SetValue(TrendColorProperty, value);
    }

    public bool HasTrend
    {
        get => (bool)GetValue(HasTrendProperty);
        private set => SetValue(HasTrendProperty, value);
    }

    public MetricCardView()
    {
        InitializeComponent();
        BindingContext = this;
        UpdateTrend();
    }

    private void UpdateTrend()
    {
        HasTrend = !string.IsNullOrWhiteSpace(TrendText);
    }
}

