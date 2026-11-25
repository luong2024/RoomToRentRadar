using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Models.MapSearch;
using RoomToRentRadar.Models.RoomListings;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadar.ViewModels.RoomListings;

internal enum ListingMapState
{
    Loading,
    Ready,
    Offline,
    MissingKey,
    Unavailable
}

public partial class ListingLocationPickerViewModel : ObservableObject
{
    private GeoCoordinate? _draftLocation;
    private ListingMapState _mapState;
    private bool _isClosing;
    private bool _closed;

    internal Func<ListingLocationPickerResult, Task<bool>>? ClosePickerAsync { get; set; }

    public GeoCoordinate? DraftLocation => _draftLocation;
    public string SelectionSummary => ListingLocation.Describe(_draftLocation);
    public bool IsClosing => _isClosing;

    public string MapMessage => AppResources.GetString(_mapState switch
    {
        ListingMapState.Ready => "ListingLocation_MapHint",
        ListingMapState.Offline => "ListingLocation_MapOffline",
        ListingMapState.MissingKey => "ListingLocation_MapMissingKey",
        ListingMapState.Unavailable => "ListingLocation_MapUnavailable",
        _ => "ListingLocation_MapLoading"
    });

    [ObservableProperty]
    public partial bool HasCloseError { get; private set; }

    internal void Initialize(GeoCoordinate? currentLocation) =>
        SetDraft(ListingLocation.IsValid(currentLocation) ? currentLocation : null);

    internal void SetMapState(ListingMapState state)
    {
        if (!_closed && SetProperty(ref _mapState, state, nameof(MapMessage)))
        {
            ConfirmCommand.NotifyCanExecuteChanged();
        }
    }

    internal void Select(GeoCoordinate coordinate)
    {
        if (!_closed && !_isClosing && _mapState == ListingMapState.Ready && ListingLocation.IsValid(coordinate))
        {
            SetDraft(coordinate);
        }
    }

    internal void EndSession()
    {
        _closed = true;
        ClosePickerAsync = null;
        NotifyClosingChanged();
    }

    private void SetDraft(GeoCoordinate? coordinate)
    {
        if (SetProperty(ref _draftLocation, coordinate, nameof(DraftLocation)))
        {
            OnPropertyChanged(nameof(SelectionSummary));
            ConfirmCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanCancel() => !_closed && !_isClosing;
    private bool CanConfirm() => CanCancel() && _mapState == ListingMapState.Ready && _draftLocation is not null;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private Task ConfirmAsync() => CanConfirm()
        ? CloseAsync(new(ListingLocationPickerOutcome.Confirmed, _draftLocation))
        : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private Task CancelAsync() => CloseAsync(new(ListingLocationPickerOutcome.Cancelled));

    private async Task CloseAsync(ListingLocationPickerResult result)
    {
        if (!CanCancel() || ClosePickerAsync is not { } closePicker)
        {
            return;
        }

        _isClosing = true;
        HasCloseError = false;
        NotifyClosingChanged();

        try
        {
            if (await closePicker(result))
            {
                EndSession();
            }
            else if (!_closed)
            {
                HasCloseError = true;
            }
        }
        finally
        {
            _isClosing = false;
            NotifyClosingChanged();
        }
    }

    private void NotifyClosingChanged()
    {
        OnPropertyChanged(nameof(IsClosing));
        ConfirmCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }
}
