using System.ComponentModel;
using RoomToRentRadar.ViewModels;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Views;

public partial class RoomDetailPage : ContentPage
{
    private readonly RoomDetailViewModel _viewModel;
    private readonly RoomDetailTravelViewModel _travelViewModel;
    private bool _observing;
    private bool _entryAnimated;

    public RoomDetailPage(RoomDetailViewModel viewModel, RoomDetailTravelViewModel travelViewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        TravelSection.BindingContext = _travelViewModel = travelViewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_observing)
        {
            _observing = true;
            _entryAnimated = false;
            _viewModel.PropertyChanged += OnDetailStateChanged;
            _viewModel.Report.PropertyChanged += OnReportStateChanged;
        }

        Shell.SetTabBarIsVisible(this, _viewModel.IsTabBarVisible);

        if (_viewModel.State == RoomDetailState.Initial && _viewModel.LoadCommand.CanExecute(null))
        {
            _viewModel.LoadCommand.Execute(null);
        }
        else if (_viewModel.State == RoomDetailState.Content)
        {
            // Returning from the sign-in page must reveal the phone without a reload,
            // and the heart must match the account's favourites on this device or another.
            if (_viewModel.RefreshContactCommand.CanExecute(null))
                _viewModel.RefreshContactCommand.Execute(null);
            if (_viewModel.RefreshFavouriteCommand.CanExecute(null))
                _viewModel.RefreshFavouriteCommand.Execute(null);
        }
        SyncTravelSection();
        AnimateContent();
    }

    private void OnDetailStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomDetailViewModel.State))
            Dispatcher.Dispatch(() =>
            {
                SyncTravelSection();
                AnimateContent();
            });
    }

    private void OnReportStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RoomReportViewModel.IsOpen))
            return;

        Dispatcher.Dispatch(() =>
            Shell.SetTabBarIsVisible(this, _viewModel.IsTabBarVisible));
    }

    private void SyncTravelSection()
    {
        if (_viewModel.State == RoomDetailState.Content && _viewModel.Listing is { } listing)
            _travelViewModel.Initialize(listing.Id, listing.Lat, listing.Lng);
    }

    private async void AnimateContent()
    {
        if (!_observing || _entryAnimated || _viewModel.State != RoomDetailState.Content)
            return;
        _entryAnimated = true;
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && !Android.Animation.ValueAnimator.AreAnimatorsEnabled())
            return;
#endif
        DetailContent.CancelAnimations();
        DetailContent.Opacity = 0;
        DetailContent.TranslationY = 8;
        await Task.WhenAll(DetailContent.FadeToAsync(1, 200, Easing.CubicOut),
            DetailContent.TranslateToAsync(0, 0, 200, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        _observing = false;
        _viewModel.PropertyChanged -= OnDetailStateChanged;
        _viewModel.Report.PropertyChanged -= OnReportStateChanged;
        Shell.SetTabBarIsVisible(this, true);
        _travelViewModel.Cancel();
        DetailContent.CancelAnimations();
        DetailContent.Opacity = 1;
        DetailContent.TranslationY = 0;
        base.OnDisappearing();
    }

    private async void OnSimilarRoomTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is ListingSummaryDto listing
            && _viewModel.OpenSimilarRoomCommand.CanExecute(listing))
        {
            await _viewModel.OpenSimilarRoomCommand.ExecuteAsync(listing);
        }
    }
}
