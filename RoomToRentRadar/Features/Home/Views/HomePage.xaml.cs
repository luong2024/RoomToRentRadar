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
        var inset = Width >= 720 ? 36 : 16;
        TopBar.Padding = new Thickness(inset, 10);
        // Item padding supplies the gap on every platform, including WinUI's
        // wrapping layout which can lose a column with nonzero item spacing.
        ListingRefresh.Margin = new Thickness(inset - 8, 0, inset - 8, 0);
        HomeChrome.Padding = new Thickness(inset, 10, inset, 8);
        FilterSheet.ContentWidthMode = Width >= 720
            ? Syncfusion.Maui.Toolkit.BottomSheet.BottomSheetContentWidthMode.Custom
            : Syncfusion.Maui.Toolkit.BottomSheet.BottomSheetContentWidthMode.Full;
        FilterSheet.BottomSheetContentWidth = Math.Min(Width, 640);
    }

    private void OnListingsScrolled(object? sender, ScrolledEventArgs e)
    {
        if (sender is not ScrollView scrollView || !_viewModel.CanLoadMore)
            return;
        if (e.ScrollY + scrollView.Height < scrollView.ContentSize.Height - 240)
            return;
        if (_viewModel.LoadMoreCommand.CanExecute(null))
            _viewModel.LoadMoreCommand.Execute(null);
    }

    private async void OnOpenMapClicked(object? sender, EventArgs e)
    {
        _viewModel.CloseSearchCommand.Execute(null);
        await Shell.Current.GoToAsync("//map");
    }

    private async void OnFavouriteHeartClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: HomeListingItem item }
            && _viewModel.ToggleFavouriteCommand.CanExecute(item))
        {
            await _viewModel.ToggleFavouriteCommand.ExecuteAsync(item);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateLayout();
        _viewModel.ActivateFavourites();
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

        if (!_viewModel.HasLoaded && _viewModel.LoadCommand.CanExecute(null))
            _viewModel.LoadCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        _motionActive = false;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.DeactivateFavourites();
        _viewModel.CloseSearchCommand.Execute(null);
        _viewModel.IsFilterPanelOpen = false;
        CampusSearchEntry.Unfocus();
        ResetMotion(HomeChrome);
        base.OnDisappearing();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeViewModel.IsSearchOpen))
            return;
        Dispatcher.Dispatch(() =>
        {
            if (!_motionActive)
                return;
            if (_viewModel.IsSearchOpen)
            {
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(180), () =>
                {
                    if (_motionActive && _viewModel.IsSearchOpen)
                        CampusSearchEntry.Focus();
                });
            }
            else
                CampusSearchEntry.Unfocus();
        });
    }

    private void OnSearchFocused(object? sender, FocusEventArgs e)
    {
        SearchInputBorder.Stroke = (Color)Application.Current!.Resources["Accent"];
        SearchInputBorder.StrokeThickness = 2;
    }

    private void OnSearchUnfocused(object? sender, FocusEventArgs e)
    {
        SearchInputBorder.Stroke = (Color)Application.Current!.Resources[
            Application.Current.RequestedTheme == AppTheme.Dark ? "LineDark" : "Line"];
        SearchInputBorder.StrokeThickness = 1;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsSearchOpen)
        {
            _viewModel.CloseSearchCommand.Execute(null);
            return true;
        }
        if (_viewModel.IsFilterPanelOpen)
        {
            _viewModel.ToggleFilterPanelCommand.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
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
