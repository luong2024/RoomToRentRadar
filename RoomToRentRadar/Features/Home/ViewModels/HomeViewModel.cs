using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.Home.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private const int PageSize = 12;

    private readonly IApiClient _apiClient;
    private readonly IErrorHandler _errorHandler;
    private readonly INavigationService _navigationService;
    private readonly HashSet<Guid> _listingIds = new();
    private string? _nextCursor;
    private int _generation;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    public partial bool HasMore { get; set; }

    [ObservableProperty]
    public partial string ResultsText { get; set; }

    [ObservableProperty]
    public partial string EmptyMessage { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial HomeListingItem? SelectedListing { get; set; }

    public HomeViewModel(IApiClient apiClient, IErrorHandler errorHandler, INavigationService navigationService)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
        _navigationService = navigationService;
        ResultsText = "Đang tải phòng trọ…";
        EmptyMessage = "Đang tải phòng trọ…";
        ErrorMessage = string.Empty;
    }

    public ObservableCollection<HomeListingItem> Listings { get; } = new();

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool CanLoadMore => HasMore
        && _nextCursor is not null
        && !IsLoadingMore
        && !HasError
        && Listings.Count > 0;

    partial void OnIsLoadingMoreChanged(bool value) => OnPropertyChanged(nameof(CanLoadMore));

    partial void OnHasMoreChanged(bool value) => OnPropertyChanged(nameof(CanLoadMore));

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CanLoadMore));
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        await LoadFirstPageAsync(cancellationToken);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            IsRefreshing = false;
            return;
        }

        await LoadFirstPageAsync(cancellationToken);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RetryAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || IsLoadingMore)
            return;

        await LoadFirstPageAsync(cancellationToken);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadMoreAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || IsLoadingMore || !CanLoadMore)
            return;

        var generation = _generation;
        IsLoadingMore = true;
        try
        {
            var page = await _apiClient.GetListingsAsync(PageSize, _nextCursor, cancellationToken);
            if (generation != _generation)
                return;

            Append(page.Data);
            _nextCursor = page.Pagination.NextCursor;
            HasMore = page.Pagination.HasMore;
            ResultsText = DescribeResults();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (generation != _generation)
                return;

            ErrorMessage = "Không thể tải thêm phòng. Vui lòng thử lại.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task OpenSelectedListingAsync()
    {
        if (SelectedListing is not { } listing)
            return;

        try
        {
            await _navigationService.GoToRoomDetailAsync(listing.Id);
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
        finally
        {
            // Clearing the selection lets the same room be tapped again.
            SelectedListing = null;
        }
    }

    private async Task LoadFirstPageAsync(CancellationToken cancellationToken)
    {
        var generation = ++_generation;
        IsBusy = true;
        ErrorMessage = string.Empty;
        EmptyMessage = "Đang tải phòng trọ…";
        try
        {
            var page = await _apiClient.GetListingsAsync(PageSize, cursor: null, cancellationToken);
            if (generation != _generation)
                return;

            Reset(page.Data);
            _nextCursor = page.Pagination.NextCursor;
            HasMore = page.Pagination.HasMore;
            ResultsText = DescribeResults();
            EmptyMessage = "Hiện chưa có phòng nào được đăng.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (generation != _generation)
                return;

            Reset(Array.Empty<ListingSummaryDto>());
            _nextCursor = null;
            HasMore = false;
            ResultsText = "Chưa tải được phòng trọ.";
            EmptyMessage = "Chưa tải được phòng trọ.";
            ErrorMessage = "Không thể tải danh sách phòng. Vui lòng thử lại.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            if (generation == _generation)
            {
                IsBusy = false;
                IsRefreshing = false;
            }
        }
    }

    private void Reset(IReadOnlyList<ListingSummaryDto> listings)
    {
        _listingIds.Clear();
        Listings.Clear();
        Append(listings);
    }

    private void Append(IEnumerable<ListingSummaryDto> listings)
    {
        foreach (var listing in listings)
        {
            if (_listingIds.Add(listing.Id))
                Listings.Add(HomeListingItem.FromDto(listing));
        }
    }

    private string DescribeResults()
        => Listings.Count == 0 ? "Chưa có phòng nào." : $"Đã tải {Listings.Count} phòng";
}
