using System.ComponentModel;
using RoomToRentRadar.Features.RoomDetail.ViewModels;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.RoomDetail.Views;

public partial class RoomDetailPage : ContentPage
{
    private readonly RoomDetailViewModel _viewModel;
    private bool _observing;
    private bool _entryAnimated;

    public RoomDetailPage(RoomDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_observing)
        {
            _observing = true;
            _entryAnimated = false;
            _viewModel.PropertyChanged += OnDetailStateChanged;
        }

        if (_viewModel.State == RoomDetailState.Initial && _viewModel.LoadCommand.CanExecute(null))
        {
            _viewModel.LoadCommand.Execute(null);
        }
        else if (_viewModel.State == RoomDetailState.Content
                 && _viewModel.RefreshContactCommand.CanExecute(null))
        {
            // Returning from the sign-in page must reveal the phone without a reload.
            _viewModel.RefreshContactCommand.Execute(null);
        }
        AnimateContent();
    }

    private void OnDetailStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomDetailViewModel.State))
            Dispatcher.Dispatch(AnimateContent);
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
