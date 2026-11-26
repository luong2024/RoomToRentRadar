using System.Text.Json;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.ViewModels;

namespace RoomToRentRadar.Controls;

/// <summary>
/// Hosts the vendored Pannellum viewer for a single panorama photo.
/// The HybridWebView is created lazily on the first panorama so the detail page
/// never pays for a webview that is not used.
/// </summary>
public partial class Panorama360View : ContentView
{
    private const string ViewerRoot = "wwwroot";
    private const string ViewerPage = "pannellum/viewer.html";
    private const string BridgeViewerReadyType = "viewer-ready";
    private const string BridgeReadyType = "ready";
    private const string BridgeErrorType = "error";

    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan LayoutPollInterval = TimeSpan.FromMilliseconds(50);
    private const int LayoutPollAttempts = 20;

    public static readonly BindableProperty PhotoProperty = BindableProperty.Create(
        nameof(Photo),
        typeof(GalleryPhotoItem),
        typeof(Panorama360View),
        defaultBindingMode: BindingMode.OneWay,
        propertyChanged: OnPhotoPropertyChanged);

    private IPhotosApiClient? _photosApiClient;
    private HybridWebView? _webView;
    private TaskCompletionSource<bool>? _viewerReadySignal;
    private TaskCompletionSource<bool>? _panoramaSignal;
    private CancellationTokenSource? _loadCancellation;
    private GalleryPhotoItem? _activePhoto;

    public Panorama360View()
    {
        InitializeComponent();
    }

    public GalleryPhotoItem? Photo
    {
        get => GetValue(PhotoProperty) as GalleryPhotoItem;
        set => SetValue(PhotoProperty, value);
    }

    /// <summary>
    /// Tears the viewer down. The webview is removed from the visual tree and its
    /// handler is disconnected so the native engine releases the panorama.
    /// </summary>
    public void Reset()
    {
        CancelPendingLoad();
        _activePhoto = null;

        _panoramaSignal?.TrySetResult(false);
        _panoramaSignal = null;
        _viewerReadySignal = null;

        var webView = _webView;
        _webView = null;

        if (webView is null)
        {
            ShowIdle();
            return;
        }

        ViewerHost.Children.Remove(webView);

        // The webview is discarded together with its page, so the raw message
        // subscription goes away with it; no explicit unsubscribe is required.
        _ = DisposeViewerAsync(webView);

        ShowIdle();
    }

    /// <summary>
    /// Re-attaches the viewer for the current photo. <see cref="Reset"/> drops the
    /// webview, so an unchanged <see cref="Photo"/> value would otherwise not
    /// trigger a reload when the lightbox is opened again.
    /// </summary>
    public void EnsureLoaded() => ApplyPhoto(Photo);

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        // A ContentView is created by XAML, so constructor injection is not possible.
        // The photos client is resolved from the handler's MauiContext instead; it is
        // registered as a singleton, so holding the reference is safe.
        if (_photosApiClient is null && Handler?.MauiContext?.Services is { } services)
            _photosApiClient = services.GetService<IPhotosApiClient>();
    }

    private static void OnPhotoPropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
        => ((Panorama360View)bindable).ApplyPhoto(newValue as GalleryPhotoItem);

    private void ApplyPhoto(GalleryPhotoItem? photo)
    {
        if (photo is null || !photo.IsPanorama)
        {
            if (_activePhoto is not null || _webView is not null)
                Reset();

            return;
        }

        if (ReferenceEquals(photo, _activePhoto) && _webView is not null)
            return;

        _activePhoto = photo;
        StartLoad(photo);
    }

    private void StartLoad(GalleryPhotoItem photo)
    {
        CancelPendingLoad();

        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        _ = LoadAsync(photo, cancellation);
    }

    private async Task LoadAsync(GalleryPhotoItem photo, CancellationTokenSource cancellation)
    {
        try
        {
            ShowLoading();

            // IsVisible is toggled by a separate binding from the one that delivers
            // Photo, so let the layout pass settle before the webview is created.
            await WaitForLayoutAsync(cancellation.Token);

            var webView = EnsureWebView();

            // viewer-ready arrives once per webview page load (it may already be done).
            var viewerSignal = _viewerReadySignal;
            if (viewerSignal is not null
                && !await WaitForSignalAsync(viewerSignal, cancellation.Token))
            {
                ShowFallback();
                return;
            }

            var dataUrl = await BuildDataUrlAsync(photo, cancellation.Token);

            cancellation.Token.ThrowIfCancellationRequested();

            var panoramaSignal = _panoramaSignal = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            // Base64 never contains a quote, so the argument is safe to inline.
            await webView.EvaluateJavaScriptAsync($"setPanorama('{dataUrl}')")
                .WaitAsync(HandshakeTimeout, cancellation.Token);

            var loaded = await WaitForSignalAsync(panoramaSignal, cancellation.Token);

            cancellation.Token.ThrowIfCancellationRequested();

            // A newer panorama replaced this one; its own load owns the visible state.
            if (!ReferenceEquals(_panoramaSignal, panoramaSignal))
                return;

            if (loaded)
                ShowReady();
            else
                ShowFallback();
        }
        catch (OperationCanceledException)
        {
            // A newer photo or a closed lightbox superseded this load.
        }
        catch (TimeoutException)
        {
            ShowFallback();
        }
        catch (Exception)
        {
            // The panorama is an enhancement: never let it break the detail page.
            ShowFallback();
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
                _loadCancellation = null;

            cancellation.Dispose();
        }
    }

    private HybridWebView EnsureWebView()
    {
        if (_webView is not null)
            return _webView;

        var webView = new HybridWebView
        {
            HybridRoot = ViewerRoot,
            DefaultFile = ViewerPage,
            BackgroundColor = Colors.Black
        };

        _viewerReadySignal = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        webView.RawMessageReceived += (_, args) => HandleBridgeMessage(args.Message);

        _webView = webView;
        ViewerHost.Children.Add(webView);

        return webView;
    }

    private void HandleBridgeMessage(string? message)
    {
        if (message is not { Length: > 0 })
            return;

        string? type;
        try
        {
            using var document = JsonDocument.Parse(message);
            type = document.RootElement.TryGetProperty("type", out var typeProperty)
                ? typeProperty.GetString()
                : null;
        }
        catch (JsonException)
        {
            return;
        }

        switch (type)
        {
            case BridgeViewerReadyType:
                _viewerReadySignal?.TrySetResult(true);
                break;

            case BridgeReadyType:
                _panoramaSignal?.TrySetResult(true);
                break;

            case BridgeErrorType:
                _panoramaSignal?.TrySetResult(false);
                break;
        }
    }

    private static async Task<bool> WaitForSignalAsync(
        TaskCompletionSource<bool> signal,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(HandshakeTimeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutSource.Token,
            cancellationToken);

        try
        {
            return await signal.Task.WaitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timed out waiting for the bridge; the caller falls back to the still image.
            return false;
        }
    }

    private async Task WaitForLayoutAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < LayoutPollAttempts; attempt++)
        {
            if (Width > 0 && Height > 0)
                return;

            await Task.Delay(LayoutPollInterval, cancellationToken);
        }
    }

    private async Task<string> BuildDataUrlAsync(GalleryPhotoItem photo, CancellationToken cancellationToken)
    {
        var bytes = await ReadPanoramaBytesAsync(photo, cancellationToken)
            ?? throw new InvalidOperationException("The panorama image could not be downloaded.");

        if (bytes.Length == 0)
            throw new InvalidOperationException("The panorama image was empty.");

        return $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}";
    }

    private async Task<byte[]?> ReadPanoramaBytesAsync(
        GalleryPhotoItem photo,
        CancellationToken cancellationToken)
    {
        var cacheDirectory = Path.Combine(FileSystem.CacheDirectory, "panoramas");
        Directory.CreateDirectory(cacheDirectory);
        var cachePath = Path.Combine(cacheDirectory, $"{photo.Id:N}.jpg");

        if (File.Exists(cachePath))
        {
            var cached = await File.ReadAllBytesAsync(cachePath, cancellationToken);
            if (cached.Length > 0)
                return cached;
        }

        var client = _photosApiClient ??= Handler?.MauiContext?.Services?.GetService<IPhotosApiClient>();
        if (client is null)
            return null;

        var bytes = await client.GetMediaBytesAsync(photo.FullUrl, cancellationToken);
        if (bytes is null || bytes.Length == 0)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken);
        }
        catch (Exception)
        {
            // Caching is best effort; the downloaded bytes are still usable.
        }

        return bytes;
    }

    private void CancelPendingLoad()
    {
        var cancellation = _loadCancellation;
        _loadCancellation = null;

        if (cancellation is null)
            return;

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The previous load already finished and released its token source.
        }
    }

    private static async Task DisposeViewerAsync(HybridWebView webView)
    {
        try
        {
            await webView.EvaluateJavaScriptAsync("disposeViewer()");
        }
        catch (Exception)
        {
            // Teardown is best effort; the webview is discarded either way.
        }

        try
        {
            webView.Handler?.DisconnectHandler();
        }
        catch (Exception)
        {
            // The handler may already be disconnected.
        }
    }

    private void ShowIdle() => RunOnUiThread(() =>
    {
        LoadingState.IsVisible = false;
        FallbackState.IsVisible = false;
    });

    private void ShowLoading() => RunOnUiThread(() =>
    {
        LoadingState.IsVisible = true;
        FallbackState.IsVisible = false;
    });

    private void ShowReady() => RunOnUiThread(() =>
    {
        LoadingState.IsVisible = false;
        FallbackState.IsVisible = false;
    });

    private void ShowFallback() => RunOnUiThread(() =>
    {
        FallbackImage.Source = Photo?.FullSource;
        LoadingState.IsVisible = false;
        FallbackState.IsVisible = true;
    });

    private void RunOnUiThread(Action action)
    {
        if (Dispatcher.IsDispatchRequired)
            Dispatcher.Dispatch(action);
        else
            action();
    }
}
