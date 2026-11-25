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
using RoomToRentRadar.Shared.Listings;

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
    private readonly List<ModerationQueueItem> _sessionApproved = [];

    private string? _nextCursor;
    private CancellationTokenSource? _activeLoadCts;
    private int _loadGeneration;
    private bool _approving;

    public ObservableCollection<ModerationQueueItem> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingState))]
    [NotifyPropertyChangedFor(nameof(IsContentState))]
    [NotifyPropertyChangedFor(nameof(IsEmptyState))]
    [NotifyPropertyChangedFor(nameof(IsSignedOutState))]
    [NotifyPropertyChangedFor(nameof(CanSignOut))]
    [NotifyPropertyChangedFor(nameof(IsDeniedState))]
    [NotifyPropertyChangedFor(nameof(IsErrorState))]
    [NotifyPropertyChangedFor(nameof(ShowFilterTabs))]
    public partial ModerationQueueState State { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPendingTab))]
    [NotifyPropertyChangedFor(nameof(IsPublishedTab))]
    [NotifyPropertyChangedFor(nameof(ShowFilterTabs))]
    [NotifyPropertyChangedFor(nameof(EmptyTitleText))]
    [NotifyPropertyChangedFor(nameof(EmptySubtitleText))]
    [NotifyPropertyChangedFor(nameof(QueueSummaryText))]
    [NotifyPropertyChangedFor(nameof(LoadMoreText))]
    public partial ListingStatus SelectedStatus { get; set; } = ListingStatus.PendingReview;

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

    public bool CanSignOut => !IsSignedOutState;

    public bool IsDeniedState => State == ModerationQueueState.Denied;

    public bool IsErrorState => State == ModerationQueueState.Error;

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    public bool IsPendingTab => SelectedStatus == ListingStatus.PendingReview;

    public bool IsPublishedTab => SelectedStatus == ListingStatus.Published;

    public bool ShowFilterTabs => State is not (ModerationQueueState.SignedOut or ModerationQueueState.Denied);

    public string QueueSummaryText => IsPendingTab
        ? $"{Items.Count} phòng đang chờ phê duyệt"
        : $"{Items.Count} phòng đã duyệt công khai";

    public string EmptyTitleText => IsPendingTab
        ? "Không có tin nào chờ duyệt"
        : "Chưa có phòng nào đã duyệt";

    public string EmptySubtitleText => IsPendingTab
        ? "Tất cả phòng mới do chủ trọ gửi lên sẽ hiển thị đầy đủ hình ảnh, thông tin và vị trí tại đây."
        : "Các phòng sau khi được duyệt sẽ xuất hiện tại mục này.";

    public string LoadMoreText => IsPendingTab
        ? "Tải thêm tin chờ duyệt"
        : "Tải thêm phòng đã duyệt";

    [RelayCommand]
    private Task SignInAsync() => _navigationService.GoToLoginAsync();

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SelectPendingTabAsync(CancellationToken cancellationToken)
    {
        if (SelectedStatus == ListingStatus.PendingReview)
        {
            return;
        }

        SelectedStatus = ListingStatus.PendingReview;
        await ReloadQueueAsync(cancellationToken);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SelectPublishedTabAsync(CancellationToken cancellationToken)
    {
        if (SelectedStatus == ListingStatus.Published)
        {
            return;
        }

        SelectedStatus = ListingStatus.Published;
        await ReloadQueueAsync(cancellationToken);
    }

    [RelayCommand]
    private Task OpenLocationMapAsync(ModerationQueueItem? item)
        => item is { HasCoordinates: true }
            ? _navigationService.GoToDirectionsAsync(item.Lat, item.Lng, null, null)
            : Task.CompletedTask;

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SignOutAsync(CancellationToken cancellationToken)
    {
        CancelActiveLoads();
        _loadGeneration++;
        IsBusy = false;
        await _authService.SignOutAsync(cancellationToken);
        _sessionApproved.Clear();
        Items.Clear();
        SelectedStatus = ListingStatus.PendingReview;
        OnPropertyChanged(nameof(QueueSummaryText));
        State = ModerationQueueState.SignedOut;
        await _navigationService.GoToRoleRootAsync(null);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task LoadAsync(CancellationToken cancellationToken)
        => ReloadQueueAsync(cancellationToken);

    private async Task ReloadQueueAsync(CancellationToken cancellationToken)
    {
        CancelActiveLoads();
        var generation = ++_loadGeneration;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeLoadCts = linkedCts;
        var token = linkedCts.Token;

        Message = string.Empty;
        Items.Clear();
        OnPropertyChanged(nameof(QueueSummaryText));
        _nextCursor = null;
        HasMore = false;
        State = ModerationQueueState.Loading;
        IsBusy = true;
        try
        {
            var role = await _authService.GetCurrentRoleAsync(token);
            if (generation != _loadGeneration)
            {
                return;
            }

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

            await LoadNextPageAsync(generation, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            if (!TryHandleAccessFailure(ex))
            {
                State = ModerationQueueState.Error;
                Message = "Không thể tải hàng đợi kiểm duyệt. Kiểm tra kết nối và thử lại.";
                _errorHandler.HandleError(ex);
            }
        }
        finally
        {
            if (ReferenceEquals(_activeLoadCts, linkedCts))
            {
                _activeLoadCts = null;
            }

            if (generation == _loadGeneration)
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadMoreAsync(CancellationToken cancellationToken)
    {
        if (!HasMore || IsBusy || _approving)
        {
            return;
        }

        var generation = _loadGeneration;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeLoadCts = linkedCts;
        var token = linkedCts.Token;
        IsBusy = true;
        try
        {
            await LoadNextPageAsync(generation, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            if (!TryHandleAccessFailure(ex))
            {
                Message = "Không thể tải thêm tin chờ duyệt.";
                _errorHandler.HandleError(ex);
            }
        }
        finally
        {
            if (ReferenceEquals(_activeLoadCts, linkedCts))
            {
                _activeLoadCts = null;
            }

            if (generation == _loadGeneration)
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ApproveAsync(ModerationQueueItem? item, CancellationToken cancellationToken)
    {
        if (item is null || !item.IsPending || _approving || IsBusy)
        {
            return;
        }

        var generation = _loadGeneration;
        _approving = true;
        try
        {
            var confirmed = await _alertPrompt.ConfirmAsync(
                "Duyệt tin đăng",
                $"Duyệt tin “{item.Title}” để hiển thị công khai?",
                "Duyệt tin",
                "Hủy",
                cancellationToken);
            if (!confirmed || generation != _loadGeneration)
            {
                return;
            }

            IsBusy = true;
            Message = string.Empty;
            try
            {
                await _client.ApproveAsync(item.Id, cancellationToken);
                var approvedSnapshot = item.ToApprovedSnapshot(DateTime.UtcNow);
                _sessionApproved.RemoveAll(existing => existing.Id == item.Id);
                _sessionApproved.Insert(0, approvedSnapshot);
                if (generation != _loadGeneration)
                {
                    return;
                }

                await RemoveApprovedAsync(item, "Đã duyệt tin", generation, cancellationToken);
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                Message = "Tin này không còn ở trạng thái chờ duyệt. Tải lại hàng đợi để xem trạng thái mới.";
            }
            catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                if (generation != _loadGeneration)
                {
                    return;
                }

                await RemoveApprovedAsync(item, "Tin này không còn tồn tại", generation, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (!TryHandleAccessFailure(ex))
                {
                    Message = "Không thể duyệt tin. Kiểm tra kết nối và thử lại.";
                    _errorHandler.HandleError(ex);
                }
            }
            finally
            {
                if (generation == _loadGeneration)
                {
                    IsBusy = false;
                }
            }
        }
        finally
        {
            _approving = false;
        }
    }

    private void CancelActiveLoads()
    {
        LoadMoreCommand.Cancel();
        try
        {
            _activeLoadCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _activeLoadCts = null;
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

    private async Task LoadNextPageAsync(int generation, CancellationToken cancellationToken)
    {
        var isFirstPage = _nextCursor is null;
        var page = SelectedStatus == ListingStatus.PendingReview
            ? await _client.GetPendingListingsAsync(PageSize, _nextCursor, cancellationToken)
            : await _client.GetListingsAsync(SelectedStatus, PageSize, _nextCursor, cancellationToken);
        if (generation != _loadGeneration)
        {
            return;
        }

        var existingIds = Items.Select(existing => existing.Id).ToHashSet();

        if (isFirstPage && SelectedStatus == ListingStatus.Published && _sessionApproved.Count > 0)
        {
            var serverById = page.Items.ToDictionary(dto => dto.Id);
            foreach (var approved in _sessionApproved)
            {
                if (!existingIds.Add(approved.Id))
                {
                    continue;
                }

                Items.Add(serverById.TryGetValue(approved.Id, out var serverDto)
                    ? new ModerationQueueItem(serverDto)
                    : approved);
            }
        }

        foreach (var listing in page.Items)
        {
            if (existingIds.Add(listing.Id))
            {
                Items.Add(new ModerationQueueItem(listing));
            }
        }

        _nextCursor = page.Pagination.NextCursor;
        HasMore = page.Pagination.HasMore && _nextCursor is not null;
        State = Items.Count == 0 ? ModerationQueueState.Empty : ModerationQueueState.Content;
        OnPropertyChanged(nameof(QueueSummaryText));
    }

    private async Task RemoveApprovedAsync(
        ModerationQueueItem item,
        string message,
        int generation,
        CancellationToken cancellationToken)
    {
        Items.Remove(item);
        OnPropertyChanged(nameof(QueueSummaryText));
        Message = $"{message} “{item.Title}”.";
        if (Items.Count > 0)
        {
            return;
        }

        if (HasMore)
        {
            await LoadNextPageAsync(generation, cancellationToken);
        }
        else
        {
            State = ModerationQueueState.Empty;
        }
    }
}
