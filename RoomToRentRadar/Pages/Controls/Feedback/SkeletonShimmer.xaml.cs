using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.Pages.Controls.Feedback;

public partial class SkeletonShimmer : ContentView
{
    private bool _isAnimating;

    public SkeletonShimmer()
    {
        InitializeComponent();
        SemanticProperties.SetDescription(this, LocalizedStrings.Instance["State_Loading"]);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        SemanticProperties.SetDescription(this, LocalizedStrings.Instance["State_Loading"]);
        LocalizedStrings.Instance.PropertyChanged += OnLocalizedStringsChanged;
        StartPulseAnimation();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        LocalizedStrings.Instance.PropertyChanged -= OnLocalizedStringsChanged;
        _isAnimating = false;
        this.AbortAnimation("PulseAnimation");
    }

    private void OnLocalizedStringsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        SemanticProperties.SetDescription(this, LocalizedStrings.Instance["State_Loading"]);
    }

    private void StartPulseAnimation()
    {
        if (_isAnimating) return;
        _isAnimating = true;

        var animation = new Animation
        {
            { 0, 0.5, new Animation(v => SetPlaceholderOpacity(v), 0.7, 0.35, Easing.SinInOut) },
            { 0.5, 1.0, new Animation(v => SetPlaceholderOpacity(v), 0.35, 0.7, Easing.SinInOut) }
        };

        animation.Commit(this, "PulseAnimation", length: 1200, repeat: () => _isAnimating);
    }

    private void SetPlaceholderOpacity(double opacity)
    {
        ImagePlaceholder.Opacity = opacity;
        PriceBar.Opacity = opacity;
        TitleBar.Opacity = opacity * 0.9;
        LocationBar.Opacity = opacity * 0.8;
    }
}

