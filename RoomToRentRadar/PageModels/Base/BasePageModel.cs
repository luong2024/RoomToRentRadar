using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.PageModels.Base;

public partial class BasePageModel : ObservableObject
{
    private Func<Task<bool>>? _lastLoad;
    private bool _lastIsRefresh;
    private Func<Task<bool>>? _pendingLoad;
    private bool _pendingIsRefresh;
    private bool _isRunning;
    private Exception? _lastException;

    [ObservableProperty]
    public partial ViewState State { get; set; } = ViewState.Loading;

    [ObservableProperty]
    public partial bool HasContent { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    public BasePageModel()
    {
        _ = new WeakLocalizationSubscription(this);
    }

    public async Task RunAsync(Func<Task<bool>> load, bool isRefresh = false)
    {
        _lastLoad = load;
        _lastIsRefresh = isRefresh;

        // Never drop a load: keep only the latest request and run it once the current one finishes (US04 AC2).
        if (_isRunning)
        {
            _pendingLoad = load;
            _pendingIsRefresh = isRefresh;
            return;
        }

        _isRunning = true;
        var previousState = State;

        try
        {
            if (isRefresh)
            {
                IsRefreshing = true;
            }
            else
            {
                IsLoading = true;
                State = ViewState.Loading;
            }

            var hasData = await load();

            HasContent = hasData;
            _lastException = null;
            ErrorMessage = string.Empty;
            State = hasData ? ViewState.Content : ViewState.Empty;
        }
        catch (OperationCanceledException)
        {
            State = previousState;
        }
        catch (Exception exception)
        {
            _lastException = exception;
            ErrorMessage = ErrorMessageMapper.Map(exception, null);
            State = exception is ApiException { IsNetworkError: true } && HasContent
                ? ViewState.Offline
                : ViewState.Error;
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
            _isRunning = false;
        }

        if (_pendingLoad is not null)
        {
            var pendingLoad = _pendingLoad;
            var pendingIsRefresh = _pendingIsRefresh;
            _pendingLoad = null;
            _pendingIsRefresh = false;
            await RunAsync(pendingLoad, pendingIsRefresh);
        }
    }

    [RelayCommand]
    private Task RetryAsync() =>
        _lastLoad is null ? Task.CompletedTask : RunAsync(_lastLoad, _lastIsRefresh);

    protected virtual void OnLanguageChanged()
    {
    }

    private void OnLocalizationChanged()
    {
        if (_lastException is not null)
            ErrorMessage = ErrorMessageMapper.Map(_lastException, null);

        OnLanguageChanged();
    }

    private sealed class WeakLocalizationSubscription
    {
        private readonly WeakReference<BasePageModel> _target;

        public WeakLocalizationSubscription(BasePageModel target)
        {
            _target = new WeakReference<BasePageModel>(target);
            LocalizedStrings.Instance.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_target.TryGetTarget(out var target))
                target.OnLocalizationChanged();
            else
                LocalizedStrings.Instance.PropertyChanged -= OnPropertyChanged;
        }
    }
}
