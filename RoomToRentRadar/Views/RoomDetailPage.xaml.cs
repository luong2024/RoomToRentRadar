using System.ComponentModel;
using System.Globalization;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using RoomToRentRadar.Helpers;
using RoomToRentRadar.Shared.Listings;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Views;

public partial class RoomDetailPage : ContentPage
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private readonly RoomDetailViewModel _viewModel;
    private readonly RoomDetailTravelViewModel _travelViewModel;
    private bool _observing;
    private bool _entryAnimated;

    public RoomDetailPage(RoomDetailViewModel viewModel, RoomDetailTravelViewModel travelViewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        TravelSection.BindingContext = _travelViewModel = travelViewModel;
        ReportBottomSheet.BindingContext = viewModel.Report;
        ReportBottomSheet.StateChanged += OnReportBottomSheetStateChanged;
#if WINDOWS
        Loaded += OnPageLoadedForKeyboard;
        Unloaded += OnPageUnloadedForKeyboard;
#endif
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.Lightbox.IsOpen)
        {
            _viewModel.Lightbox.CloseCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if WINDOWS
        AttachWindowsKeyboard();
#endif
        if (!_observing)
        {
            _observing = true;
            _entryAnimated = false;
            _viewModel.PropertyChanged += OnDetailStateChanged;
            _viewModel.Report.PropertyChanged += OnReportStateChanged;
        }

        Shell.SetTabBarIsVisible(this, false);

        if (_viewModel.State == RoomDetailState.Initial && _viewModel.LoadCommand.CanExecute(null))
        {
            _viewModel.LoadCommand.Execute(null);
        }
        else if (_viewModel.State == RoomDetailState.Content)
        {
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
        {
            Dispatcher.Dispatch(() =>
            {
                SyncTravelSection();
                AnimateContent();
            });
        }
        else if (e.PropertyName == nameof(RoomDetailViewModel.IsFavourite))
        {
            Dispatcher.Dispatch(() => _ = MotionEffects.PulsePopAsync(FavouriteButtonBorder));
        }
    }

    private void OnReportStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RoomReportViewModel.IsOpen))
            return;

        Dispatcher.Dispatch(() =>
        {
            if (_viewModel.Report.IsOpen && !ReportBottomSheet.IsOpen)
                ReportBottomSheet.Show();
            else if (!_viewModel.Report.IsOpen && ReportBottomSheet.IsOpen)
                ReportBottomSheet.Close();
        });
    }

    private void OnReportBottomSheetStateChanged(object? sender, Syncfusion.Maui.Toolkit.BottomSheet.StateChangedEventArgs e)
    {
        if (!ReportBottomSheet.IsOpen && _viewModel.Report.IsOpen)
            _viewModel.Report.CloseCommand.Execute(null);
    }

    private void SyncTravelSection()
    {
        if (_viewModel.State == RoomDetailState.Content && _viewModel.Listing is { } listing)
            _travelViewModel.Initialize(listing.Id, listing.Lat, listing.Lng);
    }

    private void AnimateContent()
    {
        if (!_observing || _entryAnimated || _viewModel.State != RoomDetailState.Content)
            return;
        _entryAnimated = true;
        _ = MotionEffects.FadeSlideInAsync(DetailContent, 12, 240);
    }

    protected override void OnDisappearing()
    {
        _observing = false;
        _viewModel.PropertyChanged -= OnDetailStateChanged;
        _viewModel.Report.PropertyChanged -= OnReportStateChanged;
        _travelViewModel.Cancel();
        DetailContent.CancelAnimations();
        DetailContent.Opacity = 1;
        DetailContent.TranslationY = 0;
#if WINDOWS
        DetachWindowsKeyboard();
#endif
        base.OnDisappearing();
    }

    private async void OnFavouriteTapped(object? sender, TappedEventArgs e)
    {
        MotionEffects.TryLightHaptic();
        if (_viewModel.ToggleFavouriteCommand.CanExecute(null))
            await _viewModel.ToggleFavouriteCommand.ExecuteAsync(null);
    }

    private async void OnShareClicked(object? sender, TappedEventArgs e)
    {
        if (_viewModel.Listing is not { } detail)
            return;

        MotionEffects.TryLightHaptic();
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = detail.Title,
                Text = $"{detail.Title} — {detail.Price.ToString("N0", Vietnamese)} đ/tháng tại {detail.Address}"
            });
        }
        catch
        {
        }
    }

    private async void OnCallClicked(object? sender, EventArgs e)
    {
        var phone = _viewModel.LandlordPhone;
        if (string.IsNullOrWhiteSpace(phone))
            return;

        MotionEffects.TryLightHaptic();
        try
        {
            if (PhoneDialer.Default.IsSupported)
                PhoneDialer.Default.Open(phone);
            else
                await Clipboard.Default.SetTextAsync(phone);
        }
        catch
        {
            try { await Clipboard.Default.SetTextAsync(phone); } catch { }
        }
    }

#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _keyboardRoot;
    private Microsoft.UI.Xaml.Input.KeyEventHandler? _keyboardHandler;

    private void OnPageLoadedForKeyboard(object? sender, EventArgs e) => AttachWindowsKeyboard();

    private void OnPageUnloadedForKeyboard(object? sender, EventArgs e) => DetachWindowsKeyboard();

    private void AttachWindowsKeyboard()
    {
        if (_keyboardRoot is not null || _keyboardHandler is not null)
            return;

        if (Window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow
            || nativeWindow.Content is not Microsoft.UI.Xaml.UIElement root)
        {
            return;
        }

        var keyHandler = new Microsoft.UI.Xaml.Input.KeyEventHandler(OnWindowsKeyDown);
        _keyboardHandler = keyHandler;
        _keyboardRoot = root;

        // handledEventsToo keeps the shortcuts working when a focused child
        // (for example the carousel) already consumed the key.
        root.AddHandler(
            Microsoft.UI.Xaml.UIElement.KeyDownEvent,
            keyHandler,
            handledEventsToo: true);
    }

    private void DetachWindowsKeyboard()
    {
        var root = _keyboardRoot;
        var handler = _keyboardHandler;

        _keyboardRoot = null;
        _keyboardHandler = null;

        if (root is null || handler is null)
            return;

        root.RemoveHandler(Microsoft.UI.Xaml.UIElement.KeyDownEvent, handler);
    }

    private void OnWindowsKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (!_viewModel.Lightbox.IsOpen)
            return;

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left:
                if (_viewModel.Lightbox.PreviousCommand.CanExecute(null))
                    _viewModel.Lightbox.PreviousCommand.Execute(null);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Right:
                if (_viewModel.Lightbox.NextCommand.CanExecute(null))
                    _viewModel.Lightbox.NextCommand.Execute(null);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Escape:
                _viewModel.Lightbox.CloseCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
#endif
}
