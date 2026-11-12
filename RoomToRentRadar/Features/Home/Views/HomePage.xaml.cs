using System.ComponentModel;
using RoomToRentRadar.Features.Home.ViewModels;

namespace RoomToRentRadar.Features.Home.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;
    private bool _motionActive;
    private bool _didAnimateEntrance;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        SizeChanged += (_, _) => UpdateLayout();
    }

    private void UpdateLayout()
    {
        var inset = Width >= 720 ? 40 : 20;
        TopBar.Padding = new Thickness(inset, 14);
        ListingRefresh.Margin = new Thickness(inset, 0, inset - 18, 0);
        var contentWidth = Math.Min(Width - inset * 2, 1280);
        CardsLayout.Span = contentWidth >= 1040 ? 3 : contentWidth >= 660 ? 2 : 1;
        HeroArt.IsVisible = Width >= 850;
        NavCaption.IsVisible = Width >= 850;
        HeroTitle.FontSize = Width >= 720 ? 34 : 28;
    }

    private async void OnOpenMapClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("//map");

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateLayout();
        if (!_motionActive)
        {
            _motionActive = true;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        if (!_didAnimateEntrance)
        {
            _didAnimateEntrance = true;
            _ = RevealAsync(HomeChrome, 8);
        }

        if (_viewModel.Listings.Count == 0 && _viewModel.LoadCommand.CanExecute(null))
            _viewModel.LoadCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        _motionActive = false;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ResetMotion(HomeChrome);
        ResetMotion(FilterPanel);
        base.OnDisappearing();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeViewModel.IsFilterPanelOpen))
            return;
        Dispatcher.Dispatch(() =>
        {
            if (!_motionActive)
                return;
            ResetMotion(FilterPanel);
            if (_viewModel.IsFilterPanelOpen)
                _ = RevealAsync(FilterPanel, 14);
        });
    }

    private async Task RevealAsync(VisualElement view, double offset)
    {
        if (!_motionActive || !AnimationsEnabled())
            return;
        view.CancelAnimations();
        view.Opacity = 0;
        view.TranslationY = offset;
        try
        {
            await Task.WhenAll(
                view.FadeToAsync(1, 180, Easing.CubicOut),
                view.TranslateToAsync(0, 0, 180, Easing.CubicOut));
        }
        catch (OperationCanceledException)
        {
            // Leaving the page or toggling panels aborts the current transition.
        }
    }

    private static void ResetMotion(VisualElement view)
    {
        view.CancelAnimations();
        view.Opacity = 1;
        view.TranslationY = 0;
    }

    private static bool AnimationsEnabled()
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            return Android.Animation.ValueAnimator.AreAnimatorsEnabled();
#endif
        return true;
    }
}
