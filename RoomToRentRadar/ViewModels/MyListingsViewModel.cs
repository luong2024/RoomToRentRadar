using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.ViewModels;

public partial class MyListingsViewModel : ObservableObject
{
    private readonly IListingManagementClient _client;
    private readonly AuthService _auth;
    private readonly INavigationService _navigation;
    private readonly IAlertPrompt _alerts;
    private readonly SynchronizationContext? _uiContext;
    private readonly List<OwnedListingItem> _allListings = [];
    private bool _confirming;
    private bool _isAuthSubscribed;

    public MyListingsViewModel(
        IListingManagementClient client,
        AuthService auth,
        INavigationService navigation,
        IAlertPrompt alerts)
    {
        _client = client;
        _auth = auth;
        _navigation = navigation;
        _alerts = alerts;
        _uiContext = SynchronizationContext.Current;
        StatusFilters =
        [
            new OwnedListingFilterOption(null, "Tất cả", SelectStatusFilter) { IsSelected = true },
            new OwnedListingFilterOption(ListingStatus.Published, "Đã duyệt", SelectStatusFilter),
            new OwnedListingFilterOption(ListingStatus.PendingReview, "Chờ duyệt", SelectStatusFilter),
            new OwnedListingFilterOption(ListingStatus.Hidden, "Đã gỡ đăng", SelectStatusFilter)
        ];
    }

    public void AttachAuthSession()
    {
        if (_isAuthSubscribed) return;
        _auth.SessionChanged += OnSessionChanged;
        _isAuthSubscribed = true;
    }

    public void DetachAuthSession()
    {
        if (!_isAuthSubscribed) return;
        _auth.SessionChanged -= OnSessionChanged;
        _isAuthSubscribed = false;
    }

    public ObservableCollection<OwnedListingItem> Listings { get; } = [];
    public IReadOnlyList<OwnedListingFilterOption> StatusFilters { get; }

    [ObservableProperty] public partial ListingStatus? SelectedStatusFilter { get; set; }
    [ObservableProperty] public partial bool IsAuthenticated { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty] public partial string Message { get; set; } = string.Empty;

    public bool HasAnyListings => _allListings.Count > 0;
    public bool NoOwnedListings => _allListings.Count == 0;
    public bool IsFilteredEmpty => _allListings.Count > 0 && Listings.Count == 0;
    public bool ShowEmptyState => Listings.Count == 0;

    public string DashboardSummaryText
    {
        get
        {
            var total = _allListings.Count;
            var published = _allListings.Count(item => item.Status == ListingStatus.Published);
            var pending = _allListings.Count(item => item.Status == ListingStatus.PendingReview);
            return $"Tổng {total} phòng · {published} đã duyệt · {pending} chờ duyệt";
        }
    }

    partial void OnSelectedStatusFilterChanged(ListingStatus? value)
    {
        SyncFilterSelection(value);
        RebuildVisibleListings();
    }

    public void SelectStatusFilter(ListingStatus? status)
    {
        if (SelectedStatusFilter == status)
        {
            SyncFilterSelection(status);
            RebuildVisibleListings();
            return;
        }

        SelectedStatusFilter = status;
    }

    [RelayCommand]
    private Task SignInAsync() => _navigation.GoToLoginAsync();

    [RelayCommand]
    private Task CreateAsync() => _navigation.GoToListingEditorAsync();

    [RelayCommand]
    private Task EditAsync(OwnedListingItem? item)
        => item is { CanEdit: true } ? _navigation.GoToListingEditorAsync(item.Id) : Task.CompletedTask;

    [RelayCommand]
    private Task ManagePhotosAsync(OwnedListingItem? item)
        => item is null ? Task.CompletedTask : _navigation.GoToListingPhotoManagerAsync(item.Id);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SignOutAsync(CancellationToken cancellationToken)
    {
        LoadCommand.Cancel();
        PublishCommand.Cancel();
        UnpublishCommand.Cancel();
        DeleteCommand.Cancel();
        await _auth.SignOutAsync(cancellationToken);
        IsAuthenticated = false;
        _allListings.Clear();
        SelectedStatusFilter = null;
        RefreshDashboardState();
        await _navigation.GoToRoleRootAsync(null);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (IsBusy || _confirming) return;
            IsBusy = true;
            Message = string.Empty;
            try
            {
                IsAuthenticated = await _auth.IsSignedInAsync(cancellationToken);
                if (!IsAuthenticated)
                {
                    _allListings.Clear();
                    RefreshDashboardState();
                    Message = "Đăng nhập để đăng và quản lý tin cho thuê của bạn.";
                    return;
                }

                var listings = await _client.GetMineAsync(cancellationToken);
                _allListings.Clear();
                foreach (var listing in listings)
                {
                    _allListings.Add(new OwnedListingItem(listing));
                }

                RefreshDashboardState();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (ListingManagementException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                IsAuthenticated = false;
                _allListings.Clear();
                RefreshDashboardState();
                Message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
            }
            catch (ListingManagementException ex)
            {
                Message = ex.Message;
            }
            catch (Exception)
            {
                Message = "Không thể tải tin của bạn. Kiểm tra kết nối và thử lại.";
            }
            finally
            {
                IsBusy = false;
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task PublishAsync(OwnedListingItem? item, CancellationToken cancellationToken)
    {
        if (item is null || !item.CanPublish || IsBusy || _confirming) return;
        IsBusy = true;
        try
        {
            var updated = await _client.PublishAsync(item.Id, cancellationToken);
            ReplaceListing(updated);
            Message = "Đã gửi duyệt lại tin đăng.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ListingManagementException ex)
        {
            if (ex.StatusCode == HttpStatusCode.Unauthorized) IsAuthenticated = false;
            Message = ex.Message;
        }
        catch (Exception)
        {
            Message = "Không thể đăng lại tin. Kiểm tra kết nối và thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task UnpublishAsync(OwnedListingItem? item, CancellationToken cancellationToken)
    {
        if (item is null || !item.CanUnpublish || IsBusy || _confirming) return;
        bool confirmed;
        _confirming = true;
        try
        {
            confirmed = await _alerts.ConfirmAsync(
                "Gỡ đăng tin?",
                "Tin sẽ không còn hiển thị công khai. Bạn có thể đăng lại bất cứ lúc nào.",
                "Gỡ đăng",
                "Hủy",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            _confirming = false;
        }

        if (!confirmed || IsBusy) return;
        IsBusy = true;
        try
        {
            var updated = await _client.UnpublishAsync(item.Id, cancellationToken);
            ReplaceListing(updated);
            Message = "Đã gỡ đăng tin.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ListingManagementException ex)
        {
            if (ex.StatusCode == HttpStatusCode.Unauthorized) IsAuthenticated = false;
            Message = ex.Message;
        }
        catch (Exception)
        {
            Message = "Không thể gỡ đăng tin. Kiểm tra kết nối và thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task DeleteAsync(OwnedListingItem? item, CancellationToken cancellationToken)
    {
        if (item is null || IsBusy || _confirming) return;
        bool confirmed;
        _confirming = true;
        try
        {
            confirmed = await _alerts.ConfirmAsync(
                "Xóa phòng cho thuê?",
                "Tin đăng và toàn bộ hình ảnh sẽ bị xóa vĩnh viễn. Thao tác này không thể hoàn tác.",
                "Xóa tin",
                "Hủy",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            _confirming = false;
        }

        if (!confirmed || IsBusy) return;
        IsBusy = true;
        try
        {
            await _client.DeleteAsync(item.Id, cancellationToken);
            _allListings.RemoveAll(existing => existing.Id == item.Id);
            RefreshDashboardState();
            Message = "Đã xóa tin đăng.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ListingManagementException ex)
        {
            if (ex.StatusCode == HttpStatusCode.Unauthorized) IsAuthenticated = false;
            Message = ex.Message;
        }
        catch (Exception)
        {
            Message = "Không thể xóa tin đăng. Kiểm tra kết nối và thử lại.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReplaceListing(ManagedListingDto updated)
    {
        var replacement = new OwnedListingItem(updated);
        var index = _allListings.FindIndex(existing => existing.Id == updated.Id);
        if (index >= 0)
        {
            _allListings[index] = replacement;
        }
        else
        {
            _allListings.Insert(0, replacement);
        }

        RefreshDashboardState();
    }

    private void RefreshDashboardState()
    {
        SyncFilterSelection(SelectedStatusFilter);
        foreach (var filter in StatusFilters)
        {
            filter.Count = filter.Status is { } status
                ? _allListings.Count(item => item.Status == status)
                : _allListings.Count;
        }

        RebuildVisibleListings();
        OnPropertyChanged(nameof(HasAnyListings));
        OnPropertyChanged(nameof(NoOwnedListings));
        OnPropertyChanged(nameof(DashboardSummaryText));
    }

    private void SyncFilterSelection(ListingStatus? selectedStatus)
    {
        foreach (var filter in StatusFilters)
        {
            filter.IsSelected = filter.Status == selectedStatus;
        }
    }

    private void RebuildVisibleListings()
    {
        var filtered = (SelectedStatusFilter is { } status
            ? _allListings.Where(item => item.Status == status)
            : _allListings).ToArray();

        if (!Listings.SequenceEqual(filtered))
        {
            Listings.Clear();
            foreach (var item in filtered)
            {
                Listings.Add(item);
            }
        }

        OnPropertyChanged(nameof(IsFilteredEmpty));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        if (_uiContext is not null && SynchronizationContext.Current != _uiContext)
        {
            _uiContext.Post(_ => HandleSessionChanged(), null);
            return;
        }

        HandleSessionChanged();
    }

    private void HandleSessionChanged()
    {
        if (_auth.CurrentRole == AppUserRole.Landlord)
        {
            IsAuthenticated = true;
            if (!IsBusy && !_confirming && LoadCommand.CanExecute(null))
            {
                LoadCommand.Execute(null);
            }
        }
        else if (!_auth.IsSignedIn)
        {
            IsAuthenticated = false;
            _allListings.Clear();
            SelectedStatusFilter = null;
            RefreshDashboardState();
        }
    }
}
