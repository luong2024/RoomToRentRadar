using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Models.MapSearch;
using RoomToRentRadar.Models.RoomListings;
using RoomToRentRadar.Services.MapSearch;

namespace RoomToRentRadar.ViewModels.MapSearch;

public partial class MapSearchViewModel
{
    private const int SearchPageSize = 50;
    private readonly IApiClient _apiClient;
    private readonly SavedMapAreaStore _savedAreas;
    private CancellationTokenSource? _searchCancellation;

    public MapSearchViewModel(IApiClient apiClient, SavedMapAreaStore savedAreas)
    {
        _apiClient = apiClient;
        _savedAreas = savedAreas;
    }

    [ObservableProperty]
    private IReadOnlyList<MapSearchRoom> _rooms = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedRoom))]
    private MapSearchRoom? _selectedRoom;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchMessage))]
    private string? _searchMessage;

    [ObservableProperty]
    private bool _canRetrySearch;

    public bool HasSelectedRoom => SelectedRoom is not null;
    public bool HasSearchMessage => SearchMessage is not null;

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task ConfirmAreaAsync()
    {
        if (IsInteractingMode || IsSearching)
            return;

        if (!HasDrawnPolygon || PolygonCoordinates.Count < 3)
        {
            StatusMessage = "Please draw a valid area before confirming.";
            return;
        }

        if (!HasValidBoundary())
        {
            StartEditing();
            SetInvalidBoundaryStatus();
            return;
        }

        var boundary = PolygonCoordinates.ToArray();
        IsAreaConfirmed = true;
        using var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        IsSearching = true;
        CanRetrySearch = false;
        SelectedRoom = null;
        SearchMessage = "Searching for rooms…";

        try
        {
            var rooms = new List<MapSearchRoom>();
            var roomIds = new HashSet<string>(StringComparer.Ordinal);
            for (int page = 1; ; page++)
            {
                var result = await _apiClient.PostAsync<MapSearchRequest, PagedResponse<MapSearchRoom>>(
                    "api/v1/rooms/spatial-search", new MapSearchRequest(boundary, page, SearchPageSize),
                    cancellation.Token);

                cancellation.Token.ThrowIfCancellationRequested();
                if (result is null || result.Page != page || result.PageSize <= 0 || result.Items is null)
                    throw new InvalidDataException("The spatial search response did not contain the requested page.");

                foreach (var room in result.Items)
                {
                    if (roomIds.Add(room.Id))
                        rooms.Add(room);
                }

                if (!result.HasMorePages)
                    break;
            }

            Rooms = rooms;
            SearchMessage = rooms.Count == 0
                ? "No rooms found in this area."
                : rooms.Count == 1 ? "1 room found" : $"{rooms.Count} rooms found";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException
            or JsonException or InvalidDataException)
        {
            if (!cancellation.IsCancellationRequested)
            {
                SearchMessage = "Couldn’t load rooms. Try again.";
                CanRetrySearch = true;
            }
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation))
            {
                _searchCancellation = null;
                IsSearching = false;
            }
        }
    }

    public void CancelSearch()
    {
        if (_searchCancellation is not { } cancellation)
            return;

        _searchCancellation = null;
        cancellation.Cancel();
        IsSearching = false;
        SearchMessage = "Search paused. Confirm the area to search again.";
        CanRetrySearch = true;
    }

    private void ResetSearchResults()
    {
        CancelSearch();
        Rooms = [];
        SelectedRoom = null;
        SearchMessage = null;
        CanRetrySearch = false;
    }

    public Task<IReadOnlyList<SavedMapArea>> GetSavedAreasAsync(Guid accountId) => _savedAreas.LoadAsync(accountId);

    public async Task<bool> SaveCurrentAreaAsync(Guid accountId, string name)
    {
        if (!IsAreaConfirmed || IsInteractingMode || string.IsNullOrWhiteSpace(name))
            return false;

        await _savedAreas.SaveAsync(accountId, name, PolygonCoordinates.ToArray());
        return true;
    }

    public Task RenameSavedAreaAsync(Guid accountId, Guid id, string name) =>
        string.IsNullOrWhiteSpace(name) ? Task.CompletedTask : _savedAreas.RenameAsync(accountId, id, name);

    public Task DeleteSavedAreaAsync(Guid accountId, Guid id) => _savedAreas.DeleteAsync(accountId, id);

    public Task OpenSavedAreaAsync(SavedMapArea area)
    {
        if (area.Boundary is not { Length: >= 3 } || area.Boundary.Any(point => point is null) ||
            !TryMeasureBoundary(area.Boundary.ToList(), out double size, out _) || size < MinimumPolygonAreaSquareMeters)
            throw new InvalidDataException("The saved area has an invalid boundary.");

        ClearPolygon();
        foreach (var point in area.Boundary)
        {
            PolygonCoordinates.Add(point);
            CurrentArea.Vertices.Add(point);
        }
        VertexCount = PolygonCoordinates.Count;
        HasDrawnPolygon = true;
        return ConfirmAreaAsync();
    }
}
