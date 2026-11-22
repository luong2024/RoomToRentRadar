using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Services;
using RoomToRentRadar.Services.Auth;
using RoomToRentRadar.Services.ErrorHandling;
using RoomToRentRadar.Services.Interactions;
using RoomToRentRadar.Services.Navigation;
using RoomToRentRadar.Services.Storage;

namespace RoomToRentRadar.ViewModels;

public enum FavouritesState
{
    Initial,
    Loading,
    Content,
    Empty,
    SignedOut,
    Error
}

public partial class FavouritesViewModel : ObservableObject
{
    private readonly IFavouritesService _favouritesService;
    private readonly IUndoService _undoService;
    private readonly IErrorHandler _errorHandler;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingVisible))]
    [NotifyPropertyChangedFor(nameof(IsContentVisible))]
    [NotifyPropertyChangedFor(nameof(IsEmptyVisible))]
    [NotifyPropertyChangedFor(nameof(IsSignedOutVisible))]
    [NotifyPropertyChangedFor(nameof(IsErrorVisible))]
    public partial FavouritesState State { get; set; }

    public FavouritesViewModel(
        IFavouritesService favouritesService,
        IUndoService undoService,
        IErrorHandler errorHandler,
        INavigationService navigationService)
    {
        _favouritesService = favouritesService;
        _undoService = undoService;
        _errorHandler = errorHandler;
        _navigationService = navigationService;
        State = FavouritesState.Initial;
    }

    public ObservableCollection<FavouriteRoomItem> Favourites { get; } = [];

    public bool IsLoadingVisible => State is FavouritesState.Initial or FavouritesState.Loading;

    public bool IsContentVisible => State == FavouritesState.Content;

    public bool IsEmptyVisible => State == FavouritesState.Empty;

    public bool IsSignedOutVisible => State == FavouritesState.SignedOut;

    public bool IsErrorVisible => State == FavouritesState.Error;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (State == FavouritesState.Loading)
            return;

        var hadContent = State == FavouritesState.Content;
        if (!hadContent)
            State = FavouritesState.Loading;

        try
        {
            var signedIn = await _favouritesService.RefreshAsync(cancellationToken);
            if (!signedIn)
            {
                Favourites.Clear();
                State = FavouritesState.SignedOut;
                return;
            }

            ApplyItems();
            SyncState();
        }
        catch (Exception ex)
        {
            if (hadContent)
            {
                // A background refresh failed; keep showing the last known list.
                return;
            }

            State = FavouritesState.Error;
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand]
    private async Task OpenRoomAsync(FavouriteRoomItem item)
    {
        try
        {
            await _navigationService.GoToRoomDetailAsync(item.ListingId);
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(FavouriteRoomItem item, CancellationToken cancellationToken)
    {
        try
        {
            var removed = await _favouritesService.RemoveAsync(item.ListingId, cancellationToken);
            Favourites.Remove(item);
            SyncState();

            if (removed is not null)
            {
                await _undoService.OfferRemovalUndoAsync(removed, cancellationToken);
                ApplyItems();
                SyncState();
            }
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
            ApplyItems();
            SyncState();
        }
    }

    [RelayCommand]
    private async Task ShowLoginAsync()
    {
        try
        {
            await _navigationService.GoToLoginAsync();
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
    }

    private void ApplyItems()
    {
        Favourites.Clear();
        foreach (var favourite in _favouritesService.Items)
        {
            Favourites.Add(FavouriteRoomItem.FromDto(favourite));
        }
    }

    private void SyncState()
        => State = Favourites.Count == 0 ? FavouritesState.Empty : FavouritesState.Content;
}
