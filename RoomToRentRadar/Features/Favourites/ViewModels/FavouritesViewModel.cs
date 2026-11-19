using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Favourites;
using RoomToRentRadar.Shared.Favourites;

namespace RoomToRentRadar.Features.Favourites.ViewModels;

public partial class FavouritesViewModel : ObservableObject
{
    public static readonly TimeSpan DefaultUndoWindow = TimeSpan.FromSeconds(5);

    private readonly IFavouritesStore _favouritesStore;
    private readonly IErrorHandler _errorHandler;
    private readonly INavigationService _navigationService;
    private readonly TimeSpan _undoWindow;
    private CancellationTokenSource? _undoCancellation;
    private FavouriteListingItem? _pendingUndo;
    private int _pendingUndoIndex;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsUndoVisible { get; set; }

    [ObservableProperty]
    public partial string UndoMessage { get; set; }

    public FavouritesViewModel(
        IFavouritesStore favouritesStore,
        IErrorHandler errorHandler,
        INavigationService navigationService,
        TimeSpan? undoWindow = null)
    {
        _favouritesStore = favouritesStore;
        _errorHandler = errorHandler;
        _navigationService = navigationService;
        _undoWindow = undoWindow ?? DefaultUndoWindow;
        ErrorMessage = string.Empty;
        UndoMessage = string.Empty;
    }

    public ObservableCollection<FavouriteListingItem> Items { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        await LoadCoreAsync(cancellationToken);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            IsRefreshing = false;
            return;
        }

        await LoadCoreAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RemoveAsync(FavouriteListingItem? item)
    {
        if (item is null)
            return;

        var index = Items.IndexOf(item);
        if (index < 0)
            return;

        ErrorMessage = string.Empty;
        Items.RemoveAt(index);
        try
        {
            await _favouritesStore.RemoveAsync(item.ListingId);
        }
        catch (Exception ex)
        {
            Items.Insert(Math.Min(index, Items.Count), item);
            ErrorMessage = "Không thể bỏ lưu phòng này. Vui lòng thử lại.";
            _errorHandler.HandleError(ex);
            return;
        }

        BeginUndo(item, index);
    }

    [RelayCommand]
    private async Task UndoAsync()
    {
        if (_pendingUndo is not { } item)
            return;

        var index = _pendingUndoIndex;
        ClearUndo();
        ErrorMessage = string.Empty;

        Items.Insert(Math.Min(index, Items.Count), item);
        try
        {
            await _favouritesStore.AddAsync(item.ListingId);
        }
        catch (Exception ex)
        {
            Items.Remove(item);
            ErrorMessage = "Không thể hoàn tác. Phòng đã bị bỏ lưu.";
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand]
    private async Task OpenAsync(FavouriteListingItem? item)
    {
        if (item is null)
            return;

        try
        {
            await _navigationService.GoToRoomDetailAsync(item.ListingId);
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    public void CancelUndoWindow() => ClearUndo();

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var favourites = await _favouritesStore.RefreshAsync(cancellationToken);
            Render(favourites);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = "Không thể tải danh sách yêu thích. Vui lòng thử lại.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    private void Render(IReadOnlyList<FavouriteListingDto> favourites)
    {
        Items.Clear();
        foreach (var favourite in favourites)
        {
            Items.Add(FavouriteListingItem.FromDto(favourite));
        }
    }

    private void BeginUndo(FavouriteListingItem item, int index)
    {
        StopUndoTimer();
        _pendingUndo = item;
        _pendingUndoIndex = index;
        UndoMessage = $"Đã bỏ lưu \"{item.Title}\"";
        IsUndoVisible = true;

        var cancellation = new CancellationTokenSource();
        _undoCancellation = cancellation;
        _ = ExpireUndoAsync(cancellation);
    }

    private async Task ExpireUndoAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(_undoWindow, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!ReferenceEquals(_undoCancellation, cancellation))
            return;

        ClearUndo();
    }

    private void ClearUndo()
    {
        StopUndoTimer();
        _pendingUndo = null;
        IsUndoVisible = false;
        UndoMessage = string.Empty;
    }

    private void StopUndoTimer()
    {
        _undoCancellation?.Cancel();
        _undoCancellation?.Dispose();
        _undoCancellation = null;
    }
}
