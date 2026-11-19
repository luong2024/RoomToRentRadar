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
        LocalizedStrings.Instance.PropertyChanged += OnLocalizationChanged;
    }

    public async Task RunAsync(Func<Task<bool>> load, bool isRefresh = false)
    {
        if (_isRunning)
            return;

        _isRunning = true;
        _lastLoad = load;
        _lastIsRefresh = isRefresh;
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
    }

    [RelayCommand]
    private Task RetryAsync() =>
        _lastLoad is null ? Task.CompletedTask : RunAsync(_lastLoad, _lastIsRefresh);

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_lastException is null)
            return;

        ErrorMessage = ErrorMessageMapper.Map(_lastException, null);
    }
}
