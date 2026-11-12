using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services;
using RoomToRentRadar.Shared.Listings;

namespace RoomToRentRadar.Features.Map.ViewModels;

public partial class MapSearchPageModel : ObservableObject
{
    public const int ResultLimit = 100;

    private readonly IApiClient _apiClient;
    private readonly ISavedAreaSearchStore _savedAreaStore;
    private readonly IErrorHandler _errorHandler;
    private CancellationTokenSource? _searchCts;
    private readonly INavigationService? _navigationService;
    private string? _nextCursor;
    private readonly HashSet<Guid> _listingIds = [];
    private bool _restoringArea;
    private bool _savedAreasLoaded;
    private bool _areaRejected;

    public MapSearchPageModel(
        IApiClient apiClient,
        ISavedAreaSearchStore savedAreaStore,
        IErrorHandler errorHandler, INavigationService? navigationService = null)
    {
        _apiClient = apiClient;
        _savedAreaStore = savedAreaStore;
        _errorHandler = errorHandler;
        _navigationService = navigationService;

        TryLoadSavedAreas();
    }

    /// <summary>Raised when the draft polygon or the pins need to be redrawn.</summary>
    public event EventHandler? MapDataChanged;

    /// <summary>Raised after a polygon is closed so the map can fit its bounds.</summary>
    public event EventHandler? FocusRequested;

    public PolygonDraft Draft { get; } = new();

    public ObservableCollection<MapListingItem> Listings { get; } = [];

    public ObservableCollection<SavedAreaSearch> SavedAreas { get; } = [];

    public IReadOnlyList<MapSortOption> SortOptions { get; } = MapSortOption.All;

    [ObservableProperty]
    public partial bool IsDrawing { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    public bool CanLoadMore => _nextCursor is not null && !IsBusy && !IsLoadingMore && HasPolygon;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(CanSaveCurrentArea));
    }
    partial void OnIsLoadingMoreChanged(bool value) => OnPropertyChanged(nameof(CanLoadMore));

    [ObservableProperty]
    public partial string StatusMessage { get; set; }
        = "Chạm “Vẽ khu vực”, sau đó chạm lên bản đồ để thêm đỉnh.";

    [ObservableProperty]
    public partial string ResultsText { get; set; } = "";

    [ObservableProperty]
    public partial MapListingItem? SelectedListing { get; set; }

    [ObservableProperty]
    public partial int SelectedSortIndex { get; set; } = 1;

    [ObservableProperty]
    public partial string NewAreaName { get; set; } = "";

    [ObservableProperty]
    public partial SavedAreaSearch? SelectedSavedArea { get; set; }

    [ObservableProperty]
    public partial bool IsSavedPanelOpen { get; set; }

    public bool HasPolygon => Draft.IsClosed;
    public bool HasDraft => Draft.Vertices.Count > 0;
    public int DraftVertexCount => Draft.Vertices.Count;
    public string DrawingHint => IsDrawing
        ? StatusMessage
        : HasPolygon ? $"Vùng đã chọn · {DraftVertexCount} điểm" : "Vẽ vùng quanh nơi bạn muốn tìm trọ.";

    public bool HasSelection => SelectedListing is not null;

    /// <summary>The preview and the saved-area panel share the bottom corner on phones.</summary>
    public bool ShowPreviewCard => HasSelection && !IsSavedPanelOpen;

    public bool HasResults => Listings.Count > 0;

    public bool HasSavedAreas => SavedAreas.Count > 0;

    public bool HasSelectedSavedArea => SelectedSavedArea is not null;

    public bool ShowStartDrawing => !IsDrawing;

    public bool ShowDrawingTools => IsDrawing;

    public bool CanFinishDrawing => IsDrawing && Draft.Vertices.Count >= PolygonDraft.MinimumVertices;

    public bool CanSaveCurrentArea => HasPolygon && !_areaRejected && !IsBusy && !IsDrawing
        && !string.IsNullOrWhiteSpace(NewAreaName);

    public bool ShowSearchButton => HasPolygon && !IsDrawing;

    partial void OnIsDrawingChanged(bool value)
    {
        OnPropertyChanged(nameof(DrawingHint));
        OnPropertyChanged(nameof(ShowStartDrawing));
        OnPropertyChanged(nameof(ShowDrawingTools));
        OnPropertyChanged(nameof(CanFinishDrawing));
        OnPropertyChanged(nameof(CanSaveCurrentArea));
        OnPropertyChanged(nameof(ShowSearchButton));
    }

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(DrawingHint));

    partial void OnSelectedListingChanged(MapListingItem? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowPreviewCard));
        MapDataChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsSavedPanelOpenChanged(bool value) => OnPropertyChanged(nameof(ShowPreviewCard));

    partial void OnNewAreaNameChanged(string value) => OnPropertyChanged(nameof(CanSaveCurrentArea));

    partial void OnSelectedSavedAreaChanged(SavedAreaSearch? value) => OnPropertyChanged(nameof(HasSelectedSavedArea));

    partial void OnSelectedSortIndexChanged(int value)
    {
        if (Draft.IsClosed && !_restoringArea)
            SearchCommand.Execute(null);
    }

    public void AddVertex(double latitude, double longitude)
    {
        if (IsDrawing && Draft.Vertices.Count >= SavedAreaSearch.MaximumVertices)
        {
            StatusMessage = $"Khu vực có tối đa {SavedAreaSearch.MaximumVertices} đỉnh.";
            return;
        }
        if (!IsDrawing || !Draft.Add(new GeoPoint(latitude, longitude)))
            return;

        StatusMessage = Draft.Vertices.Count < PolygonDraft.MinimumVertices
            ? $"Đã thêm {Draft.Vertices.Count} điểm · tiếp tục chấm quanh vùng muốn tìm."
            : $"Đã thêm {Draft.Vertices.Count} điểm · tiếp tục chấm hoặc bấm Tìm phòng để khép vùng.";
        NotifyDraftChanged();
    }

    public void SelectListing(MapListingItem? listing)
        => SelectedListing = listing is null ? null : Listings.FirstOrDefault(item => item.Listing.Id == listing.Listing.Id);

    public void CancelPendingSearch()
    {
        Interlocked.Exchange(ref _searchCts, null)?.Cancel();
        IsBusy = false;
        IsLoadingMore = false;
    }

    private void ClearResults()
    {
        SelectedListing = null;
        Listings.Clear();
        _listingIds.Clear();
        _nextCursor = null;
        ResultsText = "";
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(CanLoadMore));
        MapDataChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void StartDrawing()
    {
        CancelPendingSearch();
        Draft.Clear();
        ClearResults();
        _areaRejected = false;
        IsSavedPanelOpen = false;
        IsDrawing = true;
        OnPropertyChanged(nameof(HasResults));
        StatusMessage = "Chạm để chấm nhiều điểm quanh khu vực. Bấm Tìm phòng khi đã vẽ xong.";
        NotifyDraftChanged();
    }

    [RelayCommand]
    private void UndoVertex()
    {
        if (!IsDrawing || !Draft.RemoveLast())
            return;

        StatusMessage = Draft.Vertices.Count == 0
            ? "Chạm lên bản đồ để thêm đỉnh đầu tiên."
            : $"Còn {Draft.Vertices.Count} điểm · chạm để thêm hoặc bấm Tìm phòng khi đã vẽ xong.";
        NotifyDraftChanged();
    }

    [RelayCommand]
    private async Task FinishDrawingAsync()
    {
        if (Draft.CanClose && !Draft.HasValidGeometry)
        {
            StatusMessage = "Vùng bị tự cắt hoặc các điểm nằm trên một đường thẳng. Hãy hoàn tác điểm rồi vẽ lại.";
            return;
        }
        if (!Draft.Close())
        {
            StatusMessage = $"Cần ít nhất {PolygonDraft.MinimumVertices} đỉnh trước khi hoàn tất.";
            NotifyDraftChanged();
            return;
        }

        IsDrawing = false;
        StatusMessage = "Đang tìm phòng trong khu vực…";
        NotifyDraftChanged();
        FocusRequested?.Invoke(this, EventArgs.Empty);
        await SearchInternalAsync();
    }

    [RelayCommand]
    private void CancelDrawing()
    {
        CancelPendingSearch();
        Draft.Clear();
        ClearResults();
        IsDrawing = false;
        StatusMessage = "Đã xóa khu vực đang vẽ.";
        NotifyDraftChanged();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task Search() => SearchInternalAsync();

    [RelayCommand]
    private Task LoadMoreAsync() => CanLoadMore ? SearchInternalAsync(true) : Task.CompletedTask;

    [RelayCommand]
    private async Task OpenSelectedListingAsync()
    {
        if (_navigationService is null || SelectedListing is not { } selected
            || !Listings.Contains(selected))
            return;
        try { await _navigationService.GoToRoomDetailAsync(selected.Listing.Id); }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
    }

    private async Task SearchInternalAsync(bool append = false)
    {
        if (!Draft.IsClosed)
            return;

        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _searchCts, cts)?.Cancel();
        var cursor = append ? _nextCursor : null;
        var centroid = Draft.Centroid;
        if (!append)
        {
            ClearResults();
            _areaRejected = false;
        }
        IsLoadingMore = append;
        IsBusy = !append;
        StatusMessage = append ? "Đang tải thêm phòng…" : "Đang tìm phòng trong khu vực…";
        try
        {
            var request = new AreaSearchRequest
            {
                Area = new AreaPolygon
                {
                    Type = "Polygon",
                    Coordinates = Draft.ToGeoJsonCoordinates()
                },
                Sort = SortOptions[Math.Clamp(SelectedSortIndex, 0, SortOptions.Count - 1)].WireValue,
                Limit = ResultLimit,
                Cursor = cursor
            };

            var result = await _apiClient.SearchByAreaAsync(request, cts.Token);
            if (cts.IsCancellationRequested)
                return;

            foreach (var listing in result.Data)
            {
                if (!_listingIds.Add(listing.Id))
                    continue;
                Listings.Add(new MapListingItem(
                    listing,
                    RoomListingService.DistanceKm(centroid.Lat, centroid.Lng, listing.Lat, listing.Lng)));
            }

            _nextCursor = result.Pagination.HasMore ? result.Pagination.NextCursor : null;
            ResultsText = result.TotalCountCapped
                ? $"Hiển thị {Listings.Count} / {result.TotalCount}+ phòng"
                : $"Hiển thị {Listings.Count} / {result.TotalCount} phòng";
            StatusMessage = Listings.Count == 0
                ? "Không có phòng nào trong khu vực này."
                : $"Tìm thấy {result.TotalCount} phòng trong khu vực.";
        }
        catch (OperationCanceledException)
        {
            if (!cts.IsCancellationRequested)
                StatusMessage = "Tìm kiếm quá lâu. Vui lòng thử lại.";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            if (!cts.IsCancellationRequested)
            {
                _areaRejected = true;
                StatusMessage = InvalidAreaStatusMessage(ex.Code);
            }
        }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested)
                return;

            StatusMessage = "Không thể tìm phòng trong khu vực. Vui lòng thử lại.";
            _errorHandler.HandleError(ex);
        }
        finally
        {
            if (ReferenceEquals(_searchCts, cts))
            {
                _searchCts = null;
                IsBusy = false;
                IsLoadingMore = false;
                OnPropertyChanged(nameof(HasResults));
                MapDataChanged?.Invoke(this, EventArgs.Empty);
            }

            cts.Dispose();
        }
    }

    private static string InvalidAreaStatusMessage(string? code) => code switch
    {
        "area_too_large" => "Khu vực quá lớn (tối đa 100 km²). Hãy xóa và vẽ khu vực nhỏ hơn.",
        "self_intersecting_area" => "Khu vực bị tự cắt. Hãy xóa và vẽ lại.",
        "invalid_area_vertex_count" => "Khu vực có quá nhiều đỉnh (tối đa 200). Hãy xóa và vẽ lại.",
        _ => "Khu vực vẽ không hợp lệ (có thể bị tự cắt hoặc quá lớn). Hãy xóa và vẽ lại."
    };

    [RelayCommand]
    private void SaveArea()
    {
        if (!Draft.IsClosed || IsBusy || IsDrawing || _areaRejected)
        {
            StatusMessage = "Hãy hoàn tất khu vực trước khi lưu.";
            return;
        }

        var name = NewAreaName.Trim();
        if (name.Length == 0 || name.Length > SavedAreaSearch.MaximumNameLength)
        {
            StatusMessage = $"Tên khu vực cần từ 1 đến {SavedAreaSearch.MaximumNameLength} ký tự.";
            return;
        }

        if (!TryLoadSavedAreas())
            return;
        if (SavedAreas.Any(area => string.Equals(area.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "Tên khu vực đã tồn tại. Hãy chọn tên khác.";
            return;
        }
        if (SavedAreas.Count >= SavedAreaSearch.MaximumCount)
        {
            StatusMessage = $"Có thể lưu tối đa {SavedAreaSearch.MaximumCount} khu vực.";
            return;
        }

        var saved = new SavedAreaSearch(
            Guid.NewGuid().ToString("N"),
            name,
            Draft.Vertices.ToArray(),
            DateTime.UtcNow)
        {
            Sort = SortOptions[Math.Clamp(SelectedSortIndex, 0, SortOptions.Count - 1)].WireValue
        };

        if (!saved.IsValid)
        {
            StatusMessage = "Khu vực vẽ không hợp lệ. Hãy xóa và vẽ lại.";
            return;
        }

        var areas = SavedAreas.Append(saved).ToArray();
        try { _savedAreaStore.ReplaceAll(areas); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusMessage = "Không thể lưu khu vực. Dữ liệu hiện có được giữ nguyên; hãy thử lại.";
            return;
        }
        SavedAreas.Add(saved);
        SelectedSavedArea = saved;
        NewAreaName = "";
        StatusMessage = $"Đã lưu khu vực “{name}”.";
        OnPropertyChanged(nameof(HasSavedAreas));
    }

    [RelayCommand]
    private void ApplySavedArea()
    {
        if (SelectedSavedArea is not { } saved || !SavedAreas.Contains(saved) || !saved.IsValid)
            return;

        CancelPendingSearch();
        ClearResults();
        if (!Draft.Load(saved.Vertices))
        {
            StatusMessage = $"Khu vực “{saved.Name}” không hợp lệ.";
            NotifyDraftChanged();
            return;
        }

        IsDrawing = false;
        IsSavedPanelOpen = false;
        _restoringArea = true;
        try { SelectedSortIndex = saved.Sort == ListingSortWire.PriceAsc ? 0 : 1; }
        finally { _restoringArea = false; }
        SelectedListing = null;
        Listings.Clear();
        ResultsText = "";
        OnPropertyChanged(nameof(HasResults));
        StatusMessage = $"Đang tìm phòng trong khu vực “{saved.Name}”…";
        NotifyDraftChanged();
        FocusRequested?.Invoke(this, EventArgs.Empty);
        SearchCommand.Execute(null);
    }

    [RelayCommand]
    private void DeleteSavedArea()
    {
        if (SelectedSavedArea is not { } saved)
            return;

        var remaining = SavedAreas.Where(area => area.Id != saved.Id).ToArray();
        try
        {
            if (!TryLoadSavedAreas())
                return;
            _savedAreaStore.ReplaceAll(remaining);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusMessage = "Không thể xóa khu vực. Dữ liệu hiện có được giữ nguyên; hãy thử lại.";
            return;
        }
        SavedAreas.Remove(saved);
        SelectedSavedArea = SavedAreas.FirstOrDefault();
        StatusMessage = $"Đã xóa khu vực “{saved.Name}”.";
        OnPropertyChanged(nameof(HasSavedAreas));
    }

    [RelayCommand]
    private void ClearSelection() => SelectedListing = null;

    [RelayCommand]
    private void ToggleSavedAreas() => IsSavedPanelOpen = !IsSavedPanelOpen;

    private bool TryLoadSavedAreas()
    {
        if (_savedAreasLoaded)
            return true;
        try
        {
            var areas = _savedAreaStore.GetAll();
            SavedAreas.Clear();
            foreach (var area in areas)
                SavedAreas.Add(area);
            _savedAreasLoaded = true;
            OnPropertyChanged(nameof(HasSavedAreas));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusMessage = "Không thể đọc khu vực đã lưu. Dữ liệu hiện có được giữ nguyên; hãy thử lại.";
            return false;
        }
    }

    private void NotifyDraftChanged()
    {
        OnPropertyChanged(nameof(HasDraft));
        OnPropertyChanged(nameof(DraftVertexCount));
        OnPropertyChanged(nameof(DrawingHint));
        OnPropertyChanged(nameof(HasPolygon));
        OnPropertyChanged(nameof(CanFinishDrawing));
        OnPropertyChanged(nameof(CanSaveCurrentArea));
        OnPropertyChanged(nameof(ShowSearchButton));
        MapDataChanged?.Invoke(this, EventArgs.Empty);
    }
}
