using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;

namespace RoomToRentRadar.PageModels;

/// <summary>
/// ViewModel màn hình Phòng Yêu Thích hỗ trợ xóa tạm và Hoàn tác trong 5 giây (US06 AC4).
/// </summary>
public partial class FavouritesViewModel : ObservableObject
{
    private readonly IFavouritesService _favouritesService;
    private readonly TimeSpan _undoDuration;

    private CancellationTokenSource? _pendingUndoCts;
    private RoomListingItemModel? _pendingRemovedRoom;
    private int _pendingRemovedIndex = -1;

    private bool _isLoading;
    private bool _showUndoBar;
    private string _undoMessage = string.Empty;
    private string _errorMessage = string.Empty;

    public FavouritesViewModel(IFavouritesService favouritesService, TimeSpan? undoDuration = null)
    {
        _favouritesService = favouritesService;
        _undoDuration = undoDuration ?? TimeSpan.FromSeconds(5);
    }

    public ObservableCollection<RoomListingItemModel> Favourites { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public bool ShowUndoBar
    {
        get => _showUndoBar;
        set => SetProperty(ref _showUndoBar, value);
    }

    public string UndoMessage
    {
        get => _undoMessage;
        set => SetProperty(ref _undoMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public int FavouriteCount => Favourites.Count;
    public bool IsEmpty => !IsLoading && Favourites.Count == 0;

    [RelayCommand]
    public async Task LoadFavouritesAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var items = await _favouritesService.GetFavouritesAsync(cancellationToken);
            Favourites.Clear();
            foreach (var item in items)
            {
                item.IsFavourite = true;
                Favourites.Add(item);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Không thể tải danh sách yêu thích: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyCountChanged();
        }
    }

    /// <summary>
    /// Xóa phòng khỏi danh sách hiển thị ngay và đợi 5 giây cho người dùng bấm "Hoàn tác" trước khi commit xóa thật.
    /// </summary>
    [RelayCommand]
    public async Task RemoveWithUndoAsync(RoomListingItemModel? room)
    {
        if (room is null)
            return;

        // Nếu còn một phòng khác đang chờ xóa trước đó, chốt xóa luôn phòng cũ trước
        await CommitPendingRemovalImmediatelyAsync();

        int index = Favourites.IndexOf(room);
        if (index < 0)
            return;

        Favourites.RemoveAt(index);
        room.IsFavourite = false;
        NotifyCountChanged();

        _pendingRemovedRoom = room;
        _pendingRemovedIndex = index;
        UndoMessage = $"Đã bỏ lưu \"{room.Title}\"";
        ShowUndoBar = true;

        var cts = new CancellationTokenSource();
        _pendingUndoCts = cts;

        try
        {
            await Task.Delay(_undoDuration, cts.Token);

            // Hết 5 giây không bấm Hoàn tác -> gọi service xóa vĩnh viễn
            await _favouritesService.RemoveFavouriteAsync(room.Id);
            ClearPendingUndoState();
        }
        catch (TaskCanceledException)
        {
            // Người dùng đã bấm Hoàn tác trong thời gian 5 giây
        }
    }

    [RelayCommand]
    public void UndoRemove()
    {
        if (_pendingRemovedRoom is null)
            return;

        _pendingUndoCts?.Cancel();
        _pendingUndoCts?.Dispose();
        _pendingUndoCts = null;

        var restored = _pendingRemovedRoom;
        int targetIndex = Math.Clamp(_pendingRemovedIndex, 0, Favourites.Count);
        restored.IsFavourite = true;
        Favourites.Insert(targetIndex, restored);

        ClearPendingUndoState();
        NotifyCountChanged();
    }

    private async Task CommitPendingRemovalImmediatelyAsync()
    {
        if (_pendingRemovedRoom is null)
            return;

        var oldRoom = _pendingRemovedRoom;
        _pendingUndoCts?.Cancel();
        _pendingUndoCts?.Dispose();
        _pendingUndoCts = null;
        ClearPendingUndoState();

        await _favouritesService.RemoveFavouriteAsync(oldRoom.Id);
    }

    private void ClearPendingUndoState()
    {
        _pendingRemovedRoom = null;
        _pendingRemovedIndex = -1;
        ShowUndoBar = false;
        UndoMessage = string.Empty;
    }

    private void NotifyCountChanged()
    {
        OnPropertyChanged(nameof(FavouriteCount));
        OnPropertyChanged(nameof(IsEmpty));
    }
}