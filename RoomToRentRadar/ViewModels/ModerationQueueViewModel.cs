using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;

namespace RoomToRentRadar.ViewModels;

public enum ModerationQueueState
{
    Loading,
    Content,
    Empty,
    SignedOut,
    Denied,
    Error
}

public partial class ModerationQueueViewModel : ObservableObject
{
    private const int PageSize = 20;

    private readonly IModerationClient _client;
    private readonly AuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly IAlertPrompt _alertPrompt;
    private readonly IErrorHandler _errorHandler;

    private string? _nextCursor;

    public ObservableCollection<ModerationQueueItem> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingState))]
    [NotifyPropertyChangedFor(nameof(IsContentState))]
    [NotifyPropertyChangedFor(nameof(IsEmptyState))]
    [NotifyPropertyChangedFor(nameof(IsSignedOutState))]
    [NotifyPropertyChangedFor(nameof(IsDeniedState))]
    [NotifyPropertyChangedFor(nameof(IsErrorState))]
    public partial ModerationQueueState State { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasMore { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; set; }

    public ModerationQueueViewModel(
        IModerationClient client,
        AuthService authService,
        INavigationService navigationService,
        IAlertPrompt alertPrompt,
        IErrorHandler errorHandler)
    {
        _client = client;
        _authService = authService;
        _navigationService = navigationService;
        _alertPrompt = alertPrompt;
        _errorHandler = errorHandler;
        State = ModerationQueueState.Loading;
        Message = string.Empty;
    }

    public bool IsLoadingState => State == ModerationQueueState.Loading;

    public bool IsContentState => State == ModerationQueueState.Content;

    public bool IsEmptyState => State == ModerationQueueState.Empty;

    public bool IsSignedOutState => State == ModerationQueueState.SignedOut;

    public bool IsDeniedState => State == ModerationQueueState.Denied;

    public bool IsErrorState => State == ModerationQueueState.Error;

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    [RelayCommand]
    private Task SignInAsync() => _navigationService.GoToLoginAsync();

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Message = string.Empty;
        Items.Clear();
        _nextCursor = null;
        HasMore = false;
        State = ModerationQueueState.Loading;
        IsBusy = true;
        try
        {
            var role = await _authService.GetCurrentRoleAsync(cancellationToken);
            if (role is null)
            {
                State = ModerationQueueState.SignedOut;
                return;
            }

            if (role is not (AppUserRole.Moderator or AppUserRole.Admin))
            {
                State = ModerationQueueState.Denied;
                return;
            }

            await LoadNextPageAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!TryHandleAccessFailure(ex))
            {
                State = ModerationQueueState.Error;
                Message = "Không thể tải hàng đợi kiểm duyệt. Kiểm tra kết nối và thử lại.";
                _errorHandler.HandleError(ex);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadMoreAsync(CancellationToken cancellationToken)
    {
        if (!HasMore || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await LoadNextPageAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!TryHandleAccessFailure(ex))
            {
                Message = "Không thể tải thêm tin chờ duyệt.";
                _errorHandler.HandleError(ex);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ApproveAsync(ModerationQueueItem item, CancellationToken cancellationToken)
    {
        var confirmed = await _alertPrompt.ConfirmAsync(
            "Duyệt tin đăng",
            $"Duyệt tin “{item.Title}” để hiển thị công khai?",
            "Duyệt tin",
            "Hủy",
            cancellationToken);
        if (!confirmed)
        {
            return;
        }

        Message = string.Empty;
        try
        {
            await _client.ApproveAsync(item.Id, cancellationToken);
            await RemoveApprovedAsync(item, "Đã duyệt tin", cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            Message = "Tin này không còn ở trạng thái chờ duyệt. Tải lại hàng đợi để xem trạng thái mới.";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            await RemoveApprovedAsync(item, "Tin này không còn tồn tại", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!TryHandleAccessFailure(ex))
            {
                Message = "Không thể duyệt tin. Kiểm tra kết nối và thử lại.";
                _errorHandler.HandleError(ex);
            }
        }
    }

    private bool TryHandleAccessFailure(Exception exception)
    {
        if (exception is not ApiException apiException)
        {
            return false;
        }

        switch (apiException.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                State = ModerationQueueState.SignedOut;
                return true;
            case HttpStatusCode.Forbidden:
                State = ModerationQueueState.Denied;
                return true;
            default:
                return false;
        }
    }

    private async Task LoadNextPageAsync(CancellationToken cancellationToken)
    {
        var page = await _client.GetPendingListingsAsync(PageSize, _nextCursor, cancellationToken);
        foreach (var listing in page.Items)
        {
            Items.Add(new ModerationQueueItem(listing));
        }

        _nextCursor = page.Pagination.NextCursor;
        HasMore = page.Pagination.HasMore && _nextCursor is not null;
        State = Items.Count == 0 ? ModerationQueueState.Empty : ModerationQueueState.Content;
    }

    private async Task RemoveApprovedAsync(
        ModerationQueueItem item,
        string message,
        CancellationToken cancellationToken)
    {
        Items.Remove(item);
        Message = $"{message} “{item.Title}”.";
        if (Items.Count > 0)
        {
            return;
        }

        if (HasMore)
        {
            await LoadNextPageAsync(cancellationToken);
        }
        else
        {
            State = ModerationQueueState.Empty;
        }
    }
}
