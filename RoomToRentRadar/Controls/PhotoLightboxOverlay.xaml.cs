using System.ComponentModel;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Controls;

/// <summary>
/// Full-screen photo viewer used by the room detail page. It owns the neighbouring
/// image preloading so the carousel shows the next photo without a visible fetch.
/// </summary>
public partial class PhotoLightboxOverlay : ContentView
{
    private PhotoLightboxViewModel? _viewModel;

    public PhotoLightboxOverlay()
    {
        InitializeComponent();
        BindingContextChanged += OnOverlayBindingContextChanged;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            // The overlay left the visual tree: never let the ViewModel keep this
            // view (and its images) alive through PropertyChanged.
            DetachViewModel();
            return;
        }

        AttachViewModel(BindingContext as PhotoLightboxViewModel);
        ApplyOpenState();
    }

    private void OnOverlayBindingContextChanged(object? sender, EventArgs e)
        => AttachViewModel(BindingContext as PhotoLightboxViewModel);

    private void AttachViewModel(PhotoLightboxViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
            return;

        DetachViewModel();

        if (viewModel is null)
            return;

        _viewModel = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyOpenState();

        if (viewModel.IsOpen)
            PanoramaHost.EnsureLoaded();

        PreloadNeighbours();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;

        switch (e.PropertyName)
        {
            case nameof(PhotoLightboxViewModel.IsOpen):
                ApplyOpenState();

                if (_viewModel.IsOpen)
                {
                    PanoramaHost.EnsureLoaded();
                    PreloadNeighbours();
                }
                else
                {
                    ReleaseViewer();
                }

                break;

            case nameof(PhotoLightboxViewModel.CurrentIndex):
                PreloadNeighbours();
                break;
        }
    }

    private void ApplyOpenState()
    {
        var isOpen = _viewModel?.IsOpen == true;
        OverlayRoot.Opacity = isOpen ? 1d : 0d;
        OverlayRoot.InputTransparent = !isOpen;
        ApplyPlatformHitTest(isOpen);
    }

#if WINDOWS
    private void ApplyPlatformHitTest(bool isOpen)
    {
        // dotnet/maui#21384: InputTransparent is not applied on Windows, so the
        // closed overlay kept swallowing every pointer event on the page behind.
        // Both panels must opt out: the ContentView's own native panel stays
        // hit-testable even when only the inner Grid is disabled.
        if (Handler?.PlatformView is Microsoft.UI.Xaml.UIElement platformView)
            platformView.IsHitTestVisible = isOpen;

        if (OverlayRoot.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement platformRoot)
            platformRoot.IsHitTestVisible = isOpen;
    }
#else
    private void ApplyPlatformHitTest(bool isOpen)
    {
    }
#endif

    private void PreloadNeighbours()
    {
        if (_viewModel is null || !_viewModel.IsOpen)
            return;

        var photos = _viewModel.GetPreloadWindow();

        RunOnUiThread(() =>
        {
            PreloadHost.Children.Clear();

            foreach (var photo in photos)
            {
                // A transparent 1x1 image is enough for UriImageSource to fetch and
                // cache the neighbour before the carousel scrolls to it.
                PreloadHost.Children.Add(new Image
                {
                    Source = photo.FullSource,
                    WidthRequest = 1,
                    HeightRequest = 1,
                    Opacity = 0,
                    InputTransparent = true
                });
            }
        });
    }

    private void ReleaseViewer()
    {
        RunOnUiThread(() =>
        {
            PreloadHost.Children.Clear();
            PanoramaHost.Reset();
        });
    }

    private void DetachViewModel()
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = null;
    }

    private void RunOnUiThread(Action action)
    {
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(action);
        else
            action();
    }
}
